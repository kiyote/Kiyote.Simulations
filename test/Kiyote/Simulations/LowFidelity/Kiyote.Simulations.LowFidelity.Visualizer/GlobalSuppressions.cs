// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage( "Performance", "CA1812:An internal (assembly-level) type is never instantiated.", Justification = "Dependency Injection gives this rule fits", Scope = "module" )]
[assembly: SuppressMessage( "Style", "IDE0130:Namespace does not match folder structure", Justification = "I like to organize for easier imports.", Scope = "module" )]
[assembly: SuppressMessage( "Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Testing doesn't need localization at this time.", Scope = "module" )]
