# ExhaustiveMatching.Modern

`ExhaustiveMatching.Modern` is a Roslyn analyzer that reports missing cases in
C# switch statements and switch expressions. It supports enums and closed class
or interface hierarchies, including modern logical patterns such as `or`,
`and`, `not`, and parenthesized patterns.

This project is a maintained, modernized fork of
[WalkerCodeRanger/ExhaustiveMatching](https://github.com/WalkerCodeRanger/ExhaustiveMatching).
It keeps the existing `ExhaustiveMatching` namespace so applications can migrate
without changing their source API.

## Install

```powershell
dotnet add package ExhaustiveMatching.Modern
```

Install the package in every project that contains exhaustive switches or
declares, inherits, or implements types marked with `ClosedAttribute`.

Version 1.0.0 uses the .NET 10 SDK, C# 14, and Microsoft.CodeAnalysis 5.0. The
runtime API targets .NET Standard 2.0. Analyzer hosts must support Roslyn 5.0 or
newer.

## Quick start

Mark a switch as intentionally exhaustive by throwing the exception returned by
`ExhaustiveMatch.Failed` from its fallback case:

```csharp
using ExhaustiveMatching;

public enum CoinFlip
{
    Heads,
    Tails,
}

// EM0001: Enum value not handled by switch: Tails
var result = coinFlip switch
{
    CoinFlip.Heads => "Heads!",
    _ => throw ExhaustiveMatch.Failed(coinFlip),
};
```

The analyzer understands logical enum patterns:

```csharp
var category = coinFlip switch
{
    CoinFlip.Heads or CoinFlip.Tails => "Known outcome",
    _ => throw ExhaustiveMatch.Failed(coinFlip),
};
```

## Closed type hierarchies

Use `ClosedAttribute` to define the direct cases of a class or interface:

```csharp
using ExhaustiveMatching;

[Closed(typeof(Circle), typeof(Rectangle), typeof(Triangle))]
public abstract class Shape;

public sealed class Circle : Shape;
public sealed class Rectangle : Shape;
public sealed class Triangle : Shape;
```

The analyzer reports any concrete case not covered by the switch:

```csharp
// EM0003: Subtype not handled by switch: Triangle
var area = shape switch
{
    Circle circle => CalculateArea(circle),
    Rectangle rectangle => CalculateArea(rectangle),
    _ => throw ExhaustiveMatch.Failed(shape),
};
```

Logical patterns may cover multiple cases:

```csharp
var kind = shape switch
{
    Circle or Rectangle => "round or rectangular",
    Triangle => "triangular",
    _ => throw ExhaustiveMatch.Failed(shape),
};
```

A case may also cover a closed branch higher in a nested hierarchy. All concrete
leaf types below that branch count as handled.

## Supported patterns

The analyzer evaluates patterns against the finite set of declared enum values
or concrete leaves in a closed hierarchy.

| Pattern | Enum | Closed hierarchy |
|---|---:|---:|
| Constant and type/declaration | Yes | Yes |
| `or` | Yes | Yes |
| `and` | Yes | Yes |
| `not` | Yes | When the complement is provably safe |
| Parenthesized | Yes | Yes |
| Relational | Yes | No |
| `null` | Yes | Ignored by design |
| `var` and discard | Yes | Yes |
| Property/positional/recursive | N/A | When constraints are provably total |
| List and slice | Conservative | Conservative |

Patterns whose coverage cannot be proven produce `EM0101` and do not suppress a
missing-case diagnostic. A `when` guard produces `EM0100` and its pattern does
not count toward exhaustiveness because the guard may be false.

For nullable enums, `null` must be handled. For closed reference hierarchies,
the analyzer preserves the original behavior and does not require a null case.

## Exhaustive switch statements

Switch statements use the same analysis when their `default` section throws:

```csharp
switch (shape)
{
    case Circle or Rectangle:
        RenderSimpleShape(shape);
        break;
    case Triangle triangle:
        RenderTriangle(triangle);
        break;
    default:
        throw ExhaustiveMatch.Failed(shape);
}
```

The analyzer also recognizes the original enum convention using
`InvalidEnumArgumentException`.

## Diagnostics

| ID | Description |
|---|---|
| EM0001 | An enum switch is missing a declared value |
| EM0002 | A nullable enum switch is missing a null case |
| EM0003 | A closed-type switch is missing a concrete subtype |
| EM0011 | A direct concrete subtype is missing from its parent's `Closed` cases |
| EM0012 | A listed case is an indirect rather than direct subtype |
| EM0013 | A listed case is not a subtype |
| EM0014 | A concrete subtype is not covered by a closed case |
| EM0015 | A direct open interface is missing from its parent's `Closed` cases |
| EM0100 | A guarded case cannot prove exhaustive coverage |
| EM0101 | A case pattern cannot be analyzed safely |
| EM0102 | The switched type is neither an enum nor closed |
| EM0103 | A case type is outside the closed hierarchy |
| EM0104 | A type has duplicate `Closed` attributes |
| EM0105 | A `Closed` attribute lists a case more than once |

## Building

The repository is pinned to the .NET 10 SDK:

```powershell
dotnet test ExhaustiveMatch.sln --configuration Release
dotnet pack ExhaustiveMatching.Analyzer\ExhaustiveMatching.Analyzer.csproj `
  --configuration Release `
  --output artifacts
```

## License and attribution

This project remains licensed under the
[BSD 3-Clause License](LICENSE). The original copyright notice and license
conditions are retained. The original author's and contributors' names are not
used to endorse this fork.
