# Topology Correctness and Integration Plan

## Status and purpose

Implementation-ready plan, decisions resolved on 2026-10-20.
Production implementation has started, one validated and committed work package at
a time. The completed Geometry migration was validated with 208 passing tests and
committed as `d4a463c` before planned implementation began.
The approved decisions below govern the work. The dependency-publication gate is
an explicit future handoff, not an unresolved design choice.

Correct the simulation library's integration with Kiyote.Geometry 0.35.0, then
evaluate the remaining opportunities to use its public API more efficiently.
Correctness changes are required. Performance candidates must earn adoption through
measurements; retaining the current implementation is a valid experimental result.
The final review explicitly prioritizes correct ownership, coherent stepping, and
measured performance over preserving existing APIs or incidental numerical behavior.

## Established facts

- A compiled assembly copies source values into layers. Only `Commit` writes bound
  values back. Geometry documents at most one live compilation per assembly.
- Attach/detach invalidates a compilation. Stale compilations cannot commit,
  create/remove layers, or perform other owner operations. Commit before structural
  changes; never attempt a rescue commit afterward.
- Individual cell additions/removals update a live compilation and its layers.
  They do not make the compilation stale.
- Vacuum and connectivity layers are snapshots. Updating occupancy does not
  automatically refresh their flags for neighbouring cells.
- Chunk indices remain stable during a compilation. Layer spans are not safe to
  retain across swaps; source-run spans are invalidated by cell edits.
- Layout version changes on chunk allocation/removal, not every occupancy edit.
  It is not an occupancy revision.
- Owner-mediated swaps exchange storage and dirty flags while preserving the
  binding associated with each layer object.
- The last completed solution validation passed 208 tests with no build warnings
  or errors. Re-establish the baseline before implementation; do not treat this
  historical result as validation of future changes.

Contract reference:
[ICompiledGridAssembly.cs](../Kiyote.Geometry/src/Kiyote/Geometry/Kiyote.Geometry/Topology/ICompiledGridAssembly.cs).

## Approved decision register

| ID | Decision | Approved selection |
|----|----------|--------------------|
| D1 | Implementation scope | Correctness fixes and all benchmark-gated optimization experiments below |
| D2 | Compilation ownership and structural edits | Caller owns compilation; explicitly commit, dispose simulations/compilation, edit, compile, recreate |
| D3 | State continuity | Preserve committed cell values; reset transient flows, timing and frame counts; caller reapplies runtime options and switches frame provider |
| D4 | Dependency-repository changes | Focused Geometry and Buffers changes are allowed when correctness or measurements justify them |
| D5 | Projection owner access | Explicit generic compiled-owner parameter on projection/airflow updates |
| D6 | Minimum optional improvement | At least 10% on the intended representative workload, reproducible in matched A/B runs |
| D7 | Regression/allocation budget | At most 10% confirmed slowdown on other representative workloads; no new steady-state allocations |
| D8 | Dependency delivery | Prepare and test upstream changes; pause for the user to publish packages before the final Simulations dependency update |
| D9 | Compatibility policy | APIs and behavior may change when doing so improves correctness or measured performance; do not preserve legacy details at the expense of design |
| D10 | Combined stepping/publication | One coordinator-owned fixed-step accumulator; step both subsystems, perform due exchanges, then publish once after all ticks; use the smaller child step cap |

### Selected architecture

- All three public simulation factories accept an existing
  `ICompiledGridAssembly<TCell>` instead of compiling an `IGridAssembly<TCell>`.
  Remove the old assembly-taking creation paths rather than keeping two ownership
  models. The caller chooses the chunk size; retain the existing supported
  LowFidelity size of 32 and reject incompatible compiled layouts at creation.
- Atmosphere and thermal instances borrow that compilation and own only the layers
  they register. The combined instance creates its children against the same
  supplied compilation and owns those children, not the compilation. Temporary
  convection metadata layers must also be tracked and removed.
- The caller owns persistence. Remove `Commit` from `IAtmosphere`, `IThermal`, and
  `IAtmosphericThermals`; consumers commit the shared compilation explicitly.
- Dispose simulations before their compilation. No simulation disposes the caller's
  compilation or automatically recompiles after structural changes.
- Keep existing explicit `InvalidateTopology` methods for live per-cell/property
  edits, and make them authoritative for frame topology as well as physics metadata.
  Do not add automatic observation, a simulation session framework, an edit
  transaction, a suspend/rebind API, or a state snapshot/restore API.
- The retained projection-swap implementation uses a generic compiled-owner
  parameter on projection/airflow updates. Keep numerical kernels span-based and
  non-generic where they do not need the owner.
- Combined simulation owns elapsed-time accumulation and exchange scheduling.
  Its children expose narrow internal prepare/step/finalize hooks, not independent
  elapsed-time loops used by the coordinator. Do not build a generic pipeline framework.
- Expose lifecycle-free atmosphere/thermal views from the combined simulation.
  Proposed `IAtmosphereState` and `IThermalState` interfaces hold the existing
  readouts and state-edit operations. Standalone interfaces add Update, topology
  invalidation, and disposal; the combined view does not expose child Update,
  invalidation, or disposal. The same engines can implement these views without
  adding wrapper objects or dispatch in numerical kernels.

### Dependency delivery and publication gate

Geometry source is in `C:\code\Kiyote\Kiyote.Geometry`; Buffers source is in
`C:\code\Kiyote\Kiyote.Buffers`. Permission to make focused changes is not permission
to publish packages, push repositories, or rewrite their unrelated changes.

1. Prefer the existing published APIs. Dependency changes are conditional on a
   demonstrated need, not a mandatory refactor of both libraries.
2. Prepare focused upstream implementation, tests, documentation, and available
   upstream benchmarks. Validate there before asking for a package release.
3. Pause and give the user the exact changed packages, validated source state, and
   required API changes. The user publishes and supplies the resulting versions.
   Do not guess a version or pretend unpublished code is available to Simulations.
4. Update only the required central package versions after those packages exist.
   Restore, build, test, and perform the consumer-level A/B comparisons.
5. Adopt an optional consumer change only if it passes D6/D7. A successfully
   published helper is not itself evidence that using it improves this library.
6. Do not deliver temporary project references, persistent local package-feed
   configuration, or a dependency version that exists only on this machine.

## Correctness invariants and permitted behavior changes

- Enforce gas/heat conservation, declared vacuum/wall semantics, nonnegative
  physical quantities, fixed-step limits, and consistent units. Existing arithmetic
  order and exact old outputs are not specifications by themselves.
- Numerical/traversal changes must have a correctness rationale and explicit
  invariant tests. Do not hide a regression by broadly relaxing tolerances or
  relabeling an unexplained numerical difference as an improvement.
- All successful live occupancy edits and relevant permeability/material changes
  require caller invalidation before the next update. Failed edits need no
  invalidation. Gas/energy input APIs retain their own dirty/wake bookkeeping.
  Automatic discovery of unreported topology changes is not a supported contract.
- Frame topology follows that same explicit invalidation contract. Retire the old
  guarantee that a new cell is published without invalidation; update callers,
  tests, and documentation together. No per-update full scan is required just to
  accommodate callers that violate the contract.
- Preserve old frame leases while publishing replacements. Simulation mutation is
  single-threaded; the existing supported frame reader remains supported.
- `Dispose` does not implicitly commit. Disposal must be idempotent, and borrowed
  components must never dispose somebody else's compilation.
- Reject stale-compilation and disposed-simulation misuse explicitly at orchestration
  boundaries. The caller must keep the borrowed compilation live throughout the
  simulation's lifetime. Do not catch an error and silently recompile with missing state.
- Keep numerical kernels span-based. No topology/version checks, ownership
  abstractions, or new interface dispatch inside per-cell/SIMD loops.
- Breaking API changes are acceptable. Do not preserve obsolete overloads merely
  for compatibility or expose Geometry's internal storage.
- No new third-party dependencies or unrelated cleanup. D10 authorizes correcting
  coupled integration order, not adding new physics or speculative higher-order
  solvers. Other numerical changes require evidence, not compatibility pressure.
- Preserve the existing dirty worktree. Implementation must not overwrite unrelated
  user changes or commit anything unless separately requested.
- Keep [BENCHMARK.md](BENCHMARK.md) as class headings followed only by result tables.

## Work packages

### Implementation progress

- Baseline: migration, benchmark tables, and this plan committed as `d4a463c`;
  208 tests passed, solution build had no warnings/errors.
- W1: completed in `2994f0d`. Factories borrow the caller's compilation; persistence is
  explicit; children expose lifecycle-free state views; owned and temporary layers
  are cleaned up deterministically. Internal prepare/step/publication hooks are
  available without changing coupled timing yet.
- W1 validation: full solution build has no warnings/errors; all 248 tests pass;
  all 40 new ownership regressions also pass in Release. Warmed unchanged-topology
  updates allocate zero measured bytes for atmosphere, thermal, and combined
  simulations in both configurations. Ownership tracking allocates only during
  construction/rebuild, not steady-state stepping.
- W2: completed in `83bbaeb`. Added 26 structural-lifecycle cases covering retained/new/detached
  cells, empty replacements, detached layer references, transient flow/timing/
  exchange resets, frame-provider replacement and held leases, runtime option
  reapplication, and explicit commit/construction retries. Expanded the caller
  lifecycle and failure-handling documentation; no runtime machinery was needed.
- W2 validation: solution builds without warnings/errors; all 274 tests pass.
  All 66 ownership/lifecycle regressions also pass in Release, including the
  existing zero-allocation checks.
- W3: completed. Explicit invalidation covers live occupancy, permeability/material
  properties, derived-state publication, solar exposure, and convection links.
  Empty areas do nothing; a full invalidation supersedes queued areas. Replacements
  are staged before old metadata registrations are removed, with temporary cleanup
  and pending-work retention on failure. Affected neighbour rings wake and publish
  even without a physics tick; blocked pipe momentum is cancelled without clearing
  unchanged open pipes. Ring expansion clamps at signed coordinate limits.
- W3 validation: added 49 live-edit cases; all 323 solution tests pass and the build
  has no warnings/errors. All 115 ownership/lifecycle/invalidation cases pass in
  Release, including exact zero-allocation checks after successful chunk-growth
  invalidation for all three simulation kinds. Coverage includes existing/new
  chunks, retained empty chunks, held leases, negative/limit coordinates, diagonals,
  placement seams, all four thermal material properties, solar occlusion, convection
  membership/energy balance, caller-owned layers, and repeated failure retries.
- W3 retains complete vacuum/property rebuilds and the existing publication
  presence comparison. The explicit caller/publication invalidation contract is
  established; caching that comparison and frame-copy addresses remains the
  benchmark-gated W8 experiment. No upstream package change was required.
- W9: next required correctness package; coupled scheduling/final publication is
  unchanged so far. W4-W8 optional performance experiments have not started.

### W1. Establish one compilation owner and remove unsupported recompilation

Priority: required correctness. Implement the approved D2/D3 design.

Affected surfaces:
[Atmosphere.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Atmospherics/Atmosphere.cs),
[Thermal.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Thermals/Thermal.cs),
[AtmosphericThermals.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/AtmosphericThermals/AtmosphericThermals.cs),
their factory interfaces/implementations, DI registrations, and consumers.

1. Remove both stale-update paths that try to commit an already stale compilation.
2. Accept the caller's single compilation for the combined atmosphere/thermal
   simulation. Reuse that owner for both subsystems and convection metadata; remove
   the temporary link-builder compilation and all simulation-internal compilation.
3. Separate attaching/binding a subsystem's layers from creating the compilation.
   Track which layers each subsystem creates, including connectivity, vacuum, and
   scratch layers. Remove only those layers on normal borrowed-component disposal.
4. Put compilation disposal and whole-assembly persistence at the caller. Update
   all three factory interfaces/implementations to accept a compiled assembly and
   remove compiler dependencies that no longer serve those factories.
5. Remove all three public simulation `Commit` methods and update callers to commit
   their owner. Remove the implicit commits in atmosphere topology refresh and
   thermal structure rebuilding: these refresh read-only property bindings,
   connectivity, and vacuum, not the physical-state bindings. Their property
   binding commit callbacks are no-ops. A caller that needs current source-cell
   values before an edit explicitly commits first; invalidation itself does not
   persist unrelated gas/temperature changes.
6. Commit shared bound state once at explicit persistence/edit boundaries. Do not
   duplicate commits merely because the coordinator has two children.
7. Handle partial construction and repeated disposal without leaving registered
   layers or disposing the shared owner from a child. Cleanup of an already stale
   compilation leaves its detached snapshot layers to their owner. Geometry does
   not expose a public disposed-state query: disposing the caller's compilation
   before its borrowers violates the required lifetime order and must surface
   ObjectDisposedException, not be silently swallowed or require a new dependency
   API solely for out-of-contract cleanup.
8. Build convection descriptors with temporary property layers on the live owner,
   then remove those layers in deterministic cleanup, including failure paths.
   Repeated invalidation must not accumulate bound layers or stale descriptors.
9. Implement the lifecycle-free combined views and internal stepping hooks needed
   by W9. Keep public state mutation bookkeeping shared with internal energy edits.

Acceptance: one compilation per assembly, correct persistence, explicit errors,
and no new steady-state step allocations or ownership work in numerical kernels.

### W2. Define and implement structural-edit state transitions

Priority: required correctness. Depends on W1.

Required caller-owned sequence:

1. Finish the current update and explicitly commit the still-live compilation.
2. Dispose the simulations, then the compilation.
3. Attach/detach sources on the assembly.
4. Compile the edited assembly and recreate the simulations.
5. Reacquire exposed layers; layers retained from the old compilation are detached
   snapshots, not handles to the replacement.

If committing throws, do not proceed with disposal/editing. Surface the error and
allow the caller to retry while the original simulation/compilation are still live.
If replacement construction fails, clean up only the replacement's resources and
surface the failure; there is no automatic rollback of caller edits or silent
fallback to an old snapshot.

Required structural-edit state policy:

| State | Behavior after recreation |
|-------|---------------------------|
| Gas, condensate, atmosphere/structure temperature | Commit to original source cells before editing; retained sources retain their values, including sources subsequently detached |
| Newly attached cells | Initialize from their own source values through the supplied bindings |
| Pipe flows/momentum | Reset; no attempt to match or transfer old faces |
| Wind, pressure, totals | Reset/recompute through normal initialization and publication; never reuse old derived arrays |
| Scratch buffers and scheduling flags | Fresh storage and the normal initial wake/publication state |
| Fractional fixed-step accumulators | Reset; do not compensate by adding discarded partial time to the next update |
| Exchange cadence and published step counters | Restart for the new simulation instance |
| Per-update vented amount | Start fresh; do not carry an old update's diagnostic into the replacement |
| Sun direction and other runtime options | Caller captures/reapplies desired settings; preserve existing constructor defaults when not reapplied |
| Frame provider | New provider; caller switches readers to the new instance |
| Already acquired frame leases | Remain unchanged and valid through their existing lifetime; the old provider receives no further updates |

Document these resets as intentional behavior, not transparent automatic
recompilation. They apply to attach/detach recreation, not ordinary live cell edits.

### W3. Make topology-derived state and invalidation explicit

Priority: required integration coverage; retain correct rebuilds initially.
Depends on W1 and W2.

1. Distinguish structural edits from per-cell occupancy edits and property changes
   such as permeability/material changes. Callers invalidate after successful
   edits and before updating; combined callers invalidate the coordinator, not
   each child independently.
2. Keep connectivity, vacuum, permeability/material properties, solar exposure,
   convection links, occupied bounds, work scheduling, and publication topology
   consistent after the relevant invalidation.
3. Resize chunk-indexed tracking arrays after live cell edits allocate storage.
4. Retain complete vacuum/property rebuilds until a supported local refresh exists.
   Do not invent an internal-storage shortcut or assume a bound property layer
   automatically re-extracts arbitrary changes to existing source-cell properties.
5. Coalesce invalidations and use an explicit simulation-owned dirty flag or
   generation plus compilation identity for derived caches. This is authoritative
   under the required caller contract; no new Geometry occupancy API is needed
   merely to remove automatic polling. Never use layout version as an occupancy
   revision.
6. Correct the atmosphere development notes: cell edits do not stale a compilation,
   and committing after attach/detach is not a supported recovery path.

Local metadata refresh is optional. Under approved D4, add/reuse an
appropriately scoped public Geometry helper, test its one-cell affected ring and
diagonals, and compare it against a full rebuild. Geometry owns topology semantics;
the simulation owns deciding when its metadata needs refreshing. Follow D8 before
consuming an unpublished helper.

### W4. Build and consume convection links using sparse chunk storage

Priority: benchmark-gated optimization. Depends on W1 and W3.

1. Replace bounding-rectangle traversal with allocated-chunk/presence-bit iteration.
   Read permeability, conductance, and capacity spans once per relevant chunk.
2. Resolve neighbours through chunk/local addresses, handling negative coordinates,
   empty allocated chunks, and seams. Avoid world-coordinate lookups when the
   compiled indices already identify the cell.
3. Describe gas cells and structure faces with chunk index plus local index rather
   than column/row. These addresses are valid for the compilation; rebuild
   descriptors after relevant occupancy/permeability/material invalidation.
4. Use the compiled chunk traversal and ascending local presence bits as the
   documented order. Do not add an O(N log N) global sort merely to reproduce the
   old row-major exchange results. Keep floor and cardinal-face membership correct.
   In-place exchange is order-dependent; do not claim attachment-order independence.
5. Evaluate an internal chunk-addressed energy path so exchange does not repeatedly
   hash world coordinates or repeat public-operation setup. Preserve the actual
   energy-applied result, temperature floors, dirty flags, wake flags, and pending
   publication flags. Share those semantics with public energy input APIs.
6. Reacquire spans for each exchange phase after thermal swaps. Do not retain spans
   in descriptors or reuse a pre-transport gas-total scratch layer as current mass.
7. Keep rebuild allocations outside unchanged-topology steady state. Measure sparse
   link construction and actual repeated exchanges separately; do not justify
   consumer complexity solely with a link-building microbenchmark.

Acceptance: correct cell/face membership, balanced energy transfers, bounded
temperatures, equilibration in the correct direction, and the approved performance
gates. Update exact expectations only where the intentional traversal change
explains them. Equivalent physical inputs need not reproduce the old ordering.

### W5. Use the owner swap for odd projection iteration counts

Priority: benchmark-gated optimization. Use approved D5 for the prototype; retain
the API change only if the complete swap optimization passes D6/D7.

Affected surfaces:
[IGridProjection.cs](src/Kiyote/Simulations/Kiyote.Simulations/Topology/Projection/IGridProjection.cs),
[GridProjection.cs](src/Kiyote/Simulations/Kiyote.Simulations/Topology/Projection/GridProjection.cs),
airflow composition, callers, tests, benchmarks, visualizers, and README examples.

1. Add the generic compiled-owner parameter to projection/airflow updates and wire
   all consumers. Do not introduce a solver context, cast to internal layer types,
   capture a swap delegate per update, or swap only local variables.
2. For odd iterations, exchange pressure/scratch storage through the owner instead
   of copying every chunk. The public bound pressure layer must hold the answer.
3. Preserve dirty flags, pressure warm starts, binding identity, and empty-chunk
   behavior. Reacquire spans after swapping.
4. Keep the core requirement of one unchanging compilation during a solve.

Measure odd and even iteration counts. The existing 20-iteration benchmark cannot
demonstrate the copy-elimination benefit. Do not claim this fixes diffusion's
historical regression.

### W6. Mark thermal output dirty during its existing traversal

Priority: benchmark-gated optimization. Depends on W1.

In [Thermal.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Thermals/Thermal.cs),
mark the computed destination chunk dirty during the conduction/radiation loop.
Let the subsequent owner swap transfer those flags to the bound temperature layer,
removing the second chunk traversal solely for dirty marking.

Test repeated swaps with and without intervening commits and with empty chunks.
Do not apply whole-layer swapping to atmosphere: it computes selected work chunks,
and inactive scratch chunks can be stale.

### W7. Measure halo gathering through the current public API

Priority: experiment; production changes depend on results and the D8 release gate.

Compare the specialized helpers in
[TopologyChunk.cs](src/Kiyote/Simulations/Kiyote.Simulations/Topology/TopologyChunk.cs) and
[AtmosphereSimd.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Atmospherics/AtmosphereSimd.cs)
against `IGridLayer<T>.CopyPadded(chunk, 1, destination)`.

- Measure float and Direction data; also cover bool if a production gather uses it.
- Cover dense, partial, isolated, cross-chunk, and negative-coordinate layouts.
- Compare complete padded contents, including corners and missing-neighbour zeros.
- Measure whole diffusion/pressure, projection/advection, and LowFidelity consumers,
  not only an isolated copy microbenchmark.
- Do not assume fewer interface calls guarantees a win: the generic padding path
  performs neighbour setup and generic row-segment work.
- If the public path wins under D6/D7, use it consistently where applicable.
  If not, keep the specialization. Approved D4 permits considering a halo-one fast
  path inside Geometry/Buffers; follow D8, then repeat consumer-level A/B comparison.
- Do not replace one chunk gather with repeated per-row neighbourhood construction.

### W8. Cache publication topology until explicit invalidation

Priority: conditional experiment, not a prerequisite for correctness.
Depends on W3; no dependency change is required for the invalidation signal.

[AtmosphereFrames.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Atmospherics/AtmosphereFrames.cs)
currently compares full topology and resolves chunk coordinates during every
publication. Use the explicit invalidation signal selected in W3:

1. Mark publication topology for validation at initialization and invalidation.
2. Validate once at the next final publication, not every unchanged update.
   A property-only edit need not recreate buffers if occupancy still matches.
3. Cache source-to-buffer chunk addresses where the public API supports safe index
   access. Never cache a writable target span or a rotating-buffer snapshot across
   swaps. Do not expose storage internals to avoid a coordinate lookup.
4. Install the mapping and matching grid set together, and clear the validation
   flag only after successful installation. A failed publication must not leave a
   cached mapping that describes a different grid generation.
5. Cover additions/removals inside an existing chunk, last-cell removal, and new
   chunks. The caller reports successful edits through InvalidateTopology.

Do not add an occupancy revision in Geometry/Buffers solely to preserve automatic
publication behavior. Local vacuum/property refresh remains the separately
benchmark-gated W3 opportunity and follows D8 if it needs a new dependency API.

Keep full quantity copies and the existing triple-buffer lifetime protocol.
Partial publication requires per-slot generation tracking because rotated slots
contain different histories; that redesign is explicitly deferred.

### W9. Coordinate coupled fixed steps and publish the final coupled state

Priority: required correctness. Depends on W1-W3. Approved explicitly in D10.

The current coordinator advances all atmosphere steps, publishes its frames,
advances thermal steps, then performs all due exchanges. This can publish stale
temperature/pressure and makes coupling depend on elapsed-time batching. Different
child step caps can also advance the subsystems by different simulated durations.

1. Give the combined coordinator one elapsed-time accumulator and one exchange
   cadence. Keep standalone simulations' own elapsed-time loops for standalone use.
2. Use the shared fixed time step and
   `min(atmosphere.MaxStepsPerUpdate, thermal.MaxStepsPerUpdate)` as the combined cap.
   Validate required positive clock/cap settings at construction. On exceeding
   the cap, use the existing documented excess-time-drop policy once for the
   combined update, not independently per subsystem.
3. Prepare topology/material/solar state and rebuild links before physical work,
   including on zero-step updates. Reset per-update diagnostics once.
4. For each due tick, advance atmosphere once and structure once, then run any
   convection exchange due at that tick. Never call the children's elapsed-time
   Update methods from the coordinator. Retain the existing exchange interval's
   fixed-tick quantization and document it; do not add a new scheduling option.
5. After all ticks/exchanges, refresh touched atmosphere derived values, including
   pressure and wind, and publish once. Do not copy an intermediate frame and
   immediately copy another frame to repair it. Zero-step updates still finalize
   queued input/metadata changes and publish the appropriate final state.
6. Preserve supported individual-lease safety. Quantities finalized by one update
   describe its final coupled state; do not invent an atomic multi-lease acquisition
   guarantee that the existing frame API does not provide.
7. Keep the combined Update result as the number of exchanges performed. Apply D3
   resets to the new coordinator's accumulator/cadence during structural recreation.

Acceptance: both children advance the same number of fixed ticks; batching the
same ticks without cap truncation gives equivalent coupled results; final live
temperature/pressure and published frames agree; dirty state commits correctly.
Correct stepping/publication is required even if old numerical results or timing
numbers change. Measure that cost honestly before optional optimizations.

## Test matrix

Use real Geometry compilations for ownership/persistence integration tests and
existing plain-span unit tests for numerical kernels. Add a narrowly scoped
compiler decorator or test double only when necessary to verify compile/disposal
counts. Test names follow `Method_Condition_Expectation`.

| Area | Required coverage |
|------|-------------------|
| Ownership | Standalone and combined creation; one caller compilation and zero internal compiles; borrowed disposal leaves owner intact; combined public views omit child lifecycle controls; idempotent disposal; partial-construction failure; stable registered-layer count after repeated rebuilds |
| Persistence | Modify atmosphere and structure before commit; both persist on explicit owner commit; refresh/invalidation does not implicitly persist physical state; owner commit precedes structural invalidation; no stale commit; retry behavior if a binding throws |
| Structural lifecycle | Attach and detach under the selected protocol; stale misuse fails explicitly; approved reset policy; reacquired layers; replacement-construction failures |
| Live edits | Add/remove plus explicit invalidation within existing chunks and into new chunks; last cell removed; failed edits; negative coordinates; retain compilation identity |
| Metadata | Doors/permeability, material changes, vacuum faces/diagonals, seams, solar exposure, convection links, area/full invalidation equivalence |
| Sparse convection | Widely separated placements, touching chunk edges, negative coordinates, empty allocated chunks, shared walls; correct face membership; chunk/local address validity; conservation/floors/wake/publication bookkeeping; no old-order equality requirement |
| Projection | Odd/even iterations, at least existing 32/64 chunk sizes, bound pressure committed after swap, warm starts across updates, dirty flags, dense/sparse/vacuum/wall cases |
| Thermal swaps | Multiple steps without commit, commit between steps, empty chunks, source temperatures match results, unchanged conduction/radiation/solar behavior |
| Halo gathering | Exact padded output for each retained data type, corners/edges, holes, missing chunks, supported chunk sizes 32/64/128 |
| Frames | Add/remove with explicit invalidation; zero-step updates; no topology validation on unchanged publications; held leases and reader thread; replacement topology; old lease validity; final pressure/temperature after convection |
| Coupled timing | Shared tick counts with differing child caps; interval boundaries; equivalent tick batching without truncation; excess-time dropping once; no intermediate frame copies; standalone timing remains independently correct |
| Physics | Gas/heat conservation, venting, floors, conduction/radiation, and equilibration invariants; explicitly revise only expectations explained by approved stepping/traversal changes, never blanket-relax tolerances |
| Allocations | Warmed unchanged-topology Update paths gain no persistent per-step allocations; rebuild/setup allocations are reported separately |

Do not use wall-clock unit-test assertions for performance. Prove sparse work
through selected cells/chunks and measure elapsed cost with benchmarks.

## Benchmark protocol and adoption gates

1. Before code changes, capture contemporaneous Release baselines using the current
   dependencies and unchanged workloads. Historical reports are context, not an A/B
   control. Geometry and Buffers changed together in the historical comparison.
   After W1-W3/W9, establish a corrected-behavior baseline for optional candidates;
   do not count fewer simulated ticks or skipped exchanges as a performance win.
2. Keep existing MediumRun settings. Run benchmark processes sequentially on the same
   machine/runtime/configuration, with distinct artifact directories and no baseline
   overwrites. Restore equivalent input state for candidate comparisons.
3. Add focused cases for sparse link construction, chunk-addressed exchange,
   combined stepping/finalization, odd/even projection, halo shapes, thermal dirty
   marking, and publication metadata. Record actual tick/exchange counts for coupled
   comparisons. Keep large shape matrices focused on directly affected methods
   rather than multiplying every suite.
4. Repeat suspected wins/regressions in at least two independent matched runs.
   Require at least 10% improvement on the intended workload and reject confirmed
   regressions greater than 10% elsewhere. Use these approved D6/D7 thresholds
   together with BenchmarkDotNet error intervals; overlapping
   intervals or inconsistent results are inconclusive, not evidence of a win.
5. Reject a candidate that breaches the approved regression/allocation budget.
   If a required correctness fix has measurable overhead, report it explicitly and
   optimize within the new contract; do not retain unsupported ownership for speed.
6. Rerun all retained end-to-end methods after accepted changes. Refresh their
   result tables, include retained new benchmark cases, and remove no surviving
   class without a stated scope reason. Preserve the requested document format.

Historical reference measurements (microseconds; not acceptance thresholds):

| Method | Latest full run | Historical baseline | Interpretation |
|--------|----------------:|--------------------:|----------------|
| Diffusion.Update | 25.42 | 22.43 | Reproducible slowdown; investigate, do not assume cause |
| Pressure.Update | 27.34 | 23.22 | Uses diffusion; reproducible slowdown |
| Projection.Update | 555.0 | 563.8 | No established regression; even iterations |
| Advection.Update | 51.97 | 52.28 | No established regression |
| Airflow.Update | 765.4 | 756.3 | No established regression |
| Atmosphere.Update | 262.11 | 246.15 | Possible slowdown; evidence inconclusive |
| Atmosphere.ReadPressureFrame | 16.09 | 15.27 | Apparent slowdown did not reproduce |
| Thermal.Update | 209.4 | 197.7 | Apparent slowdown did not reproduce |
| Thermal.UpdateWithMovingSun | 233.9 | 231.6 | No established regression |

## Execution order and verification

1. Follow resolved D1-D10; there are no outstanding design questions. Pause at the
   D8 publication gate if an unpublished dependency API is needed.
2. Re-establish current build/test and targeted benchmark baselines.
3. Implement and validate W1, then commit it before starting W2. Follow with
   separately validated/committed W2, W3, and W9 changes in dependency order. W1
   supplies internal stepping hooks but does not change coupled timing/publication.
4. Run affected LowFidelity unit/integration tests at every boundary and the full
   solution validation for public API changes. Capture the corrected-behavior
   performance baseline after W3/W9, before optional optimizations.
5. Evaluate W4-W7 independently so measurements can attribute changes. W5 does not
   require W4; W6 does not require W5. Evaluate W8 only when its prerequisites exist.
6. Update all changed API consumers, XML comments, core README, and atmosphere
   development notes. Document owner/layer lifetime, the selected edit sequence,
   explicit frame invalidation, coupled timing, and intentional behavior changes.
7. Run targeted core/LowFidelity tests, then the full solution build and test suites.
8. Run final retained benchmarks, update tables, and summarize accepted/rejected
   experiments, correctness outcomes, and measured regressions outside BENCHMARK.md.

Existing validation entry points, from the repository root:

```powershell
dotnet build Kiyote.Simulations.slnx --no-restore --nologo -v:q
dotnet test Kiyote.Simulations.slnx --no-build --no-restore --nologo -v:q
dotnet run --project test\Kiyote\Simulations\Kiyote.Simulations.Benchmarks -c Release --no-restore -- --filter '*GridDiffusionBenchmarks*' '*GridPressureBenchmarks*' --artifacts BenchmarkDotNet.Artifacts\correctness-baseline-core
dotnet run --project test\Kiyote\Simulations\LowFidelity\Kiyote.Simulations.LowFidelity.Benchmarks -c Release --no-restore -- --filter '*AtmosphereBenchmarks*' '*ThermalBenchmarks*' --artifacts BenchmarkDotNet.Artifacts\correctness-baseline-lowfidelity
```

Build changed test projects before using `--no-build`. Use focused test filters and
benchmark selectors during iteration. Restore dependencies only after an approved
manifest change or a missing-dependency failure. For dependency changes, also run
the affected sibling repository tests before handing off to the user for publishing
and before consuming the new API.

## Completion criteria

- D1-D10 contain explicit selections and no implementation-critical decision remains.
- Selected ownership/state contracts are implemented and documented consistently.
- All applicable correctness matrix cases pass; solution builds without warnings.
- All retained correctness tests pass. Replace obsolete compatibility assertions
  with explicit tests of the selected contracts and intentional behavior changes;
  do not delete inconvenient physical invariants or hide changes in wider tolerances.
- Each optional change is accepted/rejected using reproducible measurements and the
  approved thresholds, not assumed benefits.
- No new steady-state allocation regression or hidden state loss is introduced.
- Current benchmark tables contain measured results and retain their simple format.
- No temporary local references, experimental implementation branches, scratch
  files, or unrelated edits are left as part of the delivered change.
