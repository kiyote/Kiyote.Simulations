# Copilot Instructions

## Project Guidelines
- Prefer IDE-aware tools (the editor's edit/build/diagnostics tooling) over terminal commands when working in this workspace; only use the terminal for operations the IDE tooling cannot perform.
- When facing ambiguous design decisions (e.g. whether to change existing structures/types, add generic type parameters, or introduce new abstractions), stop and ask clarifying questions rather than guessing or investigating extensively; working it out together is faster.
- Prefer DI-first design: it's fine for classes to be practical to create only through DI (internal class, single constructor, plain AddSingleton registration). Integration tests may build a ServiceCollection; unit tests should mock all dependencies.
