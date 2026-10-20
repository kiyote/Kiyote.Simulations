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

Attaching/detaching a source is different: commit while the compilation is live,
dispose simulations and then their compilation, edit the assembly, and create a
new compilation and simulations. A stale simulation rejects updates; it never
attempts to commit or recover automatically.

Committed gas, condensate, and temperatures survive in the source cells.
Recreation resets transient flows, partial-step timing, exchange cadence, and
frame counters. Reapply runtime options, reacquire live layers, and switch readers
to the new frame provider. Previously acquired frame leases remain valid for their
existing lifetime.
