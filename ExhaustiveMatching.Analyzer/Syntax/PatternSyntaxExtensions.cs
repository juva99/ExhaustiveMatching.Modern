using ExhaustiveMatching.Analyzer.Enums.Syntax;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveMatching.Analyzer.Syntax
{
    internal static class PatternSyntaxExtensions
    {
        public static bool IsNullPattern(this PatternSyntax pattern)
        {
            switch (pattern)
            {
                case ConstantPatternSyntax constantPattern:
                    return constantPattern.Expression.IsNullConstantExpression();
                case ParenthesizedPatternSyntax parenthesized:
                    return parenthesized.Pattern.IsNullPattern();
                default:
                    return false;
            }
        }
    }
}
