# Low-Fidelity Atmospherics — Planning Document

Status: **Ready for review (revision 11: region-based topology updates)**
Namespace: `Kiyote.Simulations.LowFidelity.Atmospherics`

---

## 1. Goals

Simulate the atmosphere inside a spaceship well enough to *look and feel* right, as cheaply as possible.

**In scope**

- Each cell holds an amount of each gas (O2, N2, CO2, …). The gas list is defined at application start-up from gas definition records, so mods can add gases.
- Query per cell:
  - **Pressure** (total gas in the cell).
  - **Composition** (fraction of each gas).
- Gas flows from high to low pressure through permeable cell boundaries, and eventually equalises.
- Gas reaching space (off-grid / vacuum) is lost, which drives decompression.
- Rapid flow at large pressure differences, so a hull breach reads as *explosive* decompression rather than a slow leak.
- Hooks so that external systems can add or remove gas between steps:
  - **Pumps** add gas.
  - **Filters** remove specific gases.
- Enough data for an external system to decide when a wall blows out (pressure difference across it).

**Out of scope (owned by callers)**

- Pump and filter behaviour (rates, power, logic). The simulation only exposes "add/remove gas at cell".
- Deciding that a wall fails, and changing the ship topology. The simulation just runs on whatever topology it is given.
- Combustion and gas reactions.
- Heat sources and sinks (heaters, coolers, hull radiation) and heat exchange with objects. These belong to a separate temperature system; the simulation only exposes "add/remove energy at cell" (§3.7).
- Physically accurate fluid dynamics (no Navier–Stokes, no projection, no advection solve).

---

## 2. Relationship to existing code

- **Topology (`Kiyote.Geometry.Topology`)**: defines the ship's grid space. The caller supplies the ship as a set of attached grids (an `IGridAssembly<TCell>`); the simulation **compiles its own `ICompiledGridAssembly<TCell>`** from it and owns every layer it creates. It uses:
  - chunk layout
  - `IGridLayer<T>` storage with halos
  - connectivity (`IGridLayer<Direction>` built from a strategy over the ship cells, e.g. gas permeability)
  - the vacuum layer (`CreateVacuumLayer`)
- **`Kiyote.Simulations.Topology`**: the full-fidelity Pressure / Projection / Advection / Airflow port.
  - It is *not* reused here: Airflow costs about 0.9 ms on 100×100 for a single species, which is too much for N gases on a large ship.
  - We do reuse its conventions:
    - Neighbourhood built once per topology.
    - Source → destination layers, caller swaps.
    - Vectorized row kernels.
    - Halo exchange.
    - `MarkDirty`.
    - Wall = no connectivity, vacuum = off-grid.
- **Current LowFidelity files**: these are placeholders and will be replaced (see §8).
  - **Nothing existing in the `Atmospherics` namespace has to be kept.** Every file there may be replaced, changed or deleted freely; there are no compatibility constraints.
  - `Gas` enum
  - `CellAtmosphere` record (`float[]` per cell)
  - `GridAtmospherics<TCell>` (8 unit `Vector`s, `NotImplementedException`)
  - `IGridAtmospherics<TCell>`
  - `Vector`

---

## 3. Model

### 3.1 State

The model stores the **amount of each gas per cell**, not pressure and percentages. Storing amounts makes the model:
- **mass-conserving**: flow only moves gas, never creates it;
- able to represent **mixing** correctly;
- able to **add or remove** gas trivially.

Everything else is derived:

| Quantity | Definition |
|---|---|
| Amount of gas *g* in cell *c* | `A[g][c]` (stored, ≥ 0) |
| Pressure | `P[c] = Σg A[g][c] · T[c] / Tref` (kPa; all cells have the same volume, Q4) |
| Fraction of gas *g* | `A[g][c] / P[c]` (0 when `P[c]` = 0) |
| Partial pressure of *g* | `A[g][c]` (same units as pressure) |

**Units (Q8).**

- **Amounts are stored as "kPa at the reference temperature"** (`Tref` = 293.15 K, i.e. 20 °C).
  - A breathable cell at room temperature holds about 21.2 O2 + 79.0 N2 + 0.04 CO2, which gives 101.3 kPa.
  - Because every cell has the same volume, this is just moles times a constant (`R · Tref / V`). The UI can show moles or kg later with no change to the model.
  - Why not store moles directly? Pressure, partial pressure and condensation thresholds are all in kPa, so this choice avoids a multiply in every kernel and keeps numbers readable while debugging.
- **Pressure is reported in kPa** and depends on temperature: `P = A · T / Tref`. Heating a cold, sealed ship raises its pressure with no gas added.
- **Temperature is stored in kelvin** (`T[c]`), so the gas law stays a simple multiply.
- **Time is in seconds.** Every rate in the settings (acceleration, friction, condensation) is "per second", so `Advance( TimeSpan.FromSeconds( 1 ) )` is exactly one second of game time.

### 3.2 Flow: virtual pipes

Use the **virtual pipe** model (as used in game water and gas sims). Every pair of connected cells has a pipe across their shared face, and each pipe stores a **flow** value.

Each step:
1. **Accelerate.** `flow += k · (P[a] − P[b])` — the pressure difference pushes gas through the face.
2. **Damp.** `flow *= (1 − friction)` — this gives the gas inertia without unbounded oscillation.
3. **Limit.** Scale outgoing flows so a cell can never send more than it holds, so amounts stay ≥ 0.
4. **Transfer.** Move gas along each pipe. The gas carried has the composition of the **upwind** cell: gas *g* moves `flow · A[g][up] / P[up]`.

Why pipes rather than plain diffusion (what the Topology Pressure sim does):

- **Momentum.**
  - Gas rushing toward a breach keeps moving, so decompression is violent and fast instead of a slow fade.
  - Doors that open between pressurised and empty rooms produce a visible gust and some sloshing before the rooms settle.
- **Propagation.** A pressure change travels many cells in a short time because flow accumulates. Diffusion only moves one cell per step.
- **Velocity for free.** The flows double as the airflow field, for wind effects, particles, or pulling loose objects toward a breach.
- **Simple and stable.** It is local, mass-conserving and stable with the limiter. There is no global pressure solve.

Only **4-connected faces** (N/E/S/W) are used.
- Each cell owns its **East** and **South** pipes, so there are 2 flow layers. Each pipe is stored once and updated once.
- Diagonals are skipped: they double the cost and pipes already spread isotropically enough for a low-fidelity sim.

### 3.3 Walls, doors, permeability

- Gas permeability is a **per-cell yes/no attribute read from the caller's `TCell`** through a struct strategy (`IAtmosphereCellStrategy<TCell>.IsPermeable( in TCell )`).
- The simulation builds its own connectivity from that with the existing `ConnectivityBuilder`: a face is open when both cells are permeable, and closed otherwise.
- There is no partial permeability. A cell either lets gas in or it doesn't.
- **Open (passable):** the pipe works normally.
- **Closed (wall):** flow is forced to 0. Gas does not move, and pressure can build up on one side.
- **Vacuum:** the face leads off the ship grid into space. The neighbour pressure is 0.
  - Gas leaving through this face is removed and counted as vented (for stats and debugging).
- Impermeable cells (walls, closed doors, machinery) hold no gas and are never simulated.

### 3.4 Pumps and filters (sources and sinks)

All caller input happens **between frames** (see §5.3). The simulation only stores each change; nothing is calculated until `Advance` runs:

```
AddGas(cell, gas, amount, temperature?)  // pump: A[g][c] += amount, mixed in at the given temperature (K)
RemoveGas(cell, gas, amount) -> float  // filter: removes up to what the cell holds, returns what was removed
```

- A pump into an empty room just raises `A[g][c]` at its cell. The pipes carry that gas outward until the room equalises.
- **Incoming temperature.** `AddGas` takes the temperature of the gas being added (K). It defaults to the cell's own temperature, which suits ordinary pumps.
  - The cell temperature becomes the amount-weighted mix, using the same rule as flow between cells (§3.7): `T = ( ΣA·T_cell + amount·T_in ) / ( ΣA + amount )`.
  - Amounts are "kPa at Tref" (moles), so the amount added is independent of temperature. Cold gas simply raises pressure less (`P = A·T/Tref`).
  - The caller never decides whether the gas condenses. If the mix drops below a gas's condensation point, the normal phase-change pass (§3.8) condenses it at `CondensationRate` from the next step. Example: a cryogenic N2 tank pumped into a room chills it, and enough of it frosts out O2.
  - `RemoveGas` takes no temperature: gas leaves at the cell temperature, so the cell temperature is unchanged.
  - `AddCondensate` and `RemoveCondensate` are kept for now (§3.8). Placing solid condensate directly avoids instantly chilling the whole cell's air, which is what would happen if a block of ice went in as very cold gas.
- A filter (CO2 scrubber) removes CO2 at its cell. The resulting low CO2 partial pressure means the flow keeps bringing more CO2 to it.

### 3.5 Pressure and blowout data

- `Pressure` is a derived `IGridLayer<float>`, computed at the end of each `Update` (one vectorized pass). Callers read it freely.
- Blowout detection belongs to the caller. The pressure layer is all it needs (Q5 resolved): a cell is assumed to push equally on all of its sides, so the caller compares a cell's pressure against its wall's strength. No extra helper layer is computed.
- **Decompression flow:**
  1. The caller decides a wall broke.
  2. The caller changes the topology/connectivity (e.g. the wall cell becomes open, or the breach now exposes vacuum).
  3. The caller rebuilds the neighbourhood.
  4. The next steps vent gas through the new opening with high initial acceleration, because the pressure difference is large.

### 3.6 Extensible gases

- The `Gas` enum is **replaced by `GasDefinition` records** registered during application initialisation (base game and mods).
  - `GasDefinition( string Id, string Name, … )`: `Id` is a stable string key such as `"o2"` or `"mymod:xenon"`. Further properties (molar mass, breathability, colour, …) can be added to the record later without touching the kernel.
- Building the gas list is split in two:
  - **`GasRegistryBuilder`** finds every definition, from however they're defined, and returns a new registry from `Build()`. It reads one or more `IGasDefinitionSource`s (for example the base game's O2/N2/CO2, one per mod, or a fixed list in tests). Duplicate `Id`s throw in `Build`.
  - **`IGasRegistry`** is the result. It is immutable from construction, so there is no `Register` or `Freeze` and no "registered too late" state. `Build` assigns each gas a dense index `0..Count-1`, and that index selects the gas's amount layer.
  - **Index order is deterministic:** sources in the order given, definitions in the order each source yields them. The same set of sources gives the same indices, which matters for test reproducibility; indices are still not persisted (see below).
  - The simulation takes the built registry, so `GasCount` is `registry.Count` rather than a setting.
  position in the registry)
  - A `GasIndex` depends on registration order, so it can change between runs when mods change. **Never persist it.** Save the `Id` instead (`registry[ index ].Id`).
  - Duplicate `Id`s throw in `GasRegistryBuilder.Build`.
- The flow kernel never reads gas properties: every gas flows the same way, carried along by the bulk flow (Q3b resolved). This keeps the kernel fast and generic. Only the phase-change pass (§3.8) reads a gas property.

### 3.7 Temperature (Q7)

- **State:** one temperature layer `T[c]` in kelvin, bound to the cell for persistence like the gas layers.
- **Effect on pressure:** `P = A · T / Tref`, so hotter cells push gas toward colder ones. Heating a cold, sealed ship raises its pressure.
- **Heat moves with the gas.** When gas flows from cell *a* into *b*, *b*'s new temperature is the amount-weighted mix: `T[b] = ( A[b]·T[b] + moved·T[a] ) / ( A[b] + moved )`.
  - Temperature is one value per cell, and every gas has the same heat capacity (Q10 resolved).
  - This is computed in the existing transfer pass from per-cell totals. It adds one layer to the pass, not one per gas.
- **Optional conduction:** a cheap diffusion step between connected cells, so a sealed, still room still evens out. It uses the `Conduction` setting, and 0 turns it off.
- **Heat sources and sinks** are the caller's job, like pumps:
  - `AddEnergy( cell, joules )` adds energy to (or, if negative, removes it from) the air in a cell. The temperature change is `ΔT = joules / ( ΣA · HeatCapacity )`, so the same heater warms thin air faster than dense air.
    - `HeatCapacity` is a single setting (J per amount-unit per K) shared by every gas (Q10).
    - A cell with ~0 gas ignores the energy, since there is no air to heat, and `AddEnergy` returns the amount applied so the caller can account for it.
    - Removals are clamped so the temperature can't drop below a small minimum (e.g. 2.7 K).
- **Separate temperature system.** Heat exchange with anything other than air belongs to a separate simulation that runs between frames. Examples: cold objects, walls, radiators, and the hull losing heat to space.
  - **Required coupling (stays here):** air temperature moves with the gas, sets the pressure and drives condensation, so the atmosphere owns it.
  - **Optional coupling (lives outside):** the other system reads `Temperature` and `GetTotalGas( cell )`, works out the exchange, and applies it with `AddEnergy`. A cold object then chills the air around it, and the airflow spreads the cold.
  - **Overshoot at high game speeds.** One frame can cover many atmosphere steps, so the external system should limit each exchange to what would bring the object and the air to the same temperature. Otherwise the temperatures swing back and forth.
  - `Conduction` (air-to-air spreading) stays inside the atmosphere.
- **Empty cells:** a cell with ~0 gas has no meaningful temperature. It takes the temperature of the first gas that arrives (because of the weighted mix), and vacuum is treated as 0 K, since it holds no gas.
- **Cost:** about +2 passes per step (pressure scale and temperature mix). Pressure stays a single fused pass.

### 3.8 Phase change (condensation and freezing)

The naive model, in keeping with Q3b:

- **`GasDefinition` gains an optional `CondensationPoint` (K).** Below it, the gas leaves the air and settles on the floor of that cell. Above it, any condensate evaporates back.
  - Freezing and condensing are the same process here: "gas that is no longer gas". The UI can label it ice or liquid using a second optional `FreezingPoint`, which does not affect the simulation.
- **State:** one condensate layer per *condensable* gas, `C[g][c]`, in the same units as the amounts and bound to the cell. Gases without a condensation point get no layer and cost nothing.
- **Rate-limited.** Each step moves `min( A, CondensationRate · dt · A )` from gas to condensate (or back the other way). This means a cold snap gives a gradual "snowfall" rather than an instant collapse, and lets condensation happen at any game speed without oscillating.
- **Condensate does not flow.** It stays in its cell until it evaporates. Pressure falls as gas condenses, so the remaining air rushes toward the cold area, which is the behaviour you'd expect.
- **Not modelled (naive version):**
  - Latent heat: condensing doesn't warm the cell and evaporating doesn't cool it (Q11 resolved). It could be added later as one optional `GasDefinition` property and one multiply-add in this pass.
  - Pressure-dependent boiling points: the threshold is a fixed temperature.
  - Puddles spreading on their own.
- **Collecting condensate (Q12).**
  - `RemoveCondensate( cell, gas, amount ) -> float` lets the player scoop up condensate (e.g. bag frozen O2). The caller stores what was removed, for example in an item.
  - `AddCondensate( cell, gas, amount )` puts it back down: dropping or unpacking the item.
  - If that cell is above the condensation point, the condensate turns back into gas at `CondensationRate`. So unpacking solid O2 in a heated room causes a pressure spike, which is the intended "bad time".
  - Evaporation is rate-limited, so a large drop produces a fast but survivable surge rather than an instant one. The rate is tuned through `CondensationRate`.
- **Cost:** one pass per condensable gas, skipped entirely when the registry has none. It runs after the transfer pass.
- **Queries:** `GetCondensate( gas )` returns the layer, for rendering frost or puddles.

### 3.9 Wind (airflow force on objects)

Callers need to know whether loose objects should be pushed, for example small items blown out through a breach.

- **What is exposed:** a per-cell **wind vector** `W[c] = (Wx, Wy)` in **kPa** (the dynamic pressure of the moving air), made available by `GetWind( column, row ) -> Vector`. Two layers, `WindX` and `WindY`, are also exposed for bulk reads.
- **Why kPa:** it is the same unit as pressure, so callers convert it to a force with one multiply: `force (N) = W · 1000 · object's exposed area (m²)`. The caller then compares that force with the object's mass and friction to decide whether it moves. That decision is the caller's job, like blowouts.
- **How it's computed (cheap, from the pipes we already have):**
  - A cell's velocity is the average of the flows on its two faces on each axis: `vx = ( flowWest + flowEast ) / 2`, `vy = ( flowNorth + flowSouth ) / 2`. The cell's own East and South flows plus its neighbours' (halo) give all four faces.
  - Flow is converted to velocity by dividing by the cell's gas density (`ΣA`), so that thin, fast air is distinguished from thick, slow air.
  - Wind is `½ · ρ · |v| · v`, using a tunable `WindScale` setting in place of real constants. This keeps it low-fidelity but monotonic: more gas moving faster means more push.
  - It is computed in one fused vectorized pass at the end of each step, and skipped in idle chunks (where wind is 0).
- **Breach behaviour:**
  - Faces that open onto vacuum carry the venting flow, so cells next to a breach show a strong wind pointing out of the ship.
  - It fades as pressure drops, which gives a natural "everything gets sucked out, then it stops".
- **Getting objects out of the ship:**
  - The simulation reports the wind only. Moving objects, and deciding that one left the grid through a vacuum face, is done by the caller.
  - `Vented` and the vacuum faces in connectivity tell the caller where the exits are.

---

## 4. Performance design

The target is to be much cheaper per gas than the Topology Airflow (0.9 ms per species on 100×100).

- **Structure of arrays.** One `IGridLayer<float>` per gas, not a `float[]` per cell.
  - The existing `CellAtmosphere` is replaced for simulation storage. It can remain as a convenience snapshot for UI.
- **Pipe update** touches only pressure (1 layer), connectivity (1 layer) and 2 flow layers. This is independent of gas count.
- **Transfer** is the only per-gas pass. Each gas is a row-wise vectorized stencil (Vector256, 8 floats) over the 2 flow layers, like the Topology diffusion kernel (~25 µs on 100×100).
- **Estimated cost on 100×100 with 3 gases: ~60–100 µs per step.**
  - Pressure: 1 pass.
  - Pipes: 1 pass.
  - Limiter: 1 pass.
  - Transfer: 3 passes.
- **Idle chunks are skipped (Q6 resolved, built in from phase 1).** Each chunk has an "active" flag. A chunk is idle when nothing in it can change:
  - **Empty:** no gas and no condensate (e.g. a vacuum section or unused hull). This is the case you described.
  - **Settled:** all pressure differences, flows and temperature differences are below an epsilon, and no condensate is near its threshold. Most of a pressurised, settled ship is in this state most of the time.
  - **Waking up:** an idle chunk is reactivated when:
    - A neighbouring active chunk sends flow across the shared edge.
    - The caller calls `AddGas`, `RemoveGas`, `AddEnergy`, `AddCondensate` or `RemoveCondensate` in it.
    - `InvalidateTopology` or a cell edit touches it.
  - Flags are recomputed at the end of each step, at almost no extra cost, as part of the pressure pass.
- **Denormal flushing**, same as the existing kernels, for near-equalised and near-vacuum cells.
- **Halo of 1 cell** on all layers. Halos are exchanged once per pass, as in the existing Topology sims.

---

## 5. Proposed API (sketch)

```csharp
// Gas identity (replaces the Gas enum)
public sealed record GasDefinition(
	string Id,
	string Name,
	float? CondensationPoint = null,  // K; null = never condenses
	float? FreezingPoint = null );    // K; display only

// position in the registry; not for persistence

// Immutable once built; no Register/Freeze
public interface IGasRegistry {
  int Count { get; }
  GasIndex Get( string id );                 // throws if unknown
  bool TryGet( string id, out GasIndex index );
  GasDefinition this[ GasIndex gas ] { get; }
  IReadOnlyList<GasDefinition> Definitions { get; } // in index order
}

// Supplies definitions from wherever they live (base game, mod files, tests)
public interface IGasDefinitionSource {
  IEnumerable<GasDefinition> GetDefinitions();
}

// Finds every definition and builds a new registry
public interface IGasRegistryBuilder {
  IGasRegistry Build();  // throws on duplicate Id; assigns dense indices in a deterministic order
}

// Default: DI-injected IEnumerable<IGasDefinitionSource>, one per base game / mod
public sealed class GasRegistryBuilder : IGasRegistryBuilder { /* GasRegistryBuilder( IEnumerable<IGasDefinitionSource> sources ) */ }

// Reads and writes simulation data on the caller's cells (struct, for devirtualization).
// The cell is the source of truth for persistence (save games).
public interface IAtmosphereCellStrategy<TCell> {
  bool IsPermeable( in TCell cell );
  float GetGas( in TCell cell, GasIndex gas );
  void SetGas( ref TCell cell, GasIndex gas, float amount );
  float GetCondensate( in TCell cell, GasIndex gas );
  void SetCondensate( ref TCell cell, GasIndex gas, float amount );
  float GetTemperature( in TCell cell );          // K
  void SetTemperature( ref TCell cell, float kelvin );
}

public interface IAtmosphericsSettings {
  float Acceleration { get; }  // k: how hard pressure differences push (per second)
  float Friction { get; }      // 0..1 damping per second
  float Conduction { get; }    // heat diffusion per second (0 = off)
  float HeatCapacity { get; }  // J per amount-unit per K, shared by all gases (§3.7)
  float CondensationRate { get; } // fraction per second moved between gas and condensate
  float FixedTimeStep { get; } // seconds per internal step (default 0.1)
  float WindScale { get; }     // converts gas flow into wind, in kPa (§3.9)
  int MaxStepsPerAdvance { get; } // cap on fixed steps per Advance call (see 5.2)
}

// Owns the compiled grid and every layer; one instance per ship
public interface IAtmosphere : IDisposable {
  IGasRegistry Gases { get; }
  IGridLayer<float> GetGas( GasIndex gas ); // treat as read-only; change gas through the methods below
  IGridLayer<float> Pressure { get; }        // kPa, result of the last Advance
  IGridLayer<float> Temperature { get; }     // K
  IGridLayer<float> GetCondensate( GasIndex gas ); // condensable gases only
  IGridLayer<float> WindX { get; }            // kPa, dynamic pressure, +x = east (§3.9)
  IGridLayer<float> WindY { get; }            // kPa, +y = south (Topology is y-down)
  Vector GetWind( int column, int row );
  float Vented { get; }                      // total gas lost to space during the last Advance

  // Runs as many fixed steps as fit in the elapsed game time (see 5.2).
  // Returns the number of steps run.
  int Advance( TimeSpan elapsed );

  // Write-back (see 5.1)
  void Commit();                     // copies gas amounts back into the cells

  // Changes: call only between Advance calls (§5.3). Each one stores the
  // value and wakes the cell's chunk; nothing is recalculated until Advance.
  void InvalidateTopology( Rect area ); // permeability or cells changed in this assembly-space area (door, breach, new tile)
  void InvalidateTopology();            // fallback: rebuild all connectivity
  void AddGas( int column, int row, GasIndex gas, float amount, float? temperature = null ); // K; null = cell temperature
  float RemoveGas( int column, int row, GasIndex gas, float amount );
  float AddEnergy( int column, int row, float joules ); // negative removes; returns the energy actually applied
  float GetTotalGas( int column, int row );            // ΣA, for external heat exchange
  void AddCondensate( int column, int row, GasIndex gas, float amount );
  float RemoveCondensate( int column, int row, GasIndex gas, float amount );

  // Queries
  float GetFraction( int column, int row, GasIndex gas );
}

public interface IGridAtmospherics {
  // Compiles the ship grids, binds the gas layers to the cells,
  // and builds connectivity from IsPermeable
  IAtmosphere Create<TCell, TStrategy>(
    IGridAssembly<TCell> ship,
    TStrategy strategy )
    where TStrategy : struct, IAtmosphereCellStrategy<TCell>;
}
```

Notes:

- **The simulation owns everything (Q1).**
  - It receives the ship's grids, compiles its own `ICompiledGridAssembly<TCell>`, and builds connectivity from `IsPermeable`.
  - It owns the gas, pressure, flow and scratch layers.
  - This deliberately differs from the caller-owned-layer convention in `Kiyote.Simulations.Topology`, because the number of layers grows with the number of registered gases.
- **Gas registry.** The `IGasRegistry` (built once by `GasRegistryBuilder` at start-up, typically registered as a DI singleton) and the settings are injected through the constructor.
- **Flows are not persisted.** They are rebuilt within a few steps after loading or recompiling. Only gas amounts live in the cells.

### 5.1 Write-back and topology changes (Q9)

The cell is responsible for persisting gas amounts, and the simulation writes them back **only between `Advance` calls**. This uses the existing compiled-grid mechanisms:

- **Bound gas layers.**
  - Each gas amount layer is created with `ICompiledGridAssembly.Bind`, using an internal struct binding that wraps `TStrategy` and the gas index.
  - `Extract` reads `GetGas` from the cell when the layer is created, and again for any cell added later.
  - The compiled grid's `Commit` calls `SetGas` for cells in dirty chunks only. The simulation marks a chunk dirty when it writes to it.
- **`IAtmosphere.Commit()`** wraps that `Commit`. The caller calls it before saving, or before any edit that needs up-to-date cell values. It is cheap when little has changed, because only dirty chunks are written.
- **Single cells added or removed** (`IGridAssembly.TryAddCell` / `TryRemoveCell`, e.g. building a new floor tile):
  - These are applied to the compiled grid immediately.
  - New cells are read in through `Extract`.
  - The caller reports the changed area with `InvalidateTopology( area )`, the same call used for doors (below). No recompile is needed.
- **Permeability changes inside existing cells** (a door opens, or a wall is breached because the caller flips the attribute):
  - The grid has no way to detect these, but the caller knows exactly where they happened.
  - The caller reports each changed region with `InvalidateTopology( Rect area )`, in assembly space.
  - **Local update, not a rebuild.** At the next `Advance`, each pending area is passed to the existing `IConnectivityBuilder.Update( compiled, layer, strategy, area )`.
    - It re-evaluates only the cells in the area plus a one-cell border, and refreshes the halos it touched.
    - A door is a 1×1 area, so this costs about 9 cell evaluations, instead of rebuilding connectivity for the whole ship.
  - **Pending areas are collected, not merged eagerly.** Areas are kept in a small reused list between frames, and at `Advance` they are processed one by one.
    - Overlapping areas are simply re-evaluated twice; it's cheap and still correct.
    - Above a threshold (e.g. when the areas cover more than a quarter of the ship), the simulation does a full `Build` instead.
  - **Flows on changed faces.** Pipes on faces that became closed are zeroed in the same area, so no flow survives across a newly closed door. Pipes on newly opened faces start at 0 and accelerate on the next step.
  - **Chunks are woken.** Every chunk the area touches (plus its border) is marked active, so a settled room next to a newly breached wall starts simulating immediately.
  - `InvalidateTopology()` with no area is kept as a fallback that rebuilds everything.
  - Gas amounts are untouched, so a breached wall vents on the very next step.
  - Cells that become impermeable have their gas pushed into a permeable neighbour, so nothing silently disappears; or it is lost if there is no such neighbour.
- **Grids attached or detached** (ships docking, sections breaking off):
  - The compiled grid reports `IsStale`.
  - The next `Advance` commits, disposes and recompiles from the assembly, then re-binds and rebuilds before stepping.
  - Gas therefore carries across via the cells themselves, with no coordinate remapping inside the simulation.
- **Rule for callers:** write to cells, attach grids and save only between `Advance` calls. `Advance` never runs while the caller is editing.

### 5.3 Frame model (changes between frames)

The simulation is never modified while `Advance` runs. Collecting changes during a frame (pumps, filters, heaters, doors) belongs to **another system outside the simulation**. That system applies them to the simulation before (or after) each `Advance`. The frame is therefore:

1. **Apply changes** (the caller, between frames):
   - The calls are `AddGas`, `RemoveGas`, `AddEnergy`, `AddCondensate`, `RemoveCondensate`, `InvalidateTopology`, plus cell edits and grid attach/detach on the assembly.
   - Each call writes the value directly and marks the chunk active and dirty (an O(1) write).
   - **No derived value is recalculated.** Pressure, flows, wind and connectivity are left as they are until the next `Advance`.
   - `RemoveGas` and `RemoveCondensate` return the amount actually removed, clamped to what the cell holds at that moment.
2. **`Advance`:**
   1. Topology: recompile if stale, and rebuild connectivity if invalidated or cells changed.
   2. Run the fixed steps for the elapsed time (§5.2).
   3. Publish pressure, flows and wind.

Notes:

- **No internal queue, tickets or threading.** Ordering, fairness between filters on the same cell, and deferring changes are the external system's job.
- **Stale derived layers.** Between frames, `Pressure`, flows and wind describe the last `Advance`, not the changes applied since. Gas and temperature layers reflect the changes immediately.
- **Amounts, not rates.** A pump applies the gas it delivered for the frame, which is its rate × elapsed game time. At 16× speed it simply applies 16× more. All of it is applied before the first fixed step, and the pipes spread it out within a few steps.
- **Layers are exposed as plain `IGridLayer<float>` (Q13 resolved).** To keep the API simple there is no read-only type. By convention, callers treat the layers as read-only and make every change through the methods above, so the tracking of active chunks (idle skipping) and dirty chunks (write-back) stays correct. Writing to a layer directly is unsupported, and is not detected.

### 5.2 Variable game speed

`Advance( TimeSpan elapsed )` is called once per frame with the game time that has passed, so 1×, 2×, 4×, 8× and 16× speed just pass in larger values.

- **Fixed internal step.**
  - The simulation keeps a time accumulator and runs `floor( accumulated / FixedTimeStep )` fixed steps, carrying the remainder to the next call.
  - A fixed step keeps the pipe model stable and deterministic: the same game time gives the same result at any game speed or frame rate.
- **Step cap.**
  - `MaxStepsPerAdvance` limits how many steps run in one call, to avoid a "spiral of death" (each slow frame demanding even more steps the next frame).
  - Time beyond the cap is **dropped**, not deferred, so the atmosphere briefly runs slower than game time rather than freezing the frame.
  - Default: enough steps for 16× at 30 fps.
- **Cost at 16×.** With a 0.1 s step at 60 fps, 16× speed is about 2–3 steps per frame. At the estimated ~60–100 µs per step on 100×100 with 3 gases, that is well under 1 ms per frame.
- **Larger steps are not used** for higher speeds. A bigger `dt` would make the pipes unstable; stepping more often is both safer and still cheap.
- **Timing source.** `IAtmosphericsSettings.FixedTimeStep`, in seconds, supplies the step length. Pausing is just calling `Advance( TimeSpan.Zero )`, or not calling it at all.

---

## 6. Expected behaviours (become tests)

1. **Quiescent.** A uniform-pressure sealed room stays unchanged (no drift, no noise).
2. **Conservation.** In a sealed room the total of each gas stays constant over many steps (within float tolerance).
3. **Equalisation.**
   - Two sealed rooms joined by an opened door converge to equal pressure.
   - Composition mixes toward the volume-weighted average.
4. **Pump fill.** A pump in an empty room raises the pressure everywhere in the room. The room eventually reaches uniform pressure, and the total matches the gas pumped in.
   - **Cold injection.** Adding gas below the cell temperature lowers the cell temperature to the weighted mix. Adding enough cold N2 to drop a cell below O2's condensation point makes O2 condense over the following steps, and gas plus condensate is conserved.
5. **Filter.** A scrubber removing CO2 lowers the room's CO2 fraction over time. O2 and N2 totals are unchanged.
6. **Walls.**
   - No gas crosses closed faces.
   - Adjacent rooms at different pressures hold that difference indefinitely.
7. **Vacuum.**
   - A room with an opening to space loses all its gas.
   - Venting is fast while the pressure difference is high (decompression), and the vented total equals the gas lost.
8. **Non-negative.** Amounts never go below 0, even with large `k` or a large breach.
9. **Stability.**
   - Random rooms, pumps and breaches over 10k steps stay finite and bounded.
   - Sloshing decays.
10. **Chunk and seam correctness.** Results are identical across chunk sizes 8 and 16, and across a multi-placement (multi-leaf) assembly.
11. **Gas count.** The simulation works with 1, 3 and 8 gases. Adding a gas doesn't change existing gases' results.
12. **Write-back round trip.**
    - After `Commit`, the cells hold the simulation's amounts.
    - Creating a new atmosphere from those cells gives identical amounts.
13. **Time-scale equivalence.**
    - One `Advance( 4·dt )` gives exactly the same result as four `Advance( dt )` calls.
    - Leftover time carries over to the next call.
    - The step cap is respected.
14. **Topology change preserves gas.**
    - Total gas is unchanged by: `InvalidateTopology( area )` with a door opened, adding a cell, or attaching a grid (which recompiles).
    - A local `InvalidateTopology( area )` produces exactly the same connectivity and results as a full rebuild.
    - The only exception is gas that is vented.
15. **Wind.**
    - Wind is zero in a settled room.
    - Next to a breach, wind points toward the vacuum face and is strongest at the start of decompression, then decays as the room empties.
    - The same flow carrying more gas gives a larger wind.

16. **Deferred calculation.**
    - Applying changes between frames doesn't change pressure, flows or wind until `Advance`.
    - Changing an idle chunk wakes it for the next `Advance`.

Plus a **benchmark** (100×100, 16-cell chunks, 3 gases, pump + breach scenario)

---

## 7. Phases

1. **Core model.**
   - Settings, neighbourhood, state.
   - Pressure, pipe, limiter and transfer kernels (scalar first).
   - Tests 1–8.
2. **Vectorize** the kernels, plus a benchmark.
3. **Sources and sinks.** `AddGas`/`RemoveGas` plus the batched source span, and tests for pumps and filters.
4. **Temperature** (§3.7): the temperature layer, pressure scaling, mixing on transfer, conduction, `AddEnergy`. Tests: heating a sealed room raises its pressure; mixing hot and cold rooms gives the weighted temperature; the same energy raises thin air's temperature more than dense air's.
5. **Phase change** (§3.8): condensate layers and the condense/evaporate pass. Tests: cooling below the condensation point removes that gas and lowers pressure; rewarming restores it; the total of gas plus condensate is conserved.
6. **Visualizer** scenario: multi-room ship, pump, scrubber, heater, cold room, breach.
7. **Optimisations:** tune idle-chunk thresholds against the benchmark. The skipping itself is built in phase 1, and phase 2 adds tests that idle chunks wake correctly.
8. **Remove or replace the placeholder types:**
   - `Gas` (replaced by the gas registry)
   - `CellAtmosphere`
   - the 8-direction `Directions` table
   - the old `IGridAtmospherics<TCell>`

---

## 8. Changes to existing LowFidelity files

None of these files need to be preserved. The table below is a suggestion only; any of them may be replaced, changed or deleted freely.

| File | Proposal |
|---|---|
Replaced by `GasDefinition`, `IGasRegistry`, `GasRegistryBuilder`, `IGasDefinitionSource` and `GasIndex`. The base game supplies O2, N2 and CO2 through its own source.
| `CellAtmosphere.cs` | Not used for storage. Optionally keep as a read-only snapshot for UI (`Pressure`, `float[] Fractions`). |
| `Vector.cs` | Keep. It is the return type of `GetWind` (§3.9). |
| `GridAtmospherics.cs` | Rewrite per §5. The 8-direction, y-up `Directions` table doesn't match Topology's 4-connected, y-down `Direction`. |
| `IGridAtmospherics.cs` | Replace per §5. The current `Update( IGridLayer<TCell> )` hides where the gas amounts live. |

---

## 9. Open questions for review

- ~~**Q1 — Scratch ownership.**~~ **Resolved:** the simulation is given the ship's grids, compiles its own grid, and owns all of its layers.
- ~~**Q2 — Partial permeability.**~~ **Resolved:** not needed. Permeability is a yes/no attribute read from the caller's cells.
- ~~**Q3 — Gas identity.**~~ **Resolved:** the `Gas` enum is replaced by `GasDefinition` records registered at initialisation, which supports mods.
  - ~~**Q3b**~~ **Resolved:** no. All gases are treated the same.
- ~~**Q9 — Gas across topology changes.**~~ **Resolved:** cells persist the gas amounts. The simulation writes them back between `Advance` calls using the compiled grid's bound layers and `Commit`, and recompiles when the assembly is stale. `InvalidateTopology()` signals permeability changes. See §5.1.
- **New: variable game speed.** `Advance( TimeSpan elapsed )` with a fixed internal step and a step cap. See §5.2.
- ~~**Q4 — Cell volume.**~~ **Resolved:** all cells have the same volume, and there is no capacity per cell.
- ~~**Q5 — Blowout data.**~~ **Resolved:** the pressure layer is enough. A cell pushes equally on all of its sides, and there is no `MaxWallDelta` helper.
- ~~**Q6 — Idle chunk skipping.**~~ **Resolved:** empty and settled chunks are skipped, and this is built in from phase 1 (§4).
- ~~**Q7 — Temperature.**~~ **Resolved:** yes. There is a temperature layer, and pressure scales with temperature (§3.7). Phase change is added in a naive form (§3.8).
- ~~**Q8 — Time step and units.**~~ **Resolved:** time is in seconds, pressure in kPa, temperature in kelvin, and amounts in "kPa at 20 °C" (§3.1).
  - ~~**Q8b — Step length.**~~ **Resolved:** the internal step is 0.1 s by default and stays a setting. It can be raised if testing shows the simulation is still stable with longer steps.
- ~~**Q10 — Heat capacity per gas.**~~ **Resolved:** no. Temperature is simple and per cell.
- ~~**Q11 — Latent heat.**~~ **Resolved:** not modelled. Phase transitions are simple temperature thresholds. Latent heat would be cheap to add, but it couples temperature and phase change (a new tuning surface and possible oscillation), so it is deferred.
- ~~**Q12 — Condensate behaviour.**~~ **Resolved:** the player can collect condensate and put it back down with `RemoveCondensate` and `AddCondensate`, and it evaporates when warmed (§3.8).
- ~~**Q13 — Read-only layer type.**~~ **Resolved:** layers are exposed as mutable `IGridLayer<float>`, and callers are trusted not to write to them.
