# Copilot Instructions

## Project Guidelines
- Prefer IDE-aware tools (the editor's edit/build/diagnostics tooling) over terminal commands when working in this workspace; only use the terminal for operations the IDE tooling cannot perform.
- Prefer jagged arrays (T[][]) over multidimensional arrays (T[,]) in C# code, to satisfy CA1814 and match project convention.
- When facing ambiguous design decisions (e.g. whether to change existing structures/types, add generic type parameters, or introduce new abstractions), stop and ask clarifying questions rather than guessing or investigating extensively; working it out together is faster.
- In Kiyote.Simulations, prefer API simplicity: expose mutable IGridLayer<T> instances to callers and trust them not to mutate, rather than introducing read-only wrapper types.

## Simulation Behavior
- In Kiyote.Simulations, float-specific simulations (e.g. IFloatGridDiffusion) run on dense FloatFields compiled from IGrid<TCell> leaves. They assume the topology never changes during a simulation and do no version checks, rebinding, or remapping. Before the topology changes, the caller copies values back into TCell (Decompile). Simulation settings are injected through the implementation's constructor, not passed to Update.
- In Kiyote.Simulations, simulations should be stateless: per-grid scratch buffers (e.g. projection divergence) are caller-owned and passed into Update alongside the grids, rather than cached inside the simulation instance.
- In Kiyote.Simulations.LowFidelity, simulations are advanced manually with the game time elapsed since the last call (to support 1x–16x game speed). TCell is responsible for persisting simulation values (e.g. for saves), so values must be written back to cells between simulation ticks.
- In the Kiyote.Simulations project, each simulation interface (e.g., IGridDiffusion, IGridPressure) should expose the caller-facing method to advance the simulation named 'Update', for consistency across simulations.
- In Kiyote.Simulations.LowFidelity Atmospherics, the simulation is fed the ship's grids, compiles its own compiled grid, and owns all its layers; gas permeability is a binary per-cell attribute read from the attached grids (no partial permeability). Gases are defined as GasDefinition records registered at application initialization (modding support) instead of an enum.

## Visualization Strategy
- In the Kiyote.Simulations solution, visualizer demo classes (Kiyote.Simulations.Visualizer project) mirror the engine's generic struct-strategy pattern: pluggable behaviors (diffusion, boundary, sampling, callback strategies) are implemented as readonly structs passed as generic type parameters for JIT devirtualization, styled consistently across GridDiffusion and GridFluid visualizer folders.
