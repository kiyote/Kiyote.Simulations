# Experiment record

This is the permanent record of performance experiments in Kiyote.Simulations:
what changed, which workloads were measured, what was retained or rejected, and
why. It includes the correctness baseline needed to interpret the measurements.
It does not require a separate implementation plan or locally retained logs.

The measurements below are historical snapshots, not promises for every runtime,
machine or topology. [BENCHMARK.md](BENCHMARK.md) holds the latest result tables.
The latest tables use retained candidate run B2 of the publication-topology cache
experiment for all 11 LowFidelity cases, plus a subsequent retained-runtime rerun
of all 15 core cases: 26 consumer cases in total.

## Decisions at a glance

- [Sparse chunk-addressed convection](#sparse-chunk-addressed-convection) was
  retained: it improved complete coupled updates and sparse metadata rebuilding.
- [Owner-mediated odd projection swap](#owner-mediated-odd-projection-swap) was
  retained for low odd iteration counts with retained empty chunks. It is not a
  general dense/high-iteration speedup.
- [Thermal dirty-marking traversal](#thermal-dirty-marking-traversal) was rejected:
  removing a traversal did not demonstrate the required end-to-end improvement.
- [Public halo gathering](#public-halo-gathering) was rejected: replacing the
  specialized halo-one helpers with the public copy API caused large, repeatable
  consumer regressions despite equivalent contents and zero warmed allocations.
- [Invalidation-driven publication topology cache](#invalidation-driven-publication-topology-cache)
  was retained: publication-heavy zero-step atmosphere updates took 13.7% less
  time, without a confirmed greater-than-10% regression or new warmed allocations.

## Measurement conventions

- Optional candidates require at least a repeatable 10% improvement on an intended
  representative workload, no confirmed regression greater than 10% elsewhere,
  and no new steady-state allocations. Those were the acceptance criteria for
  these experiments. Overlapping error intervals or inconsistent runs are
  inconclusive, not proof of a win. Correctness, persistence and physical
  invariants must also hold; reducing actual work is not a valid optimization.
- Matched experiments use A1 -> B1 -> B2 -> A2: A is the retained baseline and B
  the candidate. Baseline Release binaries were preserved during measurement to
  allow the reverse comparison without reverting the worktree, then removed.
- Percentage changes compare arithmetic means of run means, not pooled
  statistical estimates. Negative time changes are improvements; the sparse
  convection table instead labels positive time reductions explicitly.
- BenchmarkDotNet's in-process allocation estimates and exact warmed
  current-thread allocation checks are separate measurements. Neither replaces
  the other; topology rebuild allocations are outside unchanged-topology
  steady state.
- Shared host/configuration: BenchmarkDotNet 0.15.8, MediumRun/InProcessNoEmit,
  one launch, 10 warmups/15 measurements, i7-9700K, Windows 11, .NET 10.0.13,
  SDK 11.0.100-rc.2.26504.105. Runs are sequential, with no concurrent build/test
  runs during timing.
- Dependency context: Kiyote.Geometry 0.35.0, Kiyote.Buffers 0.15.0 and
  Kiyote.Buffers.Numerics 0.15.0, targeting .NET 10. No dependency changes were
  needed for the five optional experiments recorded here.

## Reproduction and evidence

Use contemporaneous Release baselines, not the historical migration figures.
Keep benchmark code, input state, iteration counts, source injection and actual
tick/exchange counts equivalent between versions. Identify the intended
representative workload before timing, then check the other consumer workloads
for regressions. Do not substitute an isolated copy/swap microbenchmark for the
whole consumer.

The commits named in each section identify the source context. Accepted commits
contain the retained production change. Rejected-experiment commits contain
coverage, benchmark checks and documentation, **not** the reverted runtime
candidate; its exact change is described in the relevant section. Use that
experiment's benchmark harness for both versions, even when the production
baseline comes from an earlier commit.

After restoring dependencies and building Release, the existing runners are:

```powershell
dotnet build Kiyote.Simulations.slnx -c Release --nologo
dotnet run --project test\Kiyote\Simulations\Kiyote.Simulations.Benchmarks -c Release --no-build --no-restore -- --filter '*' --artifacts BenchmarkDotNet.Artifacts\core-comparison
dotnet run --project test\Kiyote\Simulations\LowFidelity\Kiyote.Simulations.LowFidelity.Benchmarks -c Release --no-build --no-restore -- --filter '*' --artifacts BenchmarkDotNet.Artifacts\lowfidelity-comparison
```

Choose distinct artifact directories for every version/run and preserve baseline
binaries before changing production code. Raw reports were written beneath the
ignored `BenchmarkDotNet.Artifacts` directory and are not guaranteed to exist in
a fresh checkout. All decision-relevant timing tables, validation results,
allocation caveats and conclusions are reproduced here.

## Historical migration measurements

These are historical reference measurements in microseconds, not acceptance
thresholds or a current A/B control. Geometry and Buffers changed together in the
migration comparison, so these figures do not isolate the Geometry API's cost.
"Latest full run" below refers to that historical migration rerun.

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

## Correctness baseline and coupled stepping

- Geometry migration and benchmark refresh: committed as `d4a463c`;
  208 tests passed, solution build had no warnings/errors.
- Compilation ownership: implemented in `2994f0d`. Factories borrow the caller's compilation; persistence is
  explicit; children expose lifecycle-free state views; owned and temporary layers
  are cleaned up deterministically. Internal prepare/step/publication hooks became
  available without changing coupled timing yet.
- Ownership validation: full solution build had no warnings/errors; all 248 tests passed;
  all 40 new ownership regressions also passed in Release. Warmed unchanged-topology
  updates allocated zero measured bytes for atmosphere, thermal, and combined
  simulations in both configurations. Ownership tracking allocated only during
  construction/rebuild, not steady-state stepping.
- Structural lifecycle: implemented in `83bbaeb`. Added 26 structural-lifecycle cases covering retained/new/detached
  cells, empty replacements, detached layer references, transient flow/timing/
  exchange resets, frame-provider replacement and held leases, runtime option
  reapplication, and explicit commit/construction retries. Expanded the caller
  lifecycle and failure-handling documentation; no runtime machinery was needed.
- Lifecycle validation: solution built without warnings/errors; all 274 tests passed.
  All 66 ownership/lifecycle regressions also passed in Release, including the
  existing zero-allocation checks.
- Live invalidation: implemented in `04c6f31`. Explicit invalidation covers live occupancy, permeability/material
  properties, derived-state publication, solar exposure, and convection links.
  Empty areas do nothing; a full invalidation supersedes queued areas. Replacements
  are staged before old metadata registrations are removed, with temporary cleanup
  and pending-work retention on failure. Affected neighbour rings wake and publish
  even without a physics tick; blocked pipe momentum is cancelled without clearing
  unchanged open pipes. Ring expansion clamps at signed coordinate limits.
- Invalidation validation: added 49 live-edit cases; all 323 solution tests passed and the build
  had no warnings/errors. All 115 ownership/lifecycle/invalidation cases passed in
  Release, including exact zero-allocation checks after successful chunk-growth
  invalidation for all three simulation kinds. Coverage included existing/new
  chunks, retained empty chunks, held leases, negative/limit coordinates, diagonals,
  placement seams, all four thermal material properties, solar occlusion, convection
  membership/energy balance, caller-owned layers, and repeated failure retries.
- Live invalidation retained complete vacuum/property rebuilds and the existing publication
  presence comparison. The explicit caller/publication invalidation contract was
  established. Publication-topology/address caching was not part of this change.
  No upstream package change was required.
- Coupled fixed-step coordination: implemented in `9844801`. One coordinator-owned `TimeSpan`-tick accumulator
  advances both children using the captured shared step and smaller child cap.
  Each tick advances atmosphere, advances structure, then performs any due exchange.
  All preparation finishes before consuming elapsed time; derived atmosphere state
  and frames finalize once after all ticks/exchanges, including zero-step updates.
  Standalone loops remain independent. Typed internal factory access avoids casts
  of public state views, extra step interfaces, and child elapsed-time updates.
- Coupled stepping validation: 42 new coupled timing cases; all 365 solution tests passed in Debug
  and Release, with zero build warnings/errors. Cases covered exact phase order,
  fractional batching, unequal caps, exact-cap remainder retention, excess-time
  dropping, maximum elapsed values, cadence quantization, final-frame consistency,
  held leases, aggregate venting, pre-step failure retry, commit persistence,
  invalid configuration, the minimum representable step, standalone behavior,
  and zero-allocation coupled batches.
- Corrected-behavior benchmark baseline: reran all four existing LowFidelity workloads
  and added a sealed 100x100 coupled aluminium/gas workload. Each coupled
  invocation verifies its actual tick and exchange counts (one or eight each).
  Source injection keeps atmosphere/structure work active; timing includes final
  publication. The new coupled cases were also rerun for confirmation.
  Benchmark tables were refreshed with these baseline results; core kernel
  results were retained because those kernels did not change. No meaningful
  regression was established in the rerun standalone workloads. There was no
  comparable old coupled benchmark, so its measurements established a baseline
  rather than a claimed speedup/regression.
- Coupled benchmark confirmation: used the same MediumRun,
  InProcessNoEmit, 10 warmups/15 measurements and host/runtime as the migration
  runs. Coupled confirmation means: 1.180 ms for one tick/exchange and 9.511 ms
  for eight. The benchmark itself warms 100 invocations then requires exactly zero
  current-thread managed bytes over another 100 on each actual 100x100 workload;
  both gates passed. BenchmarkDotNet's separate in-process allocation figures were
  zero for one tick and 62 B/invocation for eight (3,984 total measured bytes over
  64 invocations), despite the exact warmed-update gates measuring zero.

## Sparse chunk-addressed convection

**Outcome:** retained. Commit `27057fc`.

**Baseline:** production source at `9844801`, using the same benchmark harness
as the candidate. Link construction scanned world-coordinate bounds, and
exchange resolved cell coordinates rather than retaining chunk/local addresses.

**Hypothesis and scope:** visit allocated chunks and occupied cells instead of
the bounding rectangle, and store chunk/local convection-face addresses. This
should benefit widely separated placements and remove repeated coordinate
resolution during full coupled updates. The exchange still uses current gas mass
and temperature storage, preserving energy accounting and dirty/publication state.
The implementation is in
[AtmosphericThermals.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/AtmosphericThermals/AtmosphericThermals.cs)
and
[AtmosphericThermalsConvection.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/AtmosphericThermals/AtmosphericThermalsConvection.cs).

- Convection-link rebuilding traverses allocated chunks and ascending local
  presence bits; metadata spans are acquired once per current/cardinal-neighbour
  chunk for each source chunk. Descriptors contain chunk/local addresses, with
  floor then N/E/S/W membership. Empty chunks, holes, diagonals and permeable
  neighbours do not become wall faces. In-place exchange deliberately follows
  compiled traversal, not a global world-coordinate sort or an
  attachment-order-independent result.
- Exchange uses narrow internal chunk-addressed state interfaces. Public and
  internal energy inputs share application cores, including floors, actual gas
  energy applied, capacity guards and dirty/wake/publication bookkeeping. Current
  gas layers are summed once per gas cell after transport/condensation; exchange
  changes no mass, so that capacity can be reused across its faces. Temperature
  spans are acquired inside each exchange after thermal swaps, never stored in
  descriptors. Temporary metadata still uses scoped ownership and retryable
  invalidation. No public API or upstream dependency change was needed.
- Validation: 24 new integration cases; all 389 tests passed in Debug and Release
  with zero build warnings/errors. Coverage included exact floor/cardinal
  membership and ordering across positive/negative chunk seams, all four signed
  coordinate corners, distant placements, empty allocated chunks, shared walls,
  permeable neighbours, compiled traversal order, repeated temperature swaps,
  fresh mass after transport, the gas temperature floor, commit after clearing
  dirty flags immediately before convection, final temperature publication, and
  exact zero-allocation warmed sparse updates. Existing ownership, metadata
  failure/retry, material invalidation, coupled timing and allocation tests passed.
- Measurements: matched A1 -> B1 -> B2 -> A2 runs of all ten LowFidelity
  benchmark cases, using identical benchmark code, published dependencies,
  MediumRun/InProcessNoEmit configuration, host and runtime. A1/A2 ran a preserved
  copy of the baseline Release binaries; B1/B2 ran the candidate. No other build,
  test or benchmark ran concurrently.
  The existing dense 100x100 workload and its injection/tick/exchange assertions
  were unchanged. The new workload uses four sealed 16x16 rooms, spaced 16 cells
  apart when dense (32x32 total bounds) or 768 apart when sparse (784x784 bounds).
  `Update` advances one real tick/exchange with ongoing gas/heat input.
  `RebuildTopology` invalidates fully and updates with zero elapsed time: it
  measures both children's metadata/solar preparation plus convection links,
  not an isolated link-builder microbenchmark or skipped physics work.

  | Workload (all times us) | A1 | A2 | B1 | B2 | Mean time reduction |
  |------------------------|---:|---:|---:|---:|--------------------:|
  | Atmosphere.Update | 287.92 | 276.40 | 257.75 | 253.55 | 9.4% |
  | ReadPressureFrame | 16.09 | 16.05 | 15.18 | 15.47 | 4.6% |
  | Thermal.Update | 203.1 | 205.7 | 198.7 | 200.6 | 2.3% |
  | Thermal.UpdateWithMovingSun | 239.8 | 230.9 | 235.6 | 228.4 | 1.4% |
  | Coupled 100x100, one tick | 1228.0 | 1212.0 | 773.3 | 788.3 | 36.0% |
  | Coupled 100x100, eight ticks | 9202.0 | 9384.0 | 5849.4 | 6453.5 | 33.8% |
  | Four dense rooms, update | 105.6 | 105.3 | 65.40 | 63.46 | 38.9% |
  | Four dense rooms, rebuild | 183.9 | 186.5 | 125.53 | 122.62 | 33.0% |
  | Four sparse rooms, update | 189.9 | 184.1 | 147.82 | 147.67 | 21.0% |
  | Four sparse rooms, rebuild | 4943.6 | 4282.5 | 875.50 | 872.31 | 81.1% |

  Reductions compare the arithmetic mean of the two candidate run means with the
  arithmetic mean of the two baseline run means; they are not pooled statistical
  estimates. Both candidate runs cleared 10% on repeated exchanges and sparse
  rebuilds, even against the faster baseline run. No representative workload
  had a confirmed slowdown, let alone one exceeding 10%. Standalone controls are
  not claimed as attributable convection speedups. Benchmark tables were refreshed from
  B2; unchanged core-kernel measurements were retained.
- Allocation gates: all coupled benchmark setups warm 100 invocations and
  require exactly zero current-thread bytes over another 100, including the
  actual dense 100x100 one/eight-tick cases and both four-room layouts. All four
  runs passed. B2's separate in-process BenchmarkDotNet figures were 6 B/45 B for
  dense one/eight ticks and 1 B for each four-room update. Full rebuilds
  intentionally allocated (127985 B dense, 217876 B sparse); these are outside
  unchanged-topology steady state. No new steady-state allocation was measured.

**Decision and future refactoring:** retain sparse traversal and chunk/local
descriptors. Repeated coupled one/eight-tick updates took 36.0%/33.8% less time,
and sparse full rebuilds took 81.1% less time. Do not reintroduce a bounding-box
scan merely to obtain world-coordinate ordering. Any new traversal or descriptor
cache must still invalidate after live edits, read gas mass after transport, and
reacquire temperature spans after storage swaps; those are correctness constraints,
not optional costs to omit from a benchmark.

## Owner-mediated odd projection swap

**Outcome:** retained for low odd iteration counts with retained empty chunks.
Commit `2311711`.

**Baseline:** production source at `27057fc`, with the candidate's parameterized
benchmark harness. An odd Jacobi iteration count left the final pressure in
scratch storage and copied it back across allocated chunks.

**Hypothesis and scope:** use the supplied compiled owner's storage swap instead
of that copy. This avoids copying every retained empty chunk when the solve does
little arithmetic. The change belongs in
[GridProjection.cs](src/Kiyote/Simulations/Kiyote.Simulations/Topology/Projection/GridProjection.cs);
complete airflow measurements include its pressure/advection composition.

- Projection and airflow Update accept the explicit generic compiled owner
  first, with all production, test, benchmark and visualizer consumers wired.
  Odd solves use the owner's pressure/scratch storage swap; even solves need no
  final swap. Numerical kernels remain non-generic. Bound pressure identity,
  dirty flags and warm starts are preserved, and the gradient stage acquires
  spans after the swap. No context, delegate, internal layer cast or upstream
  dependency change was introduced.
- Validation: 46 new cases cover zero/one/two/three/20/21 iterations, repeated
  warm starts with and without intervening commits, exact bound pressure output
  and commit counts, unchanged unrelated cell values, negative coordinates and
  chunk seams, retained empty chunks, entirely empty compilations, solved
  gradients and zero warmed allocations for odd/even cases at chunk sizes 32/64.
  The pre-existing projection fixture now uses one compiled owner for all layers
  instead of three coincident layouts. Negative iterations are rejected at
  construction: an odd negative count otherwise skips relaxation but would swap
  an unsolved scratch buffer into pressure. Zero retains its existing no-relaxation
  behavior. All 435 tests passed in Debug and Release, with no build warnings/errors.
- Measurements: matched A1 -> B1 -> B2 -> A2 runs of all 15 core benchmark
  cases, using identical benchmark workloads/configuration and published
  dependencies. A1/A2 used preserved baseline Release binaries; B1/B2 used the
  owner-swap candidate. The final constructor-only negative-iteration guard is
  outside timed work; the measured valid-configuration Update body is unchanged.
  Same MediumRun/InProcessNoEmit, 10 warmups/15 measurements, host and runtime as
  the sparse convection experiment; no other build/test/benchmark ran concurrently.
  Projection and composed airflow keep the original 100x100 occupied field with
  32x32 chunks, and parameterize 1/20/21 iterations and 0/128 retained empty
  chunks. Empty chunks are produced by compiling occupied cells in additional
  chunks and removing those cells through the live assembly; setup verifies the
  exact empty-chunk count. Timed calls perform the full solve/composition and
  caller velocity/concentration swaps, not an isolated copy/swap microbenchmark.

  | Workload (all times us) | Iterations | Empty chunks | A1 | A2 | B1 | B2 | Mean time change |
  |------------------------|-----------:|-------------:|---:|---:|---:|---:|-----------------:|
  | Projection | 1 | 0 | 103.6 | 103.5 | 103.6 | 100.1 | -1.6% |
  | Projection | 1 | 128 | 139.9 | 140.7 | 104.7 | 103.1 | -25.9% |
  | Projection | 20 | 0 | 571.2 | 522.9 | 547.2 | 545.7 | -0.1% |
  | Projection | 20 | 128 | 623.1 | 553.3 | 602.5 | 593.6 | +1.7% |
  | Projection | 21 | 0 | 596.6 | 531.8 | 593.3 | 583.5 | +4.3% |
  | Projection | 21 | 128 | 684.3 | 610.5 | 632.9 | 634.6 | -2.1% |
  | Airflow | 1 | 0 | 327.3 | 335.8 | 326.2 | 326.6 | -1.6% |
  | Airflow | 1 | 128 | 379.9 | 390.0 | 336.2 | 327.8 | -13.8% |
  | Airflow | 20 | 0 | 816.0 | 774.8 | 786.1 | 839.4 | +2.2% |
  | Airflow | 20 | 128 | 883.6 | 791.0 | 857.5 | 860.0 | +2.6% |
  | Airflow | 21 | 0 | 775.6 | 780.6 | 831.5 | 806.6 | +5.3% |
  | Airflow | 21 | 128 | 898.6 | 937.4 | 887.8 | 818.9 | -7.0% |
  | Diffusion control | - | - | 27.78 | 24.92 | 26.65 | 26.77 | +1.4% |
  | Pressure control | - | - | 27.14 | 26.51 | 26.96 | 26.59 | -0.2% |
  | Advection control | - | - | 53.81 | 54.04 | 53.67 | 53.21 | -0.9% |

  Changes compare the arithmetic means of the two candidate and two baseline
  run means, not pooled statistical estimates. The retained-empty-chunk one-
  iteration projection and complete airflow cases improved by more than 10% in
  both candidate runs even against the faster baseline run. Normal dense and
  high-iteration cases showed no reliable improvement. Dense 21-iteration cases
  averaged 4.3% slower for projection and 5.3% slower for airflow; these are
  observed costs, not erased by the sparse win. High-iteration results were noisy,
  with bimodal-distribution warnings in A2; no greater-than-10% slowdown was
  confirmed across the repeated comparisons. Adoption is specific to the
  demonstrated workload, not a claim to fix diffusion's historical regression.
- Allocation gates: both projection and airflow benchmark setups warm 100
  calls and require exactly zero current-thread managed bytes over another 100
  for all six parameter combinations each. All four runs passed, as did the
  focused odd/even allocation tests. Separate in-process B2 allocation figures
  (projection 1-6 B/call; airflow 2-6 B/call) were retained in the refreshed core
  benchmark tables. LowFidelity tables from the sparse convection experiment
  were unchanged because those workloads were not rerun for the projection swap.

**Decision and future refactoring:** retain the owner swap for odd solves, but do
not claim a general dense/high-iteration gain. The demonstrated one-iteration
case with 128 retained empty chunks improved projection/complete airflow time by
25.9%/13.8%; dense 21-iteration means instead showed 4.3%/5.3% slower execution.
Pressure and scratch must be distinct layers from the supplied live compilation.
The owner swap must carry dirty flags while preserving bound layer identity, and
the gradient stage must reacquire spans afterward. Scratch is working storage,
not a promised duplicate of final pressure. Rebenchmark numerical or storage
refactorings across zero/odd/even counts and commits, not just an isolated swap.

## Thermal dirty-marking traversal

**Outcome:** rejected because no repeatable 10% end-to-end gain was demonstrated.
Record and regression-coverage commit `ca3f906`.

**Baseline:** production source at `2311711`, with the candidate's strengthened
thermal benchmark harness. Each tick computes destination temperature for every
nonempty chunk, swaps the two temperature layers through the compiled owner, then
traverses chunks again to mark the bound temperature dirty.

**Candidate:** in
[Thermal.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Thermals/Thermal.cs),
call `_temperatureNext.MarkDirty(chunk)` immediately after radiation in the
existing computation loop; retain the owner swap, and remove only the post-swap
dirty-marking loop. The owner transfers dirty flags with storage. Numerical
kernels and all conduction/radiation work are unchanged. This was a local
prototype; the rejected runtime body is not present in the recorded commit.

- The prototype marked each computed `_temperatureNext` chunk dirty after
  conduction/radiation, relying on the owner swap to transfer those flags to
  bound temperature. It removed the second dirty-only chunk traversal without
  changing physics, atmosphere's selected-chunk handling, public APIs or
  dependencies. Correctness/allocation checks passed, but representative
  end-to-end measurements did not demonstrate the required repeatable 10%
  improvement. The runtime change was therefore reverted; the post-revert
  Release runtime DLL matched the preserved baseline's SHA256.
- Retained coverage: 19 new integration cases verify exact conduction across
  a negative-coordinate chunk boundary, zero/one/two/three/eight ticks per update,
  repeated swaps with and without intervening commits, stable bound temperature
  identity, exact dirty commit counts, unchanged unrelated source fields, pending
  added energy with and without a preceding commit, zero-step dirty preservation,
  final solar-heated/radiatively cooled persistence, retained empty chunks,
  entirely empty compilations, and exact zero warmed allocations with and without
  commits. The full 454-test Release suite passed on the candidate. The retained
  implementation passed all 454 tests in both Debug and Release, with zero build
  warnings/errors. Thermal benchmark setup now enforces zero current-thread
  allocations over 100 fixed-sun and 100 moving-sun calls after warming both;
  each timed call also verifies exactly one actual thermal tick.
- Measurements: matched A1 -> B1 -> B2 -> A2 runs of all ten LowFidelity
  benchmark cases. Benchmark code, workloads and published dependencies were
  identical between versions. A1/A2 used preserved baseline Release binaries;
  B1/B2 used the single-traversal candidate. Baseline binary hashes were checked
  before A1 and A2. Same MediumRun/InProcessNoEmit, 10 warmups/15 measurements,
  host and runtime as the sparse convection experiment; no other build/test/benchmark
  ran concurrently.
  The intended primary workload was the existing 100x100 standalone thermal
  update, not an isolated dirty-marking or empty-world microbenchmark. Moving
  sun, dense coupled one/eight-tick updates and four dense/sparse rooms supplied
  consumer-level comparisons; atmosphere, frame reads and metadata-only rebuilds
  supplied controls.

  | Workload (all times us) | A1 | A2 | B1 | B2 | Mean time change |
  |------------------------|---:|---:|---:|---:|-----------------:|
  | Atmosphere.Update | 281.46 | 266.94 | 272.77 | 248.29 | -5.0% |
  | ReadPressureFrame | 16.62 | 15.72 | 16.14 | 15.12 | -3.3% |
  | Thermal.Update | 208.3 | 198.6 | 214.7 | 201.4 | +2.3% |
  | Thermal.UpdateWithMovingSun | 236.0 | 247.1 | 241.9 | 235.4 | -1.2% |
  | Coupled 100x100, one tick | 789.4 | 865.3 | 822.8 | 833.1 | +0.1% |
  | Coupled 100x100, eight ticks | 5827.6 | 6357.2 | 6006.2 | 5560.3 | -5.1% |
  | Four dense rooms, update | 66.24 | 66.31 | 67.63 | 63.89 | -0.8% |
  | Four dense rooms, rebuild | 126.23 | 134.35 | 125.16 | 125.11 | -4.0% |
  | Four sparse rooms, update | 148.49 | 156.53 | 141.33 | 149.75 | -4.6% |
  | Four sparse rooms, rebuild | 795.22 | 817.88 | 823.42 | 831.90 | +2.6% |

  Changes compare the arithmetic means of the candidate and baseline run means,
  not pooled statistical estimates. No workload demonstrated a repeatable 10%
  improvement, and control drift/bimodal-distribution warnings prevent attributing
  the small observed changes to moving dirty marking. Thermal.Update was slower in both candidate
  runs than in the faster baseline. No greater-than-10% slowdown was confirmed;
  rejection was for failure to demonstrate the required gain, not a proven large
  regression or correctness failure.
- Allocation gates: standalone thermal and coupled dense/sparse benchmark
  setups passed exact warmed zero-allocation checks in all four runs, as did the
  new swap/commit allocation tests. Separate in-process BenchmarkDotNet figures
  were preserved: A2 reported 1 B for standalone thermal and four-room updates,
  6 B/47 B for dense coupled one/eight ticks. Rebuilds allocate by design.
  Benchmark tables were refreshed from A2 LowFidelity statistics for the retained
  implementation, not the rejected candidate. Core projection-swap tables were unchanged.

**Why rejected and when to revisit:** correctness and allocation behavior passed,
but the intended 100x100 thermal update averaged 2.3% more time, moving sun only
1.2% less, and even the eight-tick coupled case only 5.1% less. None demonstrated
the required repeatable 10% improvement. Keep the original post-swap marking pass.
This is not proof that the candidate is inherently slower: the changes were small
relative to measurement variation. Removing a loop is insufficient evidence by
itself. Revisit only with new profiling evidence that this pass matters for a
representative workload, or materially changed storage/dirty-tracking costs.
Do not extend whole-layer swapping to atmosphere's selected active chunks:
inactive scratch chunks can contain stale values.

## Public halo gathering

**Outcome:** rejected because repeatable consumer slowdowns exceeded the 10%
regression budget. Record and regression-coverage commit `26c3ddb`.

**Baseline:** production source at `ca3f906`, using the candidate's benchmark
harness. The existing helpers copy centre rows and border cells directly, with
default values for missing neighbours.

**Candidate:** replace the complete bodies of
[TopologyChunk.GatherPadded](src/Kiyote/Simulations/Kiyote.Simulations/Topology/TopologyChunk.cs)
and
[AtmosphereSimd.Gather](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Atmospherics/AtmosphereSimd.cs)
with `layer.CopyPadded(chunk, 1, padded)`, removing their now-unused private border
helpers. Keep the call sites and all numerical kernels unchanged. The intended
benefit was using one shared public implementation instead of specialized
halo-one copying, with fewer consumer-side calls. The local candidate was reverted
and is not present in the recorded commit.

- Both internal gather entry points were temporarily reduced to
  `IGridLayer<T>.CopyPadded(chunk, 1, destination)`, so all existing callers
  consistently used the public API. Numerical kernels, iteration counts, source
  inputs, traversal and ownership were unchanged. Full-content and allocation
  checks passed, but the candidate repeatedly made whole consumers substantially
  slower. Both specialized gatherers were restored; their rebuilt Release DLLs
  matched the preserved baseline SHA256 hashes. No production API or dependency
  change was retained.
- Retained coverage: the two focused fixtures now contain 104 cases, 91 more
  than before this experiment. They verify both float and Direction against an independent
  world-coordinate oracle as well as each other. Shapes include full 3x3
  neighbourhoods, partial chunks with holes, positive and negative placements,
  cardinal/diagonal boundaries, isolated occupied chunks, retained empty chunks,
  missing-neighbour zeroes, all padded corners, and nonzero-prefilled oversized
  scratch with untouched suffixes. Core tests cover 32/64-cell chunks;
  LowFidelity covers its fixed 32-cell layout. Both public and specialized paths
  pass exact warmed zero-allocation checks for float/Direction and dense/isolated
  gathers. No production gather uses bool, so no unused bool variant was added.
  All 545 tests passed on the candidate in Release and on the retained code in
  Debug and Release, with zero build warnings/errors.
- Benchmark validation: atmosphere setup now warms 100 real updates and
  requires exactly zero current-thread managed bytes over another 100; each
  timed update verifies exactly one actual atmosphere tick. This setup also
  means the pressure-frame read control reads a frame populated by real
  updates, not just an initial frame. Benchmark code is identical for A/B.
  Existing thermal, projection, composed airflow and dense/sparse coupled
  allocation/tick/exchange checks remained enabled and passed in every run.
- Measurements: matched A1 -> B1 -> B2 -> A2 runs of all 25 consumer cases
  (15 core and 10 LowFidelity), sequentially with no concurrent build/test runs.
  A1/A2 used preserved baseline Release binaries, with their hashes checked
  before both runs; B1/B2 used the public-copy candidate. Same published
  dependencies, MediumRun/InProcessNoEmit, 10 warmups/15 measurements, i7-9700K,
  Windows 11 and .NET 10.0.13.
  These measure complete diffusion/pressure, projection/advection/airflow,
  atmosphere/thermal and coupled updates, not isolated copies. Advection
  includes both float and Direction gathers. Existing sparse and retained-
  empty-chunk cases remain in the measured consumers.

  | Workload (all times us) | Iterations | Empty chunks | A1 | A2 | B1 | B2 | Mean time change |
  |------------------------|-----------:|-------------:|---:|---:|---:|---:|-----------------:|
  | Diffusion | - | - | 27.74 | 25.47 | 31.22 | 32.37 | +19.5% |
  | Pressure | - | - | 28.24 | 26.23 | 30.80 | 32.48 | +16.2% |
  | Advection | - | - | 51.05 | 52.27 | 67.59 | 70.87 | +34.0% |
  | Projection | 1 | 0 | 99.24 | 99.36 | 128.7 | 129.4 | +30.0% |
  | Projection | 1 | 128 | 115.62 | 106.06 | 134.6 | 137.3 | +22.7% |
  | Projection | 20 | 0 | 584.48 | 538.38 | 724.0 | 718.4 | +28.5% |
  | Projection | 20 | 128 | 634.44 | 578.04 | 765.2 | 757.8 | +25.6% |
  | Projection | 21 | 0 | 576.72 | 552.50 | 713.6 | 740.7 | +28.8% |
  | Projection | 21 | 128 | 611.86 | 616.69 | 755.8 | 776.6 | +24.7% |
  | Airflow | 1 | 0 | 295.8 | 320.9 | 411.8 | 430.7 | +36.6% |
  | Airflow | 1 | 128 | 325.9 | 343.4 | 447.8 | 438.3 | +32.4% |
  | Airflow | 20 | 0 | 764.4 | 756.5 | 1009.6 | 1001.3 | +32.2% |
  | Airflow | 20 | 128 | 772.1 | 805.6 | 1056.8 | 1068.7 | +34.7% |
  | Airflow | 21 | 0 | 738.0 | 808.6 | 989.0 | 1040.5 | +31.2% |
  | Airflow | 21 | 128 | 795.4 | 839.4 | 1048.7 | 1066.2 | +29.4% |
  | Atmosphere.Update | - | - | 251.41 | 268.54 | 356.75 | 416.91 | +48.8% |
  | ReadPressureFrame control | - | - | 15.39 | 16.02 | 15.98 | 15.59 | +0.5% |
  | Thermal.Update | - | - | 184.5 | 201.5 | 235.6 | 237.6 | +22.6% |
  | Thermal.UpdateWithMovingSun | - | - | 233.3 | 238.8 | 275.0 | 270.7 | +15.6% |
  | Coupled 100x100, one tick | - | - | 814.2 | 810.9 | 965.4 | 965.5 | +18.8% |
  | Coupled 100x100, eight ticks | - | - | 5914.4 | 6201.4 | 7051.1 | 6848.1 | +14.7% |
  | Four dense rooms, update | - | - | 66.66 | 68.47 | 77.55 | 75.85 | +13.5% |
  | Four dense rooms, rebuild control | - | - | 124.19 | 129.86 | 120.17 | 120.75 | -5.2% |
  | Four sparse rooms, update | - | - | 150.35 | 153.52 | 182.40 | 178.70 | +18.8% |
  | Four sparse rooms, rebuild control | - | - | 882.48 | 834.41 | 856.07 | 839.22 | -1.3% |

  Changes compare arithmetic means of the candidate and baseline run means,
  not pooled statistical estimates. Results have drift and some bimodal-
  distribution warnings, but the large slowdowns repeat against both baselines:
  both candidate advection runs were at least 29% slower than the slower baseline,
  and both candidate atmosphere runs at least 32% slower. Many other consumers
  also exceeded the 10% regression budget, while no intended workload demonstrated
  a 10% improvement. The rejection was therefore not based on small control
  fluctuations or an isolated microbenchmark. These regressions belong to the
  rejected candidate and were not introduced into the retained implementation.
- Allocation/result reporting: exact warmed helper and established consumer
  allocation checks passed in both versions. Separate in-process BenchmarkDotNet
  figures were retained, not replaced by those exact checks. Benchmark tables
  were refreshed from A2 statistics for all 25 retained cases: projection 1-6 B,
  airflow 3-6 B, atmosphere/frame reads zero, thermal/four-room updates 1 B,
  dense coupled one/eight ticks 6 B/47 B; topology rebuilds allocate by design.
  No upstream change was needed to retain the existing correct, faster path.
  No dependency fast path was implemented or measured in this experiment.

**Why rejected and when to revisit:** keep both specialized gatherers. The public
path was correct and allocation-free in exact warmed checks, but whole advection
averaged 34.0% more time, projection 22.7-30.0% more, composed airflow 29.4-36.6%
more, atmosphere 48.8% more, and thermal 22.6% more. These are rejected-candidate
costs, not regressions left in the library. Fewer calls at the consumer surface do
not prove less work inside generic neighbourhood construction/copying; these runs
establish the consumer regression, not a separately profiled attribution of every
extra instruction.

Do not repeat the same substitution with the same dependency implementation and
claim a win from an isolated copy benchmark. A new published halo-one fast path,
materially different generic copying implementation, runtime/JIT changes or a new
representative chunk/layout workload could justify revisiting it. Such a change
must again compare complete padded contents, corners, holes and missing-neighbour
zeroes, preserve scratch suffixes and zero warmed allocations, and pass matched
whole-consumer measurements. Publishing an upstream helper alone is not evidence
that adopting it is faster.

## Invalidation-driven publication topology cache

**Outcome:** retained. The implementation, coverage and benchmark harness are in
the commit introducing this section, titled
`Cache atmosphere publication topology after explicit invalidation`.

**Baseline:** production source at `c66ed60`, with the new `PublishInputs`
benchmark and strengthened atmosphere allocation setup used unchanged for both
versions. Before every publication, the publisher compared all source chunk
presence against the pressure frame, then resolved world chunk coordinates into
each quantity's current target buffer during every full copy.

**Candidate:** in
[AtmosphereFrames.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Atmospherics/AtmosphereFrames.cs),
validate topology at initial publication and after explicit invalidation, rather
than on every unchanged publication. Cache a separate integer source-chunk mapping
for each quantity's fixed-topology target enumeration. Both nonempty area and
full invalidation in
[Atmosphere.cs](src/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity/Atmospherics/Atmosphere.cs)
signal the publisher; empty areas remain no-ops.

- Mappings use public APIs only. Building them enumerates each target grid and
  resolves its coordinates to stable compiled source chunk IDs. Full copies then
  enumerate fresh target current-buffer views and reacquire source spans by ID.
  No writable span, buffered chunk snapshot, assumed cross-grid chunk index,
  internal storage access or upstream API change is retained.
- A topology mismatch creates replacement grids and mappings locally. All quantity
  copies finish before any swap or installation. Only successful publication
  installs writer-only mappings and the matching reader-visible grid generation,
  updates counters and clears pending validation. Copy failures propagate; old
  frames/counters remain published, and retry needs no second invalidation.
  Matching occupancy after property-only invalidation reuses existing grids and
  mappings. Retained empty source chunks do not create empty published chunks.
- The explicit live-edit contract is authoritative: same-chunk additions/removals,
  last-cell removals and new chunks must all be reported through invalidation.
  A layout version is not an occupancy revision. Every quantity is still copied
  in full on every publication, including zero-step updates. Triple-buffer lease
  rotation is unchanged, and topology replacement never mutates an old leased
  grid. Independent quantities still do not form an atomic multi-lease snapshot.
  Partial/dirty-only publication remains deliberately unimplemented: rotating
  slots contain different histories and would need per-slot generation tracking.

### Workloads and matched measurements

The primary workload was selected before timing: the existing fully open 100x100
atmosphere, continuously receiving gas at (50,50), updated with `TimeSpan.Zero`.
Each `PublishInputs` call verifies that no physics tick or frame step-count advance
occurs; it still applies a real input, finalizes derived quantities and copies all
eight published quantities over sixteen 32x32 chunks. This is a complete supported
consumer update, not an isolated copy benchmark or empty-world case.
The normal one-tick atmosphere update, dense coupled one/eight ticks, dense/sparse
four-room updates and topology rebuilds supplied consumer comparisons; thermal and
frame-reading methods supplied controls.

Four sequential Release runs used A1 -> B1 -> B2 -> A2, identical harness,
dependencies and MediumRun/InProcessNoEmit configuration. A1/A2 used preserved
baseline binaries; B1/B2 used the retained runtime candidate. No build, test or
other benchmark was run concurrently. Each run executed all eleven LowFidelity
cases successfully. The baseline runtime DLL was preserved with SHA256
`526C5544D487151D00B0D91D906583317264C2F4F7B6CB987681B0828CDA9F9B`.

| Workload (all times us) | A1 | A2 | B1 | B2 | Mean time change |
|------------------------|---:|---:|---:|---:|-----------------:|
| Atmosphere.PublishInputs | 30.07 | 28.94 | 25.10 | 25.81 | -13.7% |
| Atmosphere.Update | 270.49 | 286.50 | 283.63 | 280.96 | +1.4% |
| ReadPressureFrame control | 16.08 | 16.65 | 16.60 | 16.65 | +1.6% |
| Thermal.Update control | 197.1 | 209.9 | 213.7 | 213.2 | +4.9% |
| Thermal.UpdateWithMovingSun control | 227.6 | 255.7 | 248.0 | 253.3 | +3.7% |
| Coupled 100x100, one tick | 793.3 | 892.4 | 875.6 | 883.1 | +4.3% |
| Coupled 100x100, eight ticks | 6183.1 | 6261.7 | 6244.0 | 6437.6 | +1.9% |
| Four dense rooms, update | 71.77 | 70.69 | 71.96 | 69.14 | -1.0% |
| Four dense rooms, rebuild | 127.15 | 124.94 | 126.31 | 133.57 | +3.1% |
| Four sparse rooms, update | 148.28 | 152.62 | 149.25 | 152.68 | +0.3% |
| Four sparse rooms, rebuild | 914.82 | 856.79 | 858.22 | 888.53 | -1.4% |

The primary workload improved by 16.5% for A1/B1 and 10.8% for A2/B2. Both
candidate means also beat the faster baseline by more than 10%. Its reported
BenchmarkDotNet mean/error pairs were A1 30.07/2.543 us, A2 28.94/0.441 us,
B1 25.10/0.929 us and B2 25.81/0.753 us; candidate and baseline error intervals
did not overlap. The arithmetic mean of the two candidate run means was 13.7%
lower than the baseline pair.

This is not a demonstrated full-physics speedup. Normal atmosphere updates
averaged 1.4% more time, one/eight-tick coupled updates 4.3%/1.9% more, and the
unchanged thermal control 4.9% more. Baseline one-tick coupled means themselves
drifted from 793.3 to 892.4 us, so the initial candidate-versus-A1 difference is
not a reproducible greater-than-10% regression. No other workload confirmed such
a regression across the matched runs. These observed costs and host drift are
reported, not subtracted or hidden behind the primary win.

### Correctness, allocations and final reporting

- Ten additional unit cases bring the retained full solution to 555 passing tests
  in both Debug and Release, with zero build warnings/errors. Direct publisher
  coverage includes every quantity across repeated buffer rotations, source-layer
  storage swaps, noncontiguous source chunk mappings, retained empty chunks,
  removal of the final cell, empty frames and later regrowth. A reader task holds
  an old lease across repeated publications with and without topology replacement.
- Injected mid-copy failures cover unchanged frames, matching-topology invalidation
  and staged topology replacement. They verify old values/topology/counters remain
  published and retry installs correct frames without another invalidation. The
  existing live-edit integration suite additionally covers same-chunk changes,
  holes, new chunks, all-quantity publication, empty/full/area invalidation,
  metadata failure/retry, zero-step finalization and allocations after growth.
- Exact warmed tests require zero current-thread managed bytes across 100
  publications with unchanged topology, both with and without property-only
  invalidation. Atmosphere benchmark setup warms 100 real-tick and 100 zero-step
  input updates, then requires exactly zero bytes over another 100 of each. All
  four runs passed, as did existing consumer allocation gates. New integer arrays
  are allocated only for initial/replacement frame topology, not unchanged or
  matching-topology invalidations; they are persistent metadata, not temporary
  pooled buffers.
- Focused publisher scan checklist: zero hits for literal IndexOf/StartsWith/
  EndsWith/Contains comparisons, Substring, cultureless ToLower/ToUpper, chained
  Replace, params, LINQ character predicates/deferred enumeration, sync-over-async,
  ValueTask, stackalloc, string.Format, regex construction, ContainsKey,
  JSON/options construction, HttpClient construction, ToCharArray, string +=,
  ToString, encoding GetBytes and Trim/Split. All 1/1 publisher classes are sealed;
  there are zero unsealed classes. Three explicit array-creation sites are confined
  to grid/mapping creation; no compound string-allocation chain was found.
  No actionable critical, moderate or informational anti-pattern was identified
  in this focused scan; it is not a whole-library audit or substitute for the
  measured allocation/consumer gates.
- After retention, all fifteen core benchmark cases were rerun on the final
  runtime. They are reporting refreshes, not another isolated publisher A/B
  comparison; core implementations did not change. [BENCHMARK.md](BENCHMARK.md)
  uses those core results and LowFidelity B2, retaining its title, class headings
  and tables only. B2 in-process allocations were zero for PublishInputs/frame
  reads, 2 B for atmosphere ticks, 1/3 B for fixed/moving-sun thermal, 6/45 B for
  coupled one/eight ticks and 1 B for four-room updates. Metadata rebuilds allocated
  127985/217870 B for dense/sparse rooms by design. These estimates are recorded
  separately from the exact zero warmed checks, not silently replaced by them.

**Decision and future refactoring:** retain the invalidation signal and safe
integer mappings for the demonstrated publication-heavy workload. Do not claim
the same gain when physics dominates the update. Keep mappings tied to their
specific target generation, reacquire source/target views after swaps, and retain
full-copy retry/lease tests. An automatic occupancy revision, shared index
assumption or dirty-only rotating-slot optimization would be a different design
requiring its own correctness coverage and matched consumer measurements.

## Standalone thermal visualizer timestep

**Outcome:** retain a 10-second clock for the standalone thermal visualizer only.
No production solver, material definition, default simulation clock, atmosphere
clock or coupled stepping configuration changes.

**Baseline:** the solver at `4aeb9d3`, with the standalone visualizer using the
default 0.1-second clock. Three thermal scenarios (solar-only, radiation-only and
solar plus radiation) each generate 100 frames. Every frame advances one simulated
hour, for 100 simulated hours per scenario: 36,000 steps per frame and 10.8 million
solver steps across the three animations. Changing GIF playback delay cannot
reduce this work; lowering the maximum step count would discard simulated time.

**Candidate:** an isolated thermal service provider in the LowFidelity visualizer
[entry point](test/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity.Visualizer/Program.cs)
uses
[ThermalVisualizerClock](test/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity.Visualizer/Thermals/ThermalVisualizerClock.cs)
with a 10-second step. It shares the existing material registry and thermal
settings, but its thermal factory captures this separate clock. The standalone
[ThermalVisualizer](test/Kiyote/Simulations/LowFidelity/Kiyote.Simulations.LowFidelity.Visualizer/Thermals/ThermalVisualizer.cs)
checks that every one-hour update actually advances 360 steps. Atmosphere and
combined simulations continue to resolve the original provider's 0.1-second clock.
The animation length, simulated duration, initial temperatures, map, sunlight,
materials and fixed palette ranges remain unchanged.

### Measurements and numerical checks

Sequential local Release runs used the same host/runtime/dependencies documented
above, without concurrent builds or other simulation comparisons. These are
single stopwatch runs of complete simulation scenarios with temperature/pixel
capture, not BenchmarkDotNet estimates or repeatability guarantees. Each run
recreated the three scenarios from the unchanged visualizer map and support types:
50x20 cells, 499 occupied aluminium cells, 32x32 compiled chunks, initial 294.15 K,
conductivity 2.05 W/K, capacity 24200 J/K, emissivity 0.1, absorptivity 0.15,
solar flux 1361 W/m2, space temperature 2.7 K, north-east sunlight except in the
radiation-only scenario, and radiation disabled for solar-only.

Every update verified its actual step count. Every occupied cell's temperature and
palette byte was captured after every frame, and all temperatures were checked
finite and nonnegative. The three scenarios use the existing fixed palette
minimum/maximum pairs: solar 294.15/600 K, radiation 150/294.15 K, combined
150/400 K. Missing cells remain black.

| Step | Steps across all three scenarios | Elapsed seconds | Purpose |
|-----:|---------------------------------:|----------------:|---------|
| 0.1 s | 10,800,000 | 137.759 | Previous visualizer |
| 1 s | 1,080,000 | 14.299 | Intermediate precision comparison |
| 5 s | 216,000 | 3.300 | Nearby timestep comparison |
| 10 s | 108,000 | 1.869 | Retained visualizer |
| 60 s | 18,000 | 0.701 | Coarser-step comparison; not adopted |

The 10-second run reduced measured scenario time by approximately 73.7x, while
reducing actual steps by exactly 100x. A subsequent run of the actual visualizer
executable, including GIF encoding and all atmosphere/coupled scenarios, produced
all six animations in 5.383 seconds, excluding build time.

The first comparison against 0.1 seconds was **not** visually identical:

| Scenario | Maximum temperature difference, 0.1 s vs 10 s | Mean absolute difference over occupied cells/frames | Maximum grayscale difference |
|----------|--------------------------------------------:|--------------------------------------------------:|-----------------------------:|
| Solar | 22.220 K | 4.258 K | 19 |
| Radiation | 7.563 K | 2.367 K | 14 |
| Combined | 16.595 K | 4.996 K | 17 |

This prompted additional checks rather than accepting speed alone. Moving from
0.1 to just 1 second already produced nearly the same discrepancy (maximum
21.523/7.271/16.240 K respectively). In contrast, 5- versus 10-second results
differed by at most 0.314/0.073/0.070 K and one palette level. This indicated
roundoff accumulation/stagnation in very small single-precision updates, not just
larger-step truncation error.

A separate scalar double-precision implementation cross-checked the same
four-cardinal-neighbour conduction and conduction-then-radiation update at a
one-second step. It used the same occupied-cell adjacency, public vacuum flags
for exposed face counts, capacity/conductivity/emissivity/space constants, and
solar powers recovered through the public thermal simulation from a zero-
temperature, radiation-disabled ten-second first step. That initial uniform field
has no conduction gradient, so its temperature rise isolates absorbed solar
power; recovery still contains the source float rounding and is not an exact
analytic solar calculation. The reference used double accumulation over all
100 simulated hours and converted only captured temperatures back to float.
No conduction limiter activates at these tested steps for this material.

| Scenario | 0.1 s maximum error vs double reference | 10 s maximum error vs double reference | 10 s mean absolute error | 10 s maximum grayscale difference |
|----------|---------------------------------------:|--------------------------------------:|-------------------------:|----------------------------------:|
| Solar | 22.223 K | 0.292 K | 0.0368 K | 1 |
| Radiation | 7.577 K | 0.051 K | 0.0100 K | 1 |
| Combined | 16.623 K | 0.055 K | 0.0092 K | 1 |

These checks support accepting the changed pixels rather than trying to preserve
the previous reference's numerical drift. They do not constitute an analytic
solution or a general timestep-stability guarantee.

### Validation and scope

- The actual executable generated all six 50x20, 100-frame GIFs successfully.
  Every frame decoded, and every pixel of the three thermal GIFs exactly matched
  the captured 10-second comparison output.
- Debug and Release solution builds completed with zero warnings/errors. Every
  standalone thermal frame passed its full-simulated-duration step-count check.
  The comparison code/binary data were temporary verification tools, not a new
  runtime dependency, benchmark suite or production double-precision solver.
- The 60-second trial also remained close to the double reference (maximum
  solar/radiation/combined errors 0.078/0.042/0.112 K, at most one palette level).
  It was not adopted: 10 seconds already removes the minutes-long run while
  retaining a smaller step for nonlinear radiation and split integration.
- No claim is made that ten seconds is appropriate for atmosphere, convection,
  other materials, lower capacities, higher conductivities or different
  temperatures. Revalidate stability, limiters, temperature fields and palettes
  if those visualizer inputs change. Do not globally enlarge the shared clock or
  reduce the simulated duration to reproduce this speedup.
