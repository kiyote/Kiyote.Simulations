# Low-Fidelity Atmospherics — Development Notes

Namespace: `Kiyote.Simulations.LowFidelity.Atmospherics`

## Scope

Simulate the atmosphere inside a spaceship well enough to *look and feel* right, as cheaply as possible.

**In scope**

- Per-cell amounts of each gas. Gases are defined at start-up from `GasDefinition` records, so mods can add them.
- Per-cell pressure, composition, temperature, condensate and wind.
- Flow from high to low pressure through permeable cells, with momentum, so a hull breach reads as *explosive* decompression.
- Gas reaching space (off-grid / vacuum) is lost and counted in `Vented`.
- Hooks for external systems between `Update` calls: `AddGas`, `RemoveGas`, `AddEnergy`, `AddCondensate`, `RemoveCondensate`.

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
- External heat exchange should limit each exchange so object and air don't overshoot at high game speeds (one `Update` can run many steps).

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

- The atmosphere borrows the caller's `ICompiledGridAssembly<TCell>` with 32-cell chunks and owns only the layers it registers. The combined simulation shares that same compilation with the structure thermal simulation and exposes lifecycle-free child state views.
- Gas, temperature and condensate layers are **bound** to the cells through `TStrategy`. The caller invokes `compiled.Commit()` to write dirty chunks back into the cells before saving or edits that need up-to-date cell values. Update, invalidation and disposal never implicitly commit.
- Flows are not persisted; they rebuild within a few steps. Invalidation cancels momentum on blocked faces but preserves it on unchanged open faces.
- After every successful live occupancy edit or source permeability change, call `InvalidateTopology( area )` with all changed cells in assembly coordinates, or `InvalidateTopology()` to rebuild everything. Empty areas do nothing; a full invalidation supersedes queued areas. Large areas fall back to a full connectivity rebuild. Vacuum and permeability snapshots currently rebuild fully; local connectivity refresh includes the one-cell ring, diagonals and placement seams.
- Individual cell additions/removals update the live compilation; invalidate the affected topology before updating. Attach/detach makes the compilation stale: commit first, dispose simulations and then the compilation, edit, compile and recreate. A stale Update throws rather than attempting a rescue commit. Reapply runtime options and switch readers to the replacement frame provider.
- Dispose a simulation before its borrowed compilation. Simulation disposal removes only its own layers, leaves other simulations and caller layers intact, and is idempotent. Disposing the compilation early is a caller error, not a recovery path.
- All caller changes happen **between** `Update` calls; they are stored and nothing is recalculated until the next `Update`.
- Metadata invalidation wakes affected chunks and marks derived pressure/wind for publication, including on `Update(TimeSpan.Zero)`. It does not rebind physical quantities. Layout version cannot detect occupancy edits inside existing chunks. Frame topology follows the same explicit caller invalidation contract, not an automatic discovery guarantee.
- Replacement metadata is staged before old registrations are removed. Failed extraction cleans temporary layers and leaves refresh work pending for explicit retry. Partial connectivity recalculation may have occurred; no physics step runs in that atmosphere update until preparation succeeds.

## Time stepping

`Update( elapsed )` accumulates game time and runs fixed steps of `FixedTimeStep` seconds, returning how many ran. At most `MaxStepsPerUpdate` run per call; any excess time is dropped, so a stalled frame slows the simulation rather than spiralling.

This loop is standalone-only. The combined coordinator owns one `TimeSpan`-tick
accumulator, captures the shared clock step and smaller child cap, and calls the
internal prepare/step/publish phases directly. Each shared tick steps atmosphere
and structure before any due convection; derived state and frames are finalized
once after all ticks/exchanges. Diagnostics reset once per combined update, not
per internal tick. Preparation completes before consuming time, including when
elapsed time is zero. No intermediate frame is published before convection.

## Implementation notes

- **Layout:** the compiled assembly's read-only `GridLayout` (Kiyote.Geometry) with 32×32 chunks, the smallest size `GridCompiler` accepts and a whole number of `Vector256` lanes for byte-sized layers (`Direction`, `bool`). Each quantity is one `IGridLayer<T>` over that layout (structure of arrays), with chunk values accessed directly through `GetValues`. A chunk index and local index identify the same cell in every layer. Occupancy changes go through the assembly, not the layout.
- **Stages are span kernels.** Each stage (`IAtmospherePressure`, `IAtmosphereAcceleration`, `IAtmosphereLimiter`, `IAtmosphereTransport`, `IAtmosphereThermal`, `IAtmosphereCondensation`, `IAtmosphereWind`) works on one chunk and takes only spans:
  - *chunk spans* (`ChunkSize²`, row-major) from `IGridLayer<T>.GetValues( chunk )` for the cell's own values;
  - *padded spans* (`( ChunkSize + 2 )²`) for anything read at a neighbour. `Atmosphere` fills them with `AtmosphereSimd.Gather`, a one-cell-halo equivalent of `IGridLayer<T>.CopyPadded( chunk, 1, ... )`, which writes 0 where the neighbouring chunk does not exist. `AtmosphereSimd.ChunkIndex` / `PaddedIndex` map a local cell to each shape.
  Only the orchestrator knows about layers and chunks, so stages are unit tested with plain arrays.
- **Step order:** sum gas and derive pressure → accelerate flows → compute limiter scale → limit → prepare and transport each gas (into `_gasNext`), mix temperature (into `_temperatureNext`), condense → copy back. Every phase finishes for all working chunks before the next starts, so border reads always see the neighbouring chunk's values from the same phase.
- **Idle chunks are skipped.** A chunk is active when something in it or next to it can change; caller edits and topology changes wake it. Because only the work set is advanced, `_gasNext`/`_temperatureNext` are copied back per chunk rather than swapped.
- **Vectorization:** full rows (all 32 cells present) are processed 8 cells at a time with `Vector256`; partial rows (next to walls or edges) use a scalar loop over the presence bits. Both paths must produce identical results.
- **Publish:** pressure and wind are recomputed only for chunks touched since the last publish, once per `Update`, then every published quantity is copied into its frame (see below).
- **Allocation:** steady-state stepping and publishing allocate nothing; scratch spans are owned by the atmosphere instance.

## Reading the simulation from a rendering thread

The simulation runs on one thread (the *simulation thread*), which calls `Update`, `AddGas`, `RemoveGas` and the other mutating methods.
A second thread (the *render thread*) can safely visualize the simulation at the same time by reading **frames** through `IAtmosphere.Frames`.

### The rule

The render thread must **never** touch the live layers on `IAtmosphere` (`Pressure`, `Temperature`, `WindX`, `WindY`, `GetGas`, `GetCondensate`, `GetWind`, ...).
The simulation thread rewrites those layers while it advances. The render thread may only use the `IAtmosphereFrames` returned by `IAtmosphere.Frames` and the leases it hands out.

### Usage

```csharp
// Render thread
IAtmosphereFrames frames = atmosphere.Frames;
using( TripleBufferedLease<float> pressure = frames.AcquirePressure() ) {
  foreach( GridChunk<float> chunk in pressure.EnumerateChunks() ) {
    for( int row = 0; row < chunk.Size; row++ ) {
      Span<float> values = chunk.GetRowValues( row );
      for( int column = 0; column < chunk.Size; column++ ) {
        if( chunk.IsPopulated( column, row ) ) {
          Draw( chunk.Column + column, chunk.Row + row, values[column] );
        }
      }
    }
  }
}
// or, for single cells: pressure.TryGet( column, row, out float value )
```

### What the frames contain

Each published quantity is its own `TripleBufferedChunkedGrid<float>` (Kiyote.Buffers) with the same chunk size and populated cells as the simulation:

| Member | Meaning |
|---|---|
| `StepCount` | Total fixed steps run when the most recent frames were published. |
| `Vented` | Gas lost to space during the `Update` that published the most recent frames. |
| `Gases` | The gas registry, for names and indices. |
| `AcquirePressure`, `AcquireTemperature`, `AcquireWindX`, `AcquireWindY` | Lease the latest published values. |
| `AcquireGas( gas )` | Lease the latest published amount of a gas. |
| `AcquireCondensate( gas )` | Lease the latest published condensate; throws `ArgumentException` for gases that do not condense. |

Unoccupied cells are not populated in a lease, so `TryGet` returns `false` for them.

### How it works

- At the end of every `Update` the simulation copies each quantity into the buffer of its triple-buffered grid that is neither the latest nor leased, then calls `Swap` to publish it. `Swap` and `Acquire` are lock-free, so neither thread ever blocks or waits on the other.
- While a lease is held the simulation never writes its buffer, so the data is stable until the lease is disposed.
- If the simulation advances several times between acquisitions, the reader sees only the latest values; intermediate frames are skipped.
- When the populated cells change (cells added or removed, or a recompile) the simulation publishes into a new set of grids and swaps the set in atomically. Leases on the previous grids stay valid until disposed, so topology changes need no external synchronization.

### Constraints

- **Single reader.** Only one thread may acquire leases.
- **One lease per quantity.** Dispose a lease before acquiring the same quantity again. Leases of different quantities can be held together.
- **Not coherent across quantities.** Each quantity is leased independently, so two leases may come from different `Update`s. Acquire everything you need back-to-back and compare `StepCount` before and after if exact coherence matters.
- **Read-only.** Leased buffers must never be modified, and a lease must not be used after it is disposed.
