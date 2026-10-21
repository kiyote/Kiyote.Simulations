# Kiyote.Simulations.LowFidelity

## Compilation ownership

Compile the assembly once with 32-cell chunks, then pass that compilation to
`IGridAtmospherics.Create`, `IGridThermals.Create`, or
`IGridAtmosphericThermals.Create`. The factories do not require an `IGridCompiler`
service and never create or dispose a compilation themselves.

```csharp
using ICompiledGridAssembly<ShipCell> compiled = compiler.Compile( assembly, 32 );
using IAtmosphericThermals simulation = simulations.Create(
    compiled, atmosphereStrategy, thermalStrategy );

simulation.Atmosphere.AddGas( column, row, gas, amount );
simulation.Thermal.SunDirection = sunDirection;
simulation.Update( elapsed );
compiled.Commit();
```

The combined simulation uses the same compilation for both subsystems and
convection metadata. Its `IAtmosphereState` and `IThermalState` views expose
readouts and state edits, not independent stepping, invalidation, or disposal.
Standalone `IAtmosphere` and `IThermal` instances add those lifecycle operations.

Layer writes remain in the compilation until the caller invokes `compiled.Commit()`.
Updating, invalidating topology, or disposing a simulation does not implicitly
persist physical state. Dispose simulations before their compilation; simulation
disposal removes only its own registered layers. Removing a simulation-owned layer
externally or disposing its compilation early violates this lifetime contract and
is reported rather than silently ignored. Normal disposal is idempotent.

## Coupled time stepping and publication

The combined simulation captures one fixed step and the smaller of the atmosphere
and thermal `MaxStepsPerUpdate` settings at construction. Both caps must be
positive. The clock step must be finite, positive and representable as at least one
`TimeSpan` tick. Elapsed time must be nonnegative.

One coordinator accumulator counts elapsed `TimeSpan` ticks, avoiding floating
remainder drift when the same time is supplied in smaller fragments. Each due tick
advances atmosphere once, advances structure once, then performs any heat exchange
due at that tick. Both subsystems therefore advance the same simulated duration.
If the number of whole due ticks exceeds the shared cap, the coordinator runs the
cap and drops all excess time, including the fractional remainder. At exactly the
cap, the fractional remainder is retained.

`ExchangeInterval` keeps its fixed-tick quantization: divide by the clock step,
round to the nearest integer with midpoints to even, and clamp to at least one tick.
For a 0.1-second clock, 0.25 seconds means two ticks and 0.35 seconds means four.
The interval and quantized exchange duration must be finite, and the tick count
must fit an integer. `Update` still returns **exchanges**, not ticks.

Topology/material/solar preparation and convection-link rebuilding finish before
time is consumed or physics runs. Preparation failures propagate without advancing
either subsystem or publishing an intermediate frame. After all ticks and
exchanges, pressure/wind are finalized and all atmosphere quantities are published
once. Venting diagnostics cover the whole update. Zero-step updates still prepare,
finalize queued inputs and publish, but do not advance physics or exchange cadence.
Individual old leases stay unchanged; quantities remain independently leased,
not an atomic multi-quantity snapshot.

Standalone atmosphere and thermal instances retain their independent accumulators
and step caps. Public standalone factories and the combined factory use the same
typed internal engine construction and configured stage services; combined stepping
does not downcast public state views or call child elapsed-time updates.

### Convection storage and traversal

Convection descriptors use compiled chunk/local addresses, not world-coordinate
lookups. Rebuilding visits allocated chunks and their occupied presence bits,
skipping empty allocated chunks rather than scanning the occupied bounding
rectangle. Cells follow compiled chunk traversal and ascending local index; each
cell exchanges with its own floor first, then occupied impermeable cardinal
neighbours in north/east/south/west order. Holes, diagonal neighbours and permeable
neighbours are not wall faces. A shared wall can exchange with each adjacent gas
cell. Exchange is in-place and order-dependent, not attachment-order independent.

Each exchange reacquires temperature spans after the thermal step's storage swap
and sums current gas layers after transport/condensation. It transfers only the
energy actually applied to the gas, preserving temperature floors and the same
dirty/wake/publication bookkeeping as public energy inputs. Descriptors hold no
spans; relevant live topology/material edits still require explicit invalidation.

## Topology changes

For live cell additions/removals and relevant permeability/material changes, call
`InvalidateTopology(area)` or `InvalidateTopology()` before the next update.
Invalidate the combined coordinator when using the combined simulation.
If an edit needs current source-cell values, commit before making that edit.

### Live edits and metadata

- Invalidate after every successful `TryAddCell` or `TryRemoveCell`, including edits
  inside an existing chunk and removing the last occupied cell. These edits keep
  the compilation live; layout version is not an occupancy revision.
- Changes to an existing source cell's permeability or material also need
  invalidation. Bound property layers do not automatically re-extract those
  changes. Invalidation does not re-extract or commit gas, condensate or
  temperature; use the simulation's state-edit methods for physical inputs.
- Areas use **assembly coordinates**, not source-local coordinates. Include every
  changed cell; connectivity refresh includes its one-cell neighbour ring,
  diagonals and placement seams. Empty areas do nothing. A parameterless
  invalidation supersedes queued areas.
- Metadata refresh is deferred until the next `Update`, even if elapsed time is
  zero. A zero-step update refreshes pressure/wind and publishes edited occupancy
  without advancing physics. Pipe momentum on newly blocked neighbour faces is
  discarded; unchanged open pipes and physical cell values are retained.
- Connectivity can refresh locally. Vacuum and material/permeability properties
  currently rebuild fully, as do affected solar exposure and combined convection
  descriptors. Chunk-indexed work/publication tracking grows with the live
  compilation. Rebuild allocations are outside unchanged-topology steady state.
- The same explicit invalidation contract applies to frame topology. Do not rely
  on automatic discovery of edits or use layout version to decide whether to
  invalidate. Previously acquired leases remain unchanged.
- Frame publication caches integer source-chunk mappings and validates occupancy
  only on initialization and after nonempty/full invalidation. Property-only edits
  reuse matching grids. A replacement grid set and its mappings are installed only
  after all quantities copy successfully; failed publication preserves the previous
  frames/counters and retries pending validation. Every publication still copies
  every quantity in full, reacquiring current buffer views after swaps.

### Structural edits

Attaching/detaching a source is different: commit while the compilation is live,
dispose simulations and then their compilation, edit the assembly, and create a
new compilation and simulations. A stale simulation rejects updates; it never
attempts to commit or recover automatically.

Committed gas, condensate, and temperatures survive in the source cells.
Recreation resets transient flows, partial-step timing, exchange cadence, and
frame counters. Reapply runtime options, reacquire live layers, and switch readers
to the new frame provider. Previously acquired frame leases remain valid for their
existing lifetime.

### Structural edit sequence

For a caller holding mutable references to the active compilation/simulation pair:

```csharp
Vector sunDirection = simulation.Thermal.SunDirection;

compiled.Commit(); // If this throws, stop here and retry before disposing or editing.
simulation.Dispose();
compiled.Dispose();

if( !assembly.TryDetach( modulePlacement ) ) {
    throw new InvalidOperationException( "The module was not attached." );
}
// Attach additional sources here, checking each AttachResult.

compiled = compiler.Compile( assembly, 32 );
simulation = simulations.Create( compiled, atmosphereStrategy, thermalStrategy );
simulation.Thermal.SunDirection = sunDirection;
// Switch readers to simulation.Atmosphere.Frames and reacquire any live layers.
```

Retained sources keep their committed state. Detached sources also keep their
committed values and can be attached elsewhere. Newly attached cells initialize
from their own source values. Detaching the final source is supported: an empty
replacement simulation can still update and publish empty frames.

Cold recreation is intentional:

- Old pipe momentum is not copied. Wind starts at zero; pressure is recomputed from
  the rebound gas and temperature values, not carried over as a cached result.
- Fractional elapsed time and pending heat-exchange ticks are discarded. The new
  instance accumulates time from zero.
- Published step counts and per-update venting diagnostics restart. Old frame
  providers stop advancing; previously acquired leases remain unchanged.
- Runtime options such as sun direction use constructor defaults until reapplied.
  Old layer references remain detached snapshots, not aliases for new layers.

### Failure handling

A failed commit does not dispose the active simulation or authorize an edit.
Geometry leaves the failing binding dirty for explicit retry; earlier source
writes are not rolled back. Resolve the failure and commit successfully before
proceeding with structural changes.

If replacement construction fails, the edited assembly is not rolled back and
the disposed original simulation is not revived. Construction removes its
partially registered simulation layers. The replacement compilation remains
caller-owned: retry construction after correcting the problem, or dispose it.
Do not fall back silently to old layers or a partially initialized simulation.

Metadata extraction failures also propagate. Temporary replacement layers are
cleaned up, existing registrations remain owned, and failed refresh work stays
pending. Correct the cause and retry metadata refresh with `Update(TimeSpan.Zero)`;
do not silently proceed after a failed update. Successfully completed work is not
rolled back.
