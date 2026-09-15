using System.Linq;
using ExhaustiveMatching.Analyzer.Enums.Analysis;
using ExhaustiveMatching.Analyzer.Enums.Semantics;
using ExhaustiveMatching.Analyzer.Enums.Syntax;
using ExhaustiveMatching.Analyzer.Enums.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer.Enums
{
    internal class EnumSwitchStatementAnalyzer
    {
        public static void Analyze(SyntaxNodeAnalysisContext context, SwitchStatementSyntax switchStatement)
        {
            if (!IsExhaustive(context, switchStatement)) return;

            ReportWhenGuardNotSupported(context, switchStatement);

            var switchOnType = context.GetExpressionConvertedType(switchStatement.Expression);

            if (switchOnType != null && switchOnType.IsEnum(context, out var enumType, out var nullable))
                AnalyzeSwitchOnEnum(context, switchStatement, enumType, nullable);

            // TODO report warning that throws invalid enum isn't checked for exhaustiveness
        }

        private static void ReportWhenGuardNotSupported(
            SyntaxNodeAnalysisContext context,
            SwitchStatementSyntax switchStatement)
        {
            foreach (var label in switchStatement.Labels()
                .OfType<CasePatternSwitchLabelSyntax>())
            {
                if (label.WhenClause != null)
                    Diagnostics.ReportWhenClauseNotSupported(
                        context,
                        label.WhenClause);
            }
        }

        private static bool IsExhaustive(
            SyntaxNodeAnalysisContext context,
            SwitchStatementSyntax switchStatement)
        {
            // If there is no default section or it doesn't throw, we assume the
            // dev doesn't want an exhaustive match
            var thrownType = switchStatement.DefaultSection()
                ?.FirstThrowStatement()
                ?.ThrowsType(context);

            return thrownType?.IsInvalidEnumArgumentException() == true
                   || thrownType?.IsExhaustiveMatchFailedException() == true;
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
                pattern => Diagnostics.ReportCasePatternNotSupported(
                    context,
                    pattern));

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
                    (left, right) => new EnumPatternCoverage(
                        left.Values.Concat(right.Values),
                        left.CoversNull || right.CoversNull,
                        left.IsKnown && right.IsKnown));

            // If null were not required, and there were a null case, that would already be a compile error
            if (nullRequired && !coverage.CoversNull)
                Diagnostics.ReportNotExhaustiveNullableEnumSwitch(context, switchStatement);

            var unusedSymbols = SwitchOnEnumAnalyzer.UnusedEnumValues(
                enumType,
                coverage);
            Diagnostics.ReportNotExhaustiveEnumSwitch(context, switchStatement, unusedSymbols);
        }
    }
}
