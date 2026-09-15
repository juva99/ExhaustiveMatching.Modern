using System;
using System.Collections.Generic;
using System.Linq;
using ExhaustiveMatching.Analyzer.Enums.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer.Enums.Analysis
{
    public static class SwitchOnEnumAnalyzer
    {
        /// <summary>
        /// Figure out which enum values are unused.
        /// </summary>
        /// <remarks>Cases in a switch on an enum type can be actual enum values, but they can also
        /// be integer values etc. To handle that, checking for unused values must be done on the
        /// numeric value of the enum values.</remarks>
        public static IEnumerable<ISymbol> UnusedEnumValues(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enumType,
            IEnumerable<ExpressionSyntax> caseExpressions)
        {
            var evaluator = new EnumPatternCoverageEvaluator(
                context,
                enumType,
                nullable: false,
                reportUnsupported: _ => { });

            var coverage = caseExpressions
                .Select(e => evaluator.EvaluateExpression(e))
                .Aggregate(
                    new EnumPatternCoverage(
                        Enumerable.Empty<object>(),
                        coversNull: false,
                        isKnown: true),
                    (left, right) => new EnumPatternCoverage(
                        left.Values.Concat(right.Values),
                        left.CoversNull || right.CoversNull,
                        left.IsKnown && right.IsKnown));

            return UnusedEnumValues(enumType, coverage);
        }

        /// <summary>
        /// Figure out which enum values are not covered by a pattern result.
        /// </summary>
        public static IEnumerable<ISymbol> UnusedEnumValues(
            INamedTypeSymbol enumType,
            EnumPatternCoverage coverage)
        {
            var underlyingType = enumType.EnumUnderlyingType.SpecialType.ToTypeCode();
            var allSymbols = enumType.GetMembers().OfType<IFieldSymbol>();

            foreach (var symbol in allSymbols)
            {
                if (!symbol.IsConst || symbol.ConstantValue == null)
                    continue;

                object value;
                try
                {
                    value = Convert.ChangeType(symbol.ConstantValue, underlyingType);
                }
                catch
                {
                    continue;
                }

                if (!coverage.Values.Contains(value))
                    yield return symbol;
            }
        }

    }
}
