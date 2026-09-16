# Changelog

## 1.0.1 - 2026-09-16

- Added .NET 8 support alongside .NET 10, including runtime and Roslyn 4.8
  compatibility coverage.

## 1.0.0 - 2026-09-15

- Forked and renamed the package to `ExhaustiveMatching.Modern`.
- Updated the build and tests to .NET 10, C# 14, and Roslyn 5.0.
- Added coverage analysis for modern logical and parenthesized patterns.
- Added conservative handling for relational, recursive, property, positional,
  list, and slice patterns.
- Fixed guarded patterns incorrectly counting toward exhaustive coverage.
- Fixed nullable enum null-pattern handling and 64-bit enum analysis.
- Preserved the `ExhaustiveMatching` runtime namespace for source compatibility.
