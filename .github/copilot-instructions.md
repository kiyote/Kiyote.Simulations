# Copilot Instructions

## Project Guidelines
- Prefer IDE-aware tools (the editor's edit/build/diagnostics tooling) over terminal commands when working in this workspace; only use the terminal for operations the IDE tooling cannot perform.

## Visualization Strategy
- In the Kiyote.Simulations solution, visualizer demo classes (Kiyote.Simulations.Visualizer project) mirror the engine's generic struct-strategy pattern: pluggable behaviors (diffusion, boundary, sampling, callback strategies) are implemented as readonly structs passed as generic type parameters for JIT devirtualization, styled consistently across GridDiffusion and GridFluid visualizer folders.
