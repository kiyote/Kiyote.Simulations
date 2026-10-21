# Kiyote.Simulations

Simulation kernels use `Kiyote.Geometry.Topology`: attach sources to a `GridAssembly<TCell>`,
compile it with `GridCompiler`, and bind or create `IGridLayer<T>` instances.
Layers share the compiled assembly's read-only `GridLayout` and expose chunk spans through
`GetValues` and padded neighbourhoods through `CopyPadded`. Change occupancy through the
assembly, and mark modified bound layers dirty before committing them to their sources.

The former scalar and `Vectorized` implementations based on `Kiyote.Geometry.Grids`
have been removed. Tests, benchmarks, and visualizers now target the Topology kernels.

## Projection and airflow storage

Projection and composed airflow updates take the caller's compiled owner as their
first argument. All neighbourhood, physical and working layers must belong to that
same, unchanging compilation:

```csharp
projection.Update(
    compiled, neighbourhood,
    velocityX, velocityY, nextVelocityX, nextVelocityY,
    pressure, pressureScratch, divergence
);
```

Pressure is the solve's warm start and holds the final answer in the same layer
object on return. Odd Jacobi iteration counts exchange pressure/scratch storage
and dirty flags through `compiled.Swap`; even counts need no final storage swap.
Bindings stay with their registered layer objects, so committing the owner saves
the solved bound pressure. Scratch is working storage, not a second promised copy
of the final pressure. Reacquire chunk spans after each update; a previously held
span must not be treated as that layer's current storage after a swap.

Iterations must be nonnegative. Zero performs no relaxation and subtracts the
current warm-start pressure gradient; negative counts are rejected at construction.
The caller still swaps velocity/concentration source and destination layers after
an update and owns the compilation and all layers.

The swap removes an allocated-chunk copy on odd solves. It is especially useful
with retained empty chunks and few iterations, not a general dense/high-iteration
solver speedup. See the matched measurements in the
[experiment record](../../../../EXPERIMENT.md#owner-mediated-odd-projection-swap).
