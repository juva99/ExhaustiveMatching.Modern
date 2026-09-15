# Versioning for `ExhaustiveMatching.Modern`

`ExhaustiveMatching.Modern` follows semantic versioning. The package version is
independent of the Roslyn package version.

## Compatibility policy

The runtime API continues to target .NET Standard 2.0. Analyzer releases may
raise their minimum Roslyn host version when a newer C# syntax model is needed.
Such a change is documented in the release notes.

| Package | Build SDK | Language | Roslyn | Runtime target |
|---|---|---|---|---|
| 1.0.x | .NET 10 | C# 14 | 5.0 | .NET Standard 2.0 |

Patch releases contain compatible fixes. Minor releases add compatible
analysis capabilities. Major releases may change the public runtime API,
diagnostic contract, or minimum analyzer host.
