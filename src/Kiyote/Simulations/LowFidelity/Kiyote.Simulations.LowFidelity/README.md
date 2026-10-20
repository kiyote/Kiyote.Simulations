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
