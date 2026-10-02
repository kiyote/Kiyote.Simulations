// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage( "Naming", "CA1707:Identifiers should not contain underscores", Justification = "Tests are named Method_Condition_Expectation", Scope = "module" )]
[assembly: SuppressMessage( "Performance", "CA1812:An internal (assembly-level) type is never instantiated.", Justification = "Dependency Injection gives this rule fits", Scope = "module" )]
[assembly: SuppressMessage( "Security", "CA5394:Do not use insecure randomness", Justification = "We're not creating a security library here", Scope = "module" )]
[assembly: SuppressMessage( "Style", "IDE0130:Namespace does not match folder structure ", Justification = "Test name belongs at end of namespace to prevent namespace bloat", Scope = "module" )]
[assembly: SuppressMessage( "Maintainability", "CA1515:Consider making public types internal", Justification = "Code not released, doesn't mattter.", Scope = "module" )]
