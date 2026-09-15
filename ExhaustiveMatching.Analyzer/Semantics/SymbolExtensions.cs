using Microsoft.CodeAnalysis;

namespace ExhaustiveMatching.Analyzer.Semantics
{
    public static class SymbolExtensions
    {
        public static string GetFullName(this ISymbol symbol)
        {
            if (symbol == null)
                return null;

            if (!(symbol is INamedTypeSymbol))
                return symbol.ToDisplayString(
                    SymbolDisplayFormat.CSharpErrorMessageFormat);

            var ns = symbol.ContainingNamespace;
            return ns != null && !ns.IsGlobalNamespace ? $"{ns.GetFullName()}.{symbol.Name}" : symbol.Name;
        }
    }
}
