using System;
using System.Collections.Generic;
using System.Linq;
using ExhaustiveMatching.Analyzer.Semantics;
using ExhaustiveMatching.Analyzer.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer
{
    internal sealed class ClosedPatternCoverage
    {
        private ClosedPatternCoverage(
            IEnumerable<ITypeSymbol> types,
            bool isKnown,
            bool canBeNegated,
            bool isAlwaysTrue,
            bool isAlwaysFalse)
        {
            Types = new HashSet<ITypeSymbol>(
                types,
                SymbolEqualityComparer.Default);
            IsKnown = isKnown;
            CanBeNegated = canBeNegated;
            IsAlwaysTrue = isAlwaysTrue;
            IsAlwaysFalse = isAlwaysFalse;
        }

        public HashSet<ITypeSymbol> Types { get; }

        public bool IsKnown { get; }

        public bool CanBeNegated { get; }

        public bool IsAlwaysTrue { get; }

        public bool IsAlwaysFalse { get; }

        public static ClosedPatternCoverage Known(
            IEnumerable<ITypeSymbol> types,
            bool canBeNegated,
            bool isAlwaysTrue = false,
            bool isAlwaysFalse = false)
            => new ClosedPatternCoverage(
                types,
                isKnown: true,
                canBeNegated,
                isAlwaysTrue,
                isAlwaysFalse);

        public static ClosedPatternCoverage Create(
            IEnumerable<ITypeSymbol> types,
            bool isKnown,
            bool canBeNegated,
            bool isAlwaysTrue,
            bool isAlwaysFalse)
            => new ClosedPatternCoverage(
                types,
                isKnown,
                canBeNegated,
                isAlwaysTrue,
                isAlwaysFalse);

        public static ClosedPatternCoverage Unknown()
            => new ClosedPatternCoverage(
                Enumerable.Empty<ITypeSymbol>(),
                isKnown: false,
                canBeNegated: false,
                isAlwaysTrue: false,
                isAlwaysFalse: false);
    }

    internal static class PatternAnalyzer
    {
        public static ClosedPatternCoverage GetCoverage(
            this SwitchLabelSyntax switchLabel,
            SyntaxNodeAnalysisContext context,
            ITypeSymbol type,
            HashSet<ITypeSymbol> allCases,
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool isClosed)
        {
            switch (switchLabel)
            {
                case CaseSwitchLabelSyntax labelSyntax:
                    return labelSyntax.Value.GetCoverage(
                        context,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed,
                        labelSyntax);

                case CasePatternSwitchLabelSyntax patternSyntax:
                    return patternSyntax.Pattern.GetCoverage(
                        context,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                default:
                    return ClosedPatternCoverage.Known(
                        Enumerable.Empty<ITypeSymbol>(),
                        canBeNegated: true,
                        isAlwaysFalse: true);
            }
        }

        public static ClosedPatternCoverage GetCoverage(
            this ExpressionSyntax expression,
            SyntaxNodeAnalysisContext context,
            ITypeSymbol type,
            HashSet<ITypeSymbol> allCases,
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool isClosed,
            SyntaxNode diagnosticNode)
        {
            if (expression.IsTypeIdentifier(context, out var typeSymbol))
            {
                return GetTypeCoverage(
                    context,
                    typeSymbol,
                    diagnosticNode,
                    type,
                    allCases,
                    allConcreteTypes,
                    isClosed);
            }

            if (IsNullConstant(context, expression))
            {
                return ClosedPatternCoverage.Known(
                    Enumerable.Empty<ITypeSymbol>(),
                    canBeNegated: true,
                    isAlwaysFalse: true);
            }

            if (diagnosticNode is CaseSwitchLabelSyntax label)
                context.ReportCasePatternNotSupported(label);

            return ClosedPatternCoverage.Unknown();
        }

        public static ClosedPatternCoverage GetCoverage(
            this PatternSyntax pattern,
            SyntaxNodeAnalysisContext context,
            ITypeSymbol type,
            HashSet<ITypeSymbol> allCases,
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool isClosed)
        {
            switch (pattern)
            {
                case ParenthesizedPatternSyntax parenthesized:
                    return parenthesized.Pattern.GetCoverage(
                        context,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                case BinaryPatternSyntax binary:
                    var left = binary.Left.GetCoverage(
                        context,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);
                    var right = binary.Right.GetCoverage(
                        context,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                    if (binary.OperatorToken.IsKind(SyntaxKind.OrKeyword))
                        return Or(left, right);

                    if (binary.OperatorToken.IsKind(SyntaxKind.AndKeyword))
                        return And(left, right);

                    context.ReportCasePatternNotSupported(binary);
                    return ClosedPatternCoverage.Unknown();

                case UnaryPatternSyntax unary:
                    if (!unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
                    {
                        context.ReportCasePatternNotSupported(unary);
                        return ClosedPatternCoverage.Unknown();
                    }

                    return GetNotCoverage(
                        context,
                        unary,
                        unary.Pattern.GetCoverage(
                            context,
                            type,
                            allCases,
                            allConcreteTypes,
                            isClosed),
                        allConcreteTypes);

                case DiscardPatternSyntax _:
                    return All(allConcreteTypes, canBeNegated: true);

                case VarPatternSyntax _:
                    return All(allConcreteTypes, canBeNegated: true);

                case DeclarationPatternSyntax declaration:
                    return GetTypeCoverage(
                        context,
                        context.GetDeclarationType(declaration),
                        declaration,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                case TypePatternSyntax typePattern:
                    return GetTypeCoverage(
                        context,
                        context.SemanticModel.GetTypeInfo(
                                typePattern.Type,
                                context.CancellationToken)
                            .Type,
                        typePattern,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                case ConstantPatternSyntax constant:
                    return GetConstantCoverage(
                        context,
                        constant,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                case RecursivePatternSyntax recursive:
                    return GetRecursiveCoverage(
                        context,
                        recursive,
                        type,
                        allCases,
                        allConcreteTypes,
                        isClosed);

                // List and slice patterns constrain the sequence shape. They
                // cannot prove coverage of a closed runtime subtype without a
                // separate collection-domain analysis.
                case ListPatternSyntax _:
                case SlicePatternSyntax _:
                case RelationalPatternSyntax _:
                default:
                    context.ReportCasePatternNotSupported(pattern);
                    return ClosedPatternCoverage.Unknown();
            }
        }

        private static ClosedPatternCoverage GetConstantCoverage(
            SyntaxNodeAnalysisContext context,
            ConstantPatternSyntax pattern,
            ITypeSymbol type,
            HashSet<ITypeSymbol> allCases,
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool isClosed)
        {
            if (pattern.Expression.IsTypeIdentifier(context, out var typeSymbol))
            {
                return GetTypeCoverage(
                    context,
                    typeSymbol,
                    pattern,
                    type,
                    allCases,
                    allConcreteTypes,
                    isClosed);
            }

            if (IsNullConstant(context, pattern.Expression))
            {
                return ClosedPatternCoverage.Known(
                    Enumerable.Empty<ITypeSymbol>(),
                    canBeNegated: true,
                    isAlwaysFalse: true);
            }

            context.ReportCasePatternNotSupported(pattern);
            return ClosedPatternCoverage.Unknown();
        }

        private static ClosedPatternCoverage GetRecursiveCoverage(
            SyntaxNodeAnalysisContext context,
            RecursivePatternSyntax pattern,
            ITypeSymbol type,
            HashSet<ITypeSymbol> allCases,
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool isClosed)
        {
            if (!IsTotalRecursivePattern(pattern))
            {
                context.ReportCasePatternNotSupported(pattern);
                return ClosedPatternCoverage.Unknown();
            }

            var matchedType = pattern.Type == null
                ? type
                : context.SemanticModel.GetTypeInfo(
                        pattern.Type,
                        context.CancellationToken)
                    .Type;

            var coverage = GetTypeCoverage(
                context,
                matchedType,
                pattern,
                type,
                allCases,
                allConcreteTypes,
                isClosed);

            if (!coverage.IsKnown)
                return coverage;

            if (HasCompilerError(context, pattern))
            {
                context.ReportCasePatternNotSupported(pattern);
                return ClosedPatternCoverage.Unknown();
            }

            return coverage;
        }

        private static ClosedPatternCoverage GetTypeCoverage(
            SyntaxNodeAnalysisContext context,
            ITypeSymbol matchedType,
            SyntaxNode diagnosticNode,
            ITypeSymbol governingType,
            HashSet<ITypeSymbol> allCases,
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool isClosed)
        {
            if (matchedType == null)
            {
                if (diagnosticNode is PatternSyntax pattern)
                    context.ReportCasePatternNotSupported(pattern);
                return ClosedPatternCoverage.Unknown();
            }

            if (isClosed
                && !allCases.Any(t => SymbolEqualityComparer.Default.Equals(
                    t,
                    matchedType)))
            {
                var diagnostic = Diagnostic.Create(
                    Diagnostics.MatchMustBeOnCaseType,
                    diagnosticNode.GetLocation(),
                    matchedType.GetFullName(),
                    governingType?.GetFullName() ?? matchedType.GetFullName());
                context.ReportDiagnostic(diagnostic);
                return ClosedPatternCoverage.Unknown();
            }

            var types = allConcreteTypes
                .Where(t => t.IsSubtypeOf(matchedType))
                .ToArray();

            var canBeNegated = matchedType.TypeKind != TypeKind.Interface
                               && governingType?.TypeKind != TypeKind.Interface;
            var isAlwaysTrue = types.Length == allConcreteTypes.Count();
            var isAlwaysFalse = types.Length == 0;

            return ClosedPatternCoverage.Known(
                types,
                canBeNegated,
                isAlwaysTrue,
                isAlwaysFalse);
        }

        private static ClosedPatternCoverage GetNotCoverage(
            SyntaxNodeAnalysisContext context,
            UnaryPatternSyntax pattern,
            ClosedPatternCoverage operand,
            IEnumerable<ITypeSymbol> allConcreteTypes)
        {
            if (!operand.IsKnown)
                return ClosedPatternCoverage.Unknown();

            if (!operand.CanBeNegated)
            {
                if (operand.IsAlwaysTrue)
                {
                    return ClosedPatternCoverage.Known(
                        Enumerable.Empty<ITypeSymbol>(),
                        canBeNegated: true,
                        isAlwaysFalse: true);
                }

                if (operand.IsAlwaysFalse)
                {
                    return All(allConcreteTypes, canBeNegated: true);
                }

                context.ReportCasePatternNotSupported(pattern);
                return ClosedPatternCoverage.Unknown();
            }

            var types = allConcreteTypes
                .Where(t => !operand.Types.Contains(t));
            var typeArray = types.ToArray();

            return ClosedPatternCoverage.Known(
                typeArray,
                canBeNegated: true,
                isAlwaysTrue: typeArray.Length == allConcreteTypes.Count(),
                isAlwaysFalse: typeArray.Length == 0);
        }

        private static ClosedPatternCoverage Or(
            ClosedPatternCoverage left,
            ClosedPatternCoverage right)
        {
            var types = left.Types
                .Concat(right.Types)
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<ITypeSymbol>()
                .ToArray();

            return ClosedPatternCoverage.Create(
                types,
                left.IsKnown && right.IsKnown,
                left.CanBeNegated && right.CanBeNegated,
                left.IsAlwaysTrue || right.IsAlwaysTrue,
                left.IsAlwaysFalse && right.IsAlwaysFalse);
        }

        private static ClosedPatternCoverage And(
            ClosedPatternCoverage left,
            ClosedPatternCoverage right)
        {
            var types = left.IsKnown && right.IsKnown
                ? left.Types.Intersect(
                    right.Types,
                    SymbolEqualityComparer.Default)
                    .Cast<ITypeSymbol>()
                : Enumerable.Empty<ITypeSymbol>();
            var typeArray = types.ToArray();

            return ClosedPatternCoverage.Create(
                typeArray,
                left.IsKnown && right.IsKnown,
                left.CanBeNegated && right.CanBeNegated,
                left.IsAlwaysTrue && right.IsAlwaysTrue,
                left.IsAlwaysFalse || right.IsAlwaysFalse);
        }

        private static ClosedPatternCoverage All(
            IEnumerable<ITypeSymbol> allConcreteTypes,
            bool canBeNegated)
        {
            var types = allConcreteTypes.ToArray();
            return ClosedPatternCoverage.Known(
                types,
                canBeNegated,
                isAlwaysTrue: true);
        }

        private static bool IsTotalRecursivePattern(
            RecursivePatternSyntax pattern)
        {
            return (pattern.PositionalPatternClause == null
                    || (pattern.PositionalPatternClause.Subpatterns.Count > 0
                        && pattern.PositionalPatternClause.Subpatterns
                            .All(s => IsTotalNestedPattern(s.Pattern))))
                   && (pattern.PropertyPatternClause == null
                       || pattern.PropertyPatternClause.Subpatterns
                           .All(s => IsTotalNestedPattern(s.Pattern)));
        }

        private static bool IsTotalNestedPattern(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case DiscardPatternSyntax _:
                case VarPatternSyntax _:
                    return true;

                case ParenthesizedPatternSyntax parenthesized:
                    return IsTotalNestedPattern(parenthesized.Pattern);

                case BinaryPatternSyntax binary
                    when binary.OperatorToken.IsKind(SyntaxKind.OrKeyword):
                    return IsTotalNestedPattern(binary.Left)
                           || IsTotalNestedPattern(binary.Right);

                case BinaryPatternSyntax binary
                    when binary.OperatorToken.IsKind(SyntaxKind.AndKeyword):
                    return IsTotalNestedPattern(binary.Left)
                           && IsTotalNestedPattern(binary.Right);

                case RecursivePatternSyntax recursive:
                    // A recursive pattern over a nested value is still a
                    // non-null/type constraint. Without analyzing that value's
                    // domain, it cannot be proven total.
                    return false;

                default:
                    return false;
            }
        }

        private static bool IsNullConstant(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax expression)
        {
            var value = context.SemanticModel.GetConstantValue(
                expression,
                context.CancellationToken);
            return value.HasValue && value.Value == null;
        }

        private static bool HasCompilerError(
            SyntaxNodeAnalysisContext context,
            PatternSyntax pattern)
        {
            try
            {
                return context.SemanticModel
                    .GetDiagnostics(
                        pattern.Span,
                        context.CancellationToken)
                    .Any(d => d.Severity == DiagnosticSeverity.Error);
            }
            catch (ArgumentException)
            {
                return true;
            }
        }
    }
}
