# Kiyote.Geometry Grid API Changes: Migration Notes for Kiyote.Simulations

This file describes the changes made to the grid API in `Kiyote.Geometry` and what
`Kiyote.Simulations` needs to change to use them. The goal is fast per-cell
simulation (diffusion, pressure, projection, advection, airflow) over grids that
can be attached and detached at runtime, for example a ship docking with a
station, with irregular shapes described by `ConnectivityGrid`.

## Prerequisites (do these first)

1. **Update the NuGet package.** Kiyote.Simulations uses `Kiyote.Geometry` as a
   NuGet package, not a project reference. Bump the `Kiyote.Geometry` package
   reference in every Simulations project that uses it (library, tests,
   benchmarks, visualizer) to the newly published version that contains
   `IGrid<T>.Version`, `IDenseGrid<T>` and `IConnectivityGrid<TCell>.BuildTopology()`.
   Restore and confirm the new types resolve before starting any other work.
2. **Fix the build break.** `IGrid<T>.Version` is a new required interface member,
   so every `IGrid<T>` implementation in Simulations will fail to compile until it
   is added (see section 1 below). Get the solution building and all existing
   tests passing again before changing any simulation behaviour.
3. **Read the current simulation code** (diffusion, pressure, projection,
   advection, airflow, the `*ConnectivityStrategy` classes and the visualizers)
   before applying sections A–H. The code in those sections is a generic
   template and must be adapted to the existing buffer, boundary and parallel
   handling. Ask before making design choices this document leaves open,
   such as lazy versus explicit rebind, or how seams take part in Jacobi iterations.
4. **Plan how visualizers map indices back to 2D.** Visualizers and other code
   that expect one rectangular `IGrid<float>` need to map combined indices back to
   2D using `TopologyLeaf.Column`/`Row`/`Width` (global cell =
   `(leaf.Column + i % leaf.Width, leaf.Row + i / leaf.Width)` for local index `i`),
   or keep reading through the existing `IGrid<T>` indexer, which is fine for
   rendering.

## Summary of Geometry changes

### 1. `IGrid<T>.Version` (breaking)
```csharp
int Version { get; }
```
- Leaf grids (`FlatArrayGrid<T>`, `RaggedArrayGrid<T>`) always return `0`.
- `CompositeGrid<T>` advances its version by exactly one on every successful
  `TryAttach`/`TryDetach`. It also includes the versions of nested grids, so a
  change anywhere in the hierarchy shows up at the root.
- `ConnectivityGrid<TCell>` also advances its version whenever `UpdateConnectivity`
  returns `true`.
- Only compare versions for equality. Don't compare them by magnitude.

**Required:** every `IGrid<T>` implementation in this repo must add `Version`.
Known candidate: `test/.../Kiyote.Simulations.Visualizer/BufferGrid.cs`. Search for
`: IGrid<` and `: IMutableGrid<` to find the rest. Leaf implementations should return `0`.

### 2. `IDenseGrid<T>` (new, opt-in)
```csharp
public interface IDenseGrid<T> : IMutableGrid<T> {
  Span<T?> Cells { get; }   // row-major
  int Stride { get; }       // elements per row
}
```
- `FlatArrayGrid<T>` implements it. For value types, `T?` is just `T`, so
  `FlatArrayGrid<float>` gives a plain `Span<float>`.
- Local cell `(c, r)` is at `Cells[r * Stride + c]`.

### 3. `ConnectivityGrid` cache is now dense
The per-source `Direction` cache is now a `FlatArrayGrid<Direction>` instead of a
`RaggedArrayGrid<Direction>`. The public behaviour is unchanged.

### 4. `IConnectivityGrid<TCell>.BuildTopology()` (new)
```csharp
GridTopology<TCell> BuildTopology();
```
It returns an immutable snapshot:

| Member | Meaning |
|---|---|
| `int Version` | The connectivity grid's `Version` at build time. |
| `int CellCount` | The total number of cells across all leaves (sum of `Width * Height`). |
| `ReadOnlySpan<TopologyLeaf<TCell>> Leaves` | One entry per attached data grid: `Grid`, `Column`, `Row`, `Width`, `Height`, `Offset`, `Length`. |
| `ReadOnlySpan<Direction> Cells` | The connectivity flags for every cell, indexed by **combined index**. |
| `ReadOnlySpan<Direction> GetConnectivity(int leaf)` | That leaf's slice of `Cells`, row-major, stride = `leaf.Width`. |
| `ReadOnlySpan<SeamLink> Seams` | **Directed** cross-leaf connections: `(Index, NeighbourIndex, Direction)`. A symmetric connection appears twice, once from each side. |
| `bool TryGetIndex(column, row, out index)` | Converts a connectivity-space coordinate to a combined index. This is slow, so use it for setup and tools only. |

**Combined index** = `leaf.Offset + localRow * leaf.Width + localColumn`.
Connections within a leaf are encoded only in `Cells`. Connections that cross a
leaf boundary appear only in `Seams`. Within-leaf neighbours are not duplicated
in `Seams`.

## Changes to make in Kiyote.Simulations

### A. Store simulation fields in combined buffers
Replace per-cell `IGrid<float>` / `IMutableGrid<float>` indexer access in the hot
loops with flat `float[]` buffers of length `topology.CellCount`, one per field
(pressure, velocity X/Y, density, divergence, scratch and so on). Get each leaf's
slice with:
```csharp
Span<float> p = pressure.AsSpan( leaf.Offset, leaf.Length );
```
Keep `IGrid<T>` for setup, input, visualization and occasional queries only.

### B. Rebuild on version change
```csharp
if( _topology is null || _topology.Version != _connectivity.Version ) {
  GridTopology<TCell> next = _connectivity.BuildTopology();
  RemapFields( _topology, next );   // see C
  _topology = next;
}
```
If the caller always knows when attach, detach or `UpdateConnectivity` happens,
it can call an explicit `Rebind()` instead. The version check is then an optional
safety net or debug assertion.

### C. Remap field data across rebuilds
When the topology changes, allocate new buffers of `next.CellCount`. For each
leaf in `next` whose `Grid` also appears in the previous topology (compare by
reference), copy the old slice into the new slice. Leaf sizes don't change
between rebuilds, so a straight `CopyTo` works. Initialise new leaves from
their data grids or to defaults. Data for detached leaves can be written back
to their own storage if the undocked ship needs to keep its state.

### D. Rewrite inner loops as row/column span loops
For each of diffusion, pressure (Jacobi/Gauss-Seidel), projection and advection:
```csharp
for( int l = 0; l < topology.Leaves.Length; l++ ) {
  TopologyLeaf<TCell> leaf = topology.Leaves[l];
  ReadOnlySpan<Direction> conn = topology.GetConnectivity( l );
  ReadOnlySpan<float> src = field.AsSpan( leaf.Offset, leaf.Length );
  Span<float> dst = scratch.AsSpan( leaf.Offset, leaf.Length );
  int w = leaf.Width;
  for( int r = 0; r < leaf.Height; r++ ) {
    int rowStart = r * w;
    for( int c = 0; c < w; c++ ) {
      int i = rowStart + c;
      Direction d = conn[i];
      if( d == Direction.None ) { dst[i] = src[i]; continue; }
      // Within-leaf neighbours: use flags plus bounds (c > 0, c < w - 1, r > 0, r < Height - 1).
      // Flags pointing out of the leaf are handled by the seam pass, not here.
    }
  }
}
```
- Treat `Direction.None` or unset flags as walls or solid cells. Use the same
  boundary semantics as the current `*ConnectivityStrategy` implementations.
- Diagonal flags (`NorthEast` and so on) are available if a stencil needs them.

### E. Add a seam pass after each leaf pass
```csharp
foreach( SeamLink s in topology.Seams ) {
  // One-sided contribution into s.Index from s.NeighbourIndex.
  // This conserves quantity automatically because the reverse link also exists.
  scratch[s.Index] += rate * ( field[s.NeighbourIndex] - field[s.Index] );
}
```
- For Jacobi-style solvers, fold seam neighbours into the same neighbour sum and
  count used by the interior stencil instead of doing a separate correction.
  The simplest approach is to accumulate the seam sum and count into small
  per-cell scratch arrays before the leaf pass, or to run the seam pass first.
- Keep this pass scalar. Seams are few compared with interior cells.

### F. Optional SIMD (after correctness)
With data in contiguous `float` spans:
- Precompute per-direction weight arrays (`0f`/`1f`) from `conn` at rebuild time,
  so the interior stencil has no branches.
- Vectorize interior rows using `Vector<float>` / `Vector256<float>` or
  `System.Numerics.Tensors.TensorPrimitives`.
- Don't use `float?` for field storage. It doubles memory and prevents SIMD.

### G. API naming
Keep the caller-facing method that advances each simulation named `Update`
(`IGridDiffusion`, `IGridPressure` and so on), per repo convention. A topology or
buffer rebind can be a separate method (e.g. `Rebind`) or happen lazily inside
`Update` using the version check from B.

### H. Tests and benchmarks
- Update the test and visualizer `IGrid<T>` implementations for `Version` (see 1).
- Add integration tests for: two leaves side by side with a seam (the quantity
  diffuses across the seam and the total is conserved), attaching mid-run
  (fields remap and are preserved), and detaching (the remaining leaf keeps
  its values).
- Re-run `GridDiffusionBenchmarks`, `GridPressureBenchmarks` and
  `GridProjectionBenchmarks` before and after the change, and record the results
  in `BENCHMARK.md`.

## Not yet provided by Geometry (possible follow-ups)
- Incremental topology rebuild limited to a region. Currently `BuildTopology`
  rebuilds everything, which is fine because it only happens on attach, detach
  or connectivity change.
- Cell-level (mask-aware) overlap checking in `CompositeGrid.TryAttach`.
  Overlap is still tested against bounding boxes.
