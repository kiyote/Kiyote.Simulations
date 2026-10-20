# Kiyote.Simulations

Simulation kernels use `Kiyote.Geometry.Topology`: attach sources to a `GridAssembly<TCell>`,
compile it with `GridCompiler`, and bind or create `IGridLayer<T>` instances.
Layers share the compiled assembly's read-only `GridLayout` and expose chunk spans through
`GetValues` and padded neighbourhoods through `CopyPadded`. Change occupancy through the
assembly, and mark modified bound layers dirty before committing them to their sources.

The former scalar and `Vectorized` implementations based on `Kiyote.Geometry.Grids`
have been removed. Tests, benchmarks, and visualizers now target the Topology kernels.
