# Low-Fidelity Atmospherics — Development Notes

Namespace: `Kiyote.Simulations.LowFidelity.Atmospherics`

## Scope

Simulate the atmosphere inside a spaceship well enough to *look and feel* right, as cheaply as possible.

**In scope**

- Per-cell amounts of each gas. Gases are defined at start-up from `GasDefinition` records, so mods can add them.
- Per-cell pressure, composition, temperature, condensate and wind.
- Flow from high to low pressure through permeable cells, with momentum, so a hull breach reads as *explosive* decompression.
- Gas reaching space (off-grid / vacuum) is lost and counted in `Vented`.
- Hooks for external systems between `Advance` calls: `AddGas`, `RemoveGas`, `AddEnergy`, `AddCondensate`, `RemoveCondensate`.

**Out of scope (owned by callers)**

- Pump, filter and heater behaviour. The simulation only exposes "add/remove gas or energy at a cell".
- Deciding that a wall fails and changing the topology. The caller reads `Pressure`, changes the cells, then calls `InvalidateTopology`.
- Moving objects in the wind; the simulation only reports wind.
- Heat exchange with anything other than air (objects, walls, the hull). A separate system reads `Temperature`/`GetTotalGas` and applies `AddEnergy`.
- Combustion, reactions, latent heat, pressure-dependent boiling points, and accurate fluid dynamics.

## Model

### State and units

The simulation stores the **amount of each gas per cell**, not pressure and percentages. This keeps it mass-conserving, makes mixing correct, and makes adding and removing gas trivial. Everything else is derived:

| Quantity | Definition |
|---|---|
| Amount of gas *g* in cell *c* | `A[g][c]` (stored, ≥ 0) |
| Pressure | `P[c] = Σg A[g][c] · T[c] / Tref` (kPa; every cell has the same volume) |
| Fraction of gas *g* | `A[g][c] / ΣA` (0 when empty) |

- **Amounts are "kPa at the reference temperature"** (`Tref` = 293.15 K). A breathable room-temperature cell holds about 21.2 O2 + 79.0 N2 + 0.04 CO2 = 101.3 kPa. This is moles times a constant, chosen because pressure, partial pressure and condensation are all in kPa.
- **Temperature is in kelvin**, so the gas law is one multiply. Heating a sealed room raises its pressure with no gas added.
- **Time is in seconds**; every rate in `IAtmosphericsSettings` is per second.

### Flow: virtual pipes

Every pair of 4-connected cells has a pipe across their shared face, with a stored flow. Each cell owns its **East** and **South** pipes (`_flowEast`, `_flowSouth`), so each pipe is stored and updated once. Each step:

1. **Accelerate:** `flow += k · ( P[a] − P[b] )`.
2. **Damp:** `flow *= ( 1 − friction )`.
3. **Limit:** scale outgoing flows so a cell never sends more than it holds; amounts stay ≥ 0.
4. **Transfer:** move gas along each pipe using the composition of the **upwind** cell.

Pipes are used rather than plain diffusion because they give momentum (violent decompression, gusts when doors open), propagate pressure changes many cells per step, provide the airflow field for free, and need no global solve.
Diagonals are skipped: they double the cost and pipes already spread isotropically enough.

### Walls and vacuum

- Permeability is a per-cell yes/no read from the caller's `TCell` via `IAtmosphereCellStrategy<TCell>.IsPermeable`. Connectivity is built from it; a face is open only when both cells are permeable.
- Closed faces force flow to 0. Impermeable cells hold no gas and are never simulated.
- Faces leading off the grid are vacuum: neighbour pressure is 0 and gas leaving through them is added to `Vented`.

### Temperature

- One temperature per cell; all gases share one heat capacity (`HeatCapacity`).
- **Heat moves with the gas:** the receiving cell becomes the amount-weighted mix. `AddGas` uses the same mixing with the incoming gas temperature (defaulting to the cell's).
- `AddEnergy` changes temperature by `joules / ( ΣA · HeatCapacity )`, ignores empty cells, clamps at 2.7 K, and returns the energy actually applied.
- Optional air-to-air conduction is controlled by `Conduction` (0 = off).
- External heat exchange should limit each exchange so object and air don't overshoot at high game speeds (one `Advance` can run many steps).

### Phase change

- A gas with a `CondensationPoint` gets a condensate layer. Below that temperature, gas moves to condensate; above it, condensate evaporates, both at `CondensationRate`, so changes are gradual at any game speed.
- Condensate does not flow. `FreezingPoint` is display-only.
- `AddCondensate`/`RemoveCondensate` let the caller scoop up or drop condensate (e.g. a bag of frozen O2 that boils off in a warm room).

### Wind

- `WindX`/`WindY` (and `GetWind`) are in kPa of dynamic pressure, +x east, +y south.
- Velocity is the average of the face flows on each axis divided by the cell's gas density; wind is `½ · ρ · |v| · v` scaled by `WindScale`.
- Callers convert to force with `W · 1000 · area (m²)`.

### Gas registry

- `GasRegistryBuilder` reads one or more `IGasDefinitionSource`s and builds an immutable `IGasRegistry`. Duplicate `Id`s throw.
- Indices are dense and deterministic for a given set of sources, but **never persist a `GasIndex`**; save the `Id`.
- The flow kernel never reads gas properties; only the phase-change pass does.

## Ownership, persistence and topology

- The atmosphere receives the ship's `IGridAssembly<TCell>`, compiles its own `ICompiledGridAssembly<TCell>`, and owns every layer it creates.
- Gas, temperature and condensate layers are **bound** to the cells through `TStrategy`. `Commit()` writes dirty chunks back into the cells; call it before saving or before edits that need up-to-date cell values.
- Flows are not persisted; they rebuild within a few steps.
- `InvalidateTopology( area )` rebuilds connectivity for an area (door, breach, new tile); `InvalidateTopology()` rebuilds everything. Large areas fall back to a full rebuild.
- If the compiled grid goes stale (cells added or removed), `Advance` commits, recompiles and rebinds automatically.
- All caller changes happen **between** `Advance` calls; they are stored and nothing is recalculated until the next `Advance`.

## Time stepping

`Advance( elapsed )` accumulates game time and runs fixed steps of `FixedTimeStep` seconds, returning how many ran. At most `MaxStepsPerAdvance` run per call; any excess time is dropped, so a stalled frame slows the simulation rather than spiralling.

## Implementation notes

- **Layout:** one `IGridLayer<float>` per gas (structure of arrays), 16×16 chunks with a 1-cell halo. Halos are exchanged once per pass.
transfer (gas-major, into `_gasNext`/`_temperatureNext`, with temperature mixing, conduction and phase change) → copy back.
- **Idle chunks are skipped.** A chunk is active when something in it or next to it can change; caller edits and topology changes wake it.
- **Vectorization:** full rows (all 16 cells valid) are processed 8 cells at a time with `Vector256`; partial rows (next to walls or edges) use a scalar loop over the validity mask. Both paths must produce identical results.
- **Publish:** pressure and wind are recomputed only for chunks touched since the last publish, once per `Advance`.
- **Allocation:** steady-state stepping and publishing allocate nothing.

## Reading the simulation from a rendering thread

The simulation runs on one thread (the *simulation thread*), which calls `Advance`, `AddGas`, `RemoveGas` and the other mutating methods.
A second thread (the *render thread*) can safely visualize the simulation at the same time by reading **frames**.

### The rule

The render thread must **never** touch the live layers on `IAtmosphere` (`Pressure`, `Temperature`, `WindX`, `WindY`, `GetGas`, `GetCondensate`, `GetWind`, ...).
The simulation thread rewrites those layers while it advances. The render thread may only use:

- `IAtmosphere.AcquireFrame()`
- `IAtmosphere.ReleaseFrame( frame )`
- the members of the `IAtmosphereFrame` it is holding

### Usage

```csharp
// Render thread
IAtmosphereFrame frame = atmosphere.AcquireFrame();
try {
  ReadOnlySpan<float> pressure = frame.Pressure;
  for( int row = 0; row < rows; row++ ) {
    for( int column = 0; column < columns; column++ ) {
      int index = frame.IndexOf( column, row );
      if( index < 0 ) {
        continue; // Off-grid / vacuum
      }
      Draw( column, row, pressure[index] );
    }
  }
} finally {
  atmosphere.ReleaseFrame( frame );
}
```

### What a frame contains

An `IAtmosphereFrame` is a read-only copy of the simulation as of the end of an `Advance` call:

| Member | Meaning |
|---|---|
| `StepCount` | Total fixed steps run when the frame was captured. Use it to detect whether the frame is new. |
| `Vented` | Gas lost to space during the `Advance` that produced the frame. |
| `Gases` | The gas registry, for names and indices. |
| `Pressure`, `Temperature`, `WindX`, `WindY` | Per-cell values. |
| `GetGas( gas )` | Per-cell amount of a gas. |
| `GetCondensate( gas )` | Per-cell condensate; throws `ArgumentException` for gases that do not condense. |
| `IndexOf( column, row )` | Index into any of the spans above, or `-1` for cells outside the grid. |

The spans are the raw chunked storage, including halo cells, so they are **not** row-major. Always index them with `IndexOf`. Every span in a frame uses the same indexing.

### How it works

The atmosphere keeps three frames in a lock-free triple buffer:

- **back**: owned by the simulation thread. At the end of every `Advance` the simulation copies its layers into this frame, then publishes it with `Interlocked.Exchange` into the shared slot (marked *fresh*) and takes the frame that was there as its new back frame.
- **shared**: the most recently published frame not yet taken by the reader.
- **front**: owned by the render thread. `AcquireFrame` swaps the front frame for the shared one (with `Interlocked.Exchange`) only when the shared frame is fresh; otherwise it hands back the same front frame again.

Consequences:

- Neither thread ever blocks or waits on the other.
- While the render thread holds a frame, the simulation never writes to it, so the data is stable for as long as it is held.
- If the simulation advances several times between acquisitions, the reader sees only the latest frame; intermediate frames are skipped.
- If the simulation has not advanced, `AcquireFrame` returns the same frame (same `StepCount`) as last time.
- Copies reuse their arrays, so publishing allocates nothing in the steady state. The copy happens once per `Advance`, not once per fixed step.

### Constraints

- **Single reader.** Only one thread may call `AcquireFrame`/`ReleaseFrame`.
- **One frame at a time.** Calling `AcquireFrame` while a frame is held throws `InvalidOperationException`. So does calling `ReleaseFrame` when nothing is held.
- **Do not keep references after release.** Once released, a frame may be overwritten by the simulation at any time. Copy any values you need beyond the release.
- **Read-only.** Frames must never be modified.
- **Topology and size changes are externally synchronized.** When the grid's content or size changes, the atmosphere recompiles and creates new frames. The caller must make sure the render thread is not holding a frame (and is not inside `AcquireFrame`/`ReleaseFrame`) while that happens. The atmosphere does not guard against it.
