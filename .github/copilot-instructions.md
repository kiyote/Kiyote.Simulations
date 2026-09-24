# Copilot Instructions

## Project Guidelines
- Prefer IDE-aware tools (the editor's edit/build/diagnostics tooling) over terminal commands when working in this workspace; only use the terminal for operations the IDE tooling cannot perform.
- Prefer jagged arrays (T[][]) over multidimensional arrays (T[,]) in C# code, to satisfy CA1814 and match project convention.
- When facing ambiguous design decisions (e.g. whether to change existing structures/types, add generic type parameters, or introduce new abstractions), stop and ask clarifying questions rather than guessing or investigating extensively; working it out together is faster.

## Visualization Strategy
- In the Kiyote.Simulations solution, visualizer demo classes (Kiyote.Simulations.Visualizer project) mirror the engine's generic struct-strategy pattern: pluggable behaviors (diffusion, boundary, sampling, callback strategies) are implemented as readonly structs passed as generic type parameters for JIT devirtualization, styled consistently across GridDiffusion and GridFluid visualizer folders.

## User Preferences
- In the Kiyote.Simulations project, each simulation interface (e.g., IGridDiffusion, IGridPressure) should expose the caller-facing method to advance the simulation named 'Update', for consistency across simulations.
