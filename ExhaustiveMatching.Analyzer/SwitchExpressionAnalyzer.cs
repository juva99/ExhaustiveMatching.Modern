using System.Linq;
using ExhaustiveMatching.Analyzer.Enums.Analysis;
using ExhaustiveMatching.Analyzer.Enums.Semantics;
using ExhaustiveMatching.Analyzer.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer
{
    internal static class SwitchExpressionAnalyzer
    {
        public static void Analyze(
            SyntaxNodeAnalysisContext context,
            SwitchExpressionSyntax switchExpression)
        {
            var switchKind = IsExhaustive(context, switchExpression);
            if (!switchKind.IsExhaustive) return;

            ReportWhenGuardNotSupported(context, switchExpression);

            var switchOnType = context.GetExpressionConvertedType(switchExpression.GoverningExpression);

            if (switchOnType != null
                && switchOnType.IsEnum(context, out var enumType, out var nullable))
                AnalyzeSwitchOnEnum(context, switchExpression, enumType, nullable);
            else if (!switchKind.ThrowsInvalidEnum)
                AnalyzeSwitchOnClosed(context, switchExpression, switchOnType);

            // TODO report warning that throws invalid enum isn't checked for exhaustiveness
        }

        private static SwitchStatementKind IsExhaustive(
            SyntaxNodeAnalysisContext context,
            SwitchExpressionSyntax switchExpression)
        {
            var discardArm = switchExpression.Arms.LastOrDefault(
                a => IsExhaustiveFallbackPattern(a.Pattern));

            // If there is no unguarded fallback arm or it doesn't throw, we
            // assume the dev doesn't want an exhaustive match.
            if (discardArm?.Expression is ThrowExpressionSyntax throwExpression)
                return ExpressionAnalyzer.SwitchStatementKindForThrown(context, throwExpression.Expression);

            return new SwitchStatementKind(false, false);
        }

        private static void ReportWhenGuardNotSupported(
            SyntaxNodeAnalysisContext context,
            SwitchExpressionSyntax switchExpression)
        {
            foreach (var arm in switchExpression.Arms)
                if (arm.WhenClause != null)
                    context.ReportWhenClauseNotSupported(arm.WhenClause);
        }

        private static void AnalyzeSwitchOnEnum(
            SyntaxNodeAnalysisContext context,
            SwitchExpressionSyntax switchExpression,
            INamedTypeSymbol enumType,
            bool nullRequired)
        {
            var discardArm = switchExpression.Arms.LastOrDefault(
                a => IsExhaustiveFallbackPattern(a.Pattern));
            var evaluator = new EnumPatternCoverageEvaluator(
                context,
                enumType,
                nullRequired,
                pattern => context.ReportCasePatternNotSupported(pattern));

            var coverage = switchExpression.Arms
                .Where(a => !ReferenceEquals(a, discardArm))
                .Where(a => a.WhenClause == null)
                .Select(a => evaluator.EvaluatePattern(a.Pattern))
                .Aggregate(
                    new EnumPatternCoverage(
                        Enumerable.Empty<object>(),
                        coversNull: false,
                        isKnown: true),
                    CombineCoverage);

            if (nullRequired && !coverage.CoversNull)
                Diagnostics.ReportNotExhaustiveNullableEnumSwitch(context, switchExpression);

            var unusedSymbols = SwitchOnEnumAnalyzer.UnusedEnumValues(
                enumType,
                coverage);
            Diagnostics.ReportNotExhaustiveEnumSwitch(context, switchExpression, unusedSymbols);
        }

        private static EnumPatternCoverage CombineCoverage(
            EnumPatternCoverage left,
            EnumPatternCoverage right)
        {
            return new EnumPatternCoverage(
                left.Values.Concat(right.Values),
                left.CoversNull || right.CoversNull,
                left.IsKnown && right.IsKnown);
        }

        private static void AnalyzeSwitchOnClosed(
            SyntaxNodeAnalysisContext context,
            SwitchExpressionSyntax switchExpression,
            ITypeSymbol type)
        {
            if (type == null)
                return;

            var discardArm = switchExpression.Arms.LastOrDefault(
                a => IsExhaustiveFallbackPattern(a.Pattern));
            var patterns = switchExpression.Arms
                .Where(a => !ReferenceEquals(a, discardArm))
                .Where(a => a.WhenClause == null)
                .Select(a => a.Pattern)
                .ToList();

            var closedAttributeType = context.GetClosedAttributeType();
            var isClosed = type.HasAttribute(closedAttributeType);

            var allCases = type.GetClosedTypeCases(closedAttributeType);
            var allConcreteTypes = allCases
                .Where(t => t.IsConcreteOrLeaf(closedAttributeType));

            if (!isClosed && type.TryGetStructurallyClosedTypeCases(context, out allCases))
            {
                isClosed = true;
                allConcreteTypes = allCases
                    .Where(t => t.IsConcrete());
            }

            var coverage = patterns
                .Select(pattern => pattern.GetCoverage(
                    context,
                    type,
                    allCases,
                    allConcreteTypes,
                    isClosed))
                .Aggregate(
                    ClosedPatternCoverage.Known(
                        Enumerable.Empty<ITypeSymbol>(),
                        canBeNegated: true,
                        isAlwaysFalse: true),
                    CombineCoverage);

            // If it is an open type, we don't want to actually check for uncovered types, but
            // we still needed to check the switch cases
            if (!isClosed)
            {
                context.ReportOpenTypeNotSupported(type, switchExpression.GoverningExpression);
                return; // No point in trying to check for uncovered types, this isn't closed
            }

            var uncoveredTypes = allConcreteTypes
                .Where(t => !coverage.Types.Contains(t))
                .ToArray();

            context.ReportNotExhaustiveObjectSwitch(switchExpression.SwitchKeyword, uncoveredTypes);
        }

        private static bool IsExhaustiveFallbackPattern(PatternSyntax pattern)
        {
            while (pattern is ParenthesizedPatternSyntax parenthesized)
                pattern = parenthesized.Pattern;

            return pattern is DiscardPatternSyntax || pattern is VarPatternSyntax;
        }

        private static ClosedPatternCoverage CombineCoverage(
            ClosedPatternCoverage left,
            ClosedPatternCoverage right)
        {
            return ClosedPatternCoverage.Create(
                left.Types.Concat(right.Types),
                left.IsKnown && right.IsKnown,
                left.CanBeNegated && right.CanBeNegated,
                left.IsAlwaysTrue || right.IsAlwaysTrue,
                left.IsAlwaysFalse && right.IsAlwaysFalse);
        }
    }
}
