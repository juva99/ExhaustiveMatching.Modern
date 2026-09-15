using System.Collections.Generic;
using System.Linq;
using ExhaustiveMatching.Analyzer.Enums.Analysis;
using ExhaustiveMatching.Analyzer.Enums.Semantics;
using ExhaustiveMatching.Analyzer.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer
{
    internal static class SwitchStatementAnalyzer
    {
        public static void Analyze(
            SyntaxNodeAnalysisContext context,
            SwitchStatementSyntax switchStatement)
        {
            var switchKind = IsExhaustive(context, switchStatement);
            if (!switchKind.IsExhaustive) return;

            ReportWhenGuardNotSupported(context, switchStatement);

            var switchOnType = context.GetExpressionConvertedType(switchStatement.Expression);

            if (switchOnType != null
                && switchOnType.IsEnum(context, out var enumType, out var nullable))
                AnalyzeSwitchOnEnum(context, switchStatement, enumType, nullable);
            else if (!switchKind.ThrowsInvalidEnum)
                AnalyzeSwitchOnClosed(context, switchStatement, switchOnType);

            // TODO report warning that throws invalid enum isn't checked for exhaustiveness
        }

        private static SwitchStatementKind IsExhaustive(
            SyntaxNodeAnalysisContext context,
            SwitchStatementSyntax switchStatement)
        {
            var defaultSection = switchStatement.Sections
                .FirstOrDefault(s => s.Labels.OfType<DefaultSwitchLabelSyntax>().Any());

            var throwStatement = defaultSection?.Statements
                                    .OfType<ThrowStatementSyntax>().FirstOrDefault();

            // If there is no default section or it doesn't throw, we assume the
            // dev doesn't want an exhaustive match
            if (throwStatement == null)
                return new SwitchStatementKind(false, false);

            return ExpressionAnalyzer.SwitchStatementKindForThrown(context,
                throwStatement.Expression);
        }

        private static void ReportWhenGuardNotSupported(
            SyntaxNodeAnalysisContext context,
            SwitchStatementSyntax switchStatement)
        {
            var patternLabels = switchStatement.Sections.SelectMany(s => s.Labels)
                                               .OfType<CasePatternSwitchLabelSyntax>();
            foreach (var patternLabel in patternLabels)
                if (patternLabel.WhenClause != null)
                    context.ReportWhenClauseNotSupported(patternLabel.WhenClause);
        }

        private static void AnalyzeSwitchOnEnum(
            SyntaxNodeAnalysisContext context,
            SwitchStatementSyntax switchStatement,
            INamedTypeSymbol enumType,
            bool nullRequired)
        {
            var evaluator = new EnumPatternCoverageEvaluator(
                context,
                enumType,
                nullRequired,
                pattern => context.ReportCasePatternNotSupported(pattern));

            var coverage = switchStatement.Sections
                .SelectMany(s => s.Labels)
                .Where(label => !(label is DefaultSwitchLabelSyntax))
                .Where(label => !(label is CasePatternSwitchLabelSyntax pattern
                                  && pattern.WhenClause != null))
                .Select(evaluator.EvaluateLabel)
                .Aggregate(
                    new EnumPatternCoverage(
                        Enumerable.Empty<object>(),
                        coversNull: false,
                        isKnown: true),
                    CombineCoverage);

            if (nullRequired && !coverage.CoversNull)
                Diagnostics.ReportNotExhaustiveNullableEnumSwitch(context, switchStatement);

            var unusedSymbols = SwitchOnEnumAnalyzer.UnusedEnumValues(
                enumType,
                coverage);
            Diagnostics.ReportNotExhaustiveEnumSwitch(context, switchStatement, unusedSymbols);
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
            SwitchStatementSyntax switchStatement,
            ITypeSymbol type)
        {
            if (type == null)
                return;

            var switchLabels = switchStatement
                .Sections.SelectMany(s => s.Labels)
                .Where(label => !(label is DefaultSwitchLabelSyntax))
                .Where(label => !(label is CasePatternSwitchLabelSyntax pattern
                                  && pattern.WhenClause != null))
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

            var coverage = switchLabels
                .Select(switchLabel => switchLabel.GetCoverage(
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
                context.ReportOpenTypeNotSupported(type, switchStatement.Expression);
                return; // No point in trying to check for uncovered types, this isn't closed
            }

            var uncoveredTypes = allConcreteTypes
                .Where(t => !coverage.Types.Contains(t))
                .ToArray();

            context.ReportNotExhaustiveObjectSwitch(switchStatement.SwitchKeyword, uncoveredTypes);
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
