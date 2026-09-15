using System;
using System.Collections.Generic;
using System.Linq;
using ExhaustiveMatching.Analyzer.Enums.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer.Enums.Analysis
{
    /// <summary>
    /// The portion of an enum domain which is definitely covered by a pattern.
    /// </summary>
    public sealed class EnumPatternCoverage
    {
        public EnumPatternCoverage(
            IEnumerable<object> values,
            bool coversNull,
            bool isKnown)
        {
            Values = new HashSet<object>(values);
            CoversNull = coversNull;
            IsKnown = isKnown;
        }

        /// <summary>
        /// Numeric values of enum members definitely covered by the pattern.
        /// </summary>
        public ISet<object> Values { get; }

        /// <summary>
        /// Whether the pattern definitely covers null.
        /// </summary>
        public bool CoversNull { get; }

        /// <summary>
        /// Whether the coverage is exact. Unsupported patterns return false,
        /// while Values still contains any safe lower bound from an or pattern.
        /// </summary>
        public bool IsKnown { get; }
    }

    /// <summary>
    /// Evaluates C# patterns against the finite set of declared enum values.
    /// </summary>
    public sealed class EnumPatternCoverageEvaluator
    {
        private readonly SyntaxNodeAnalysisContext _context;
        private readonly INamedTypeSymbol _enumType;
        private readonly bool _nullable;
        private readonly TypeCode _underlyingTypeCode;
        private readonly object[] _allValues;
        private readonly Action<PatternSyntax> _reportUnsupported;
        private readonly HashSet<SyntaxNode> _reportedUnsupported =
            new HashSet<SyntaxNode>();

        public EnumPatternCoverageEvaluator(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol enumType,
            bool nullable,
            Action<PatternSyntax> reportUnsupported)
        {
            _context = context;
            _enumType = enumType;
            _nullable = nullable;
            _underlyingTypeCode = enumType.EnumUnderlyingType.SpecialType.ToTypeCode();
            _allValues = GetEnumValues(enumType, _underlyingTypeCode);
            _reportUnsupported = reportUnsupported ?? (_ => { });
        }

        public EnumPatternCoverage EvaluateLabel(SwitchLabelSyntax label)
        {
            switch (label)
            {
                case CaseSwitchLabelSyntax caseLabel:
                    return EvaluateExpression(caseLabel.Value, caseLabel);
                case CasePatternSwitchLabelSyntax patternLabel:
                    return EvaluatePattern(patternLabel.Pattern);
                default:
                    return Known();
            }
        }

        private static bool IsRelationalOperator(SyntaxKind operatorKind)
        {
            switch (operatorKind)
            {
                case SyntaxKind.GreaterThanToken:
                case SyntaxKind.GreaterThanEqualsToken:
                case SyntaxKind.LessThanToken:
                case SyntaxKind.LessThanEqualsToken:
                    return true;
                default:
                    return false;
            }
        }

        public EnumPatternCoverage EvaluateExpression(
            ExpressionSyntax expression,
            SyntaxNode diagnosticNode = null)
        {
            if (HasCompilerError(expression))
                return Known();

            if (IsTypeIdentifier(expression, out var typeSymbol))
                return EvaluateType(typeSymbol, diagnosticNode);

            if (TryGetConstantValue(expression, out var value))
            {
                if (value == null)
                    return Null();

                if (TryConvert(value, out var converted))
                    return Known(new[] { converted });
            }

            // Traditional case labels are already compiler-checked constant
            // expressions. Do not add a second pattern diagnostic for an
            // invalid expression which the compiler is reporting.
            return Known();
        }

        public EnumPatternCoverage EvaluatePattern(PatternSyntax pattern)
        {
            switch (pattern)
            {
                case ParenthesizedPatternSyntax parenthesized:
                    return EvaluatePattern(parenthesized.Pattern);

                case BinaryPatternSyntax binary:
                    var left = EvaluatePattern(binary.Left);
                    var right = EvaluatePattern(binary.Right);
                    if (binary.OperatorToken.IsKind(SyntaxKind.OrKeyword))
                        return Or(left, right);

                    if (binary.OperatorToken.IsKind(SyntaxKind.AndKeyword))
                        return And(left, right);

                    return Unsupported(binary);

                case UnaryPatternSyntax unary:
                    if (!unary.OperatorToken.IsKind(SyntaxKind.NotKeyword))
                        return Unsupported(unary);

                    var operand = EvaluatePattern(unary.Pattern);
                    if (!operand.IsKnown)
                    {
                        return new EnumPatternCoverage(
                            Enumerable.Empty<object>(),
                            coversNull: false,
                            isKnown: false);
                    }
                    return Not(operand);

                case RelationalPatternSyntax relational:
                    return EvaluateRelational(relational);

                case DiscardPatternSyntax _:
                    return Top();

                case VarPatternSyntax _:
                    return Top();

                case DeclarationPatternSyntax declaration:
                    return EvaluateType(
                        GetTypeInfo(declaration.Type),
                        declaration);

                case TypePatternSyntax typePattern:
                    return EvaluateType(
                        GetTypeInfo(typePattern.Type),
                        typePattern);

                case ConstantPatternSyntax constant:
                    return EvaluateConstantPattern(constant);

                case RecursivePatternSyntax recursive:
                    return EvaluateRecursivePattern(recursive);

                // A list pattern has no finite enum meaning. In particular,
                // do not treat a slice as a wildcard over the enum domain.
                case ListPatternSyntax _:
                case SlicePatternSyntax _:
                    return Unsupported(pattern);

                default:
                    return Unsupported(pattern);
            }
        }

        private EnumPatternCoverage EvaluateConstantPattern(
            ConstantPatternSyntax pattern)
        {
            if (HasCompilerError(pattern))
                return Unsupported(pattern);

            if (IsTypeIdentifier(pattern.Expression, out var typeSymbol))
                return EvaluateType(typeSymbol, pattern);

            if (!TryGetConstantValue(pattern.Expression, out var value))
                return Unsupported(pattern);

            if (value == null)
                return Null();

            return TryConvert(value, out var converted)
                ? Known(new[] { converted })
                : Unsupported(pattern);
        }

        private EnumPatternCoverage EvaluateRecursivePattern(
            RecursivePatternSyntax pattern)
        {
            if (!IsTotalRecursivePattern(pattern))
                return Unsupported(pattern);

            var matchedType = pattern.Type == null
                ? _enumType
                : GetTypeInfo(pattern.Type);

            var coverage = EvaluateType(matchedType, pattern);
            if (!coverage.IsKnown)
                return coverage;

            return HasCompilerError(pattern)
                ? Unsupported(pattern)
                : coverage;
        }

        private EnumPatternCoverage EvaluateRelational(
            RelationalPatternSyntax pattern)
        {
            if (!IsRelationalOperator(pattern.OperatorToken.Kind())
                || HasCompilerError(pattern))
                return Unsupported(pattern);

            if (!TryGetConstantValue(pattern.Expression, out var value)
                || value == null
                || !TryConvert(value, out var bound))
            {
                return Unsupported(pattern);
            }

            var matched = new List<object>();
            foreach (var enumValue in _allValues)
            {
                var comparison = Compare(enumValue, bound);
                if (MatchesRelationalOperator(
                        pattern.OperatorToken.Kind(),
                        comparison))
                {
                    matched.Add(enumValue);
                }
            }

            return Known(matched);
        }

        private EnumPatternCoverage EvaluateType(
            ITypeSymbol typeSymbol,
            SyntaxNode diagnosticNode)
        {
            if (typeSymbol == null)
            {
                return diagnosticNode is PatternSyntax pattern
                    ? Unsupported(pattern)
                    : Known();
            }

            if (SymbolEqualityComparer.Default.Equals(typeSymbol, _enumType))
                return Top(false);

            var conversion = _context.Compilation.ClassifyConversion(
                _enumType,
                typeSymbol);
            if (conversion.IsImplicit
                && (conversion.IsBoxing
                    || conversion.IsIdentity
                    || conversion.IsReference))
            {
                return Top(false);
            }

            return diagnosticNode is PatternSyntax unsupportedPattern
                ? Unsupported(unsupportedPattern)
                : Known();
        }

        private bool IsTotalRecursivePattern(
            RecursivePatternSyntax pattern)
        {
            if (pattern.PositionalPatternClause != null
                && (pattern.PositionalPatternClause.Subpatterns.Count == 0
                    || !pattern.PositionalPatternClause.Subpatterns
                        .All(s => IsTotalNestedPattern(s.Pattern))))
            {
                return false;
            }

            if (pattern.PropertyPatternClause != null
                && !pattern.PropertyPatternClause.Subpatterns
                    .All(s => IsTotalNestedPattern(s.Pattern)))
            {
                return false;
            }

            return true;
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
                    // Nested recursive patterns impose a non-null/type
                    // constraint on a member whose domain is not modeled here.
                    return false;

                default:
                    return false;
            }
        }

        private EnumPatternCoverage Not(EnumPatternCoverage operand)
        {
            var values = _allValues
                .Where(v => !operand.Values.Contains(v));
            return Known(values, _nullable && !operand.CoversNull);
        }

        private EnumPatternCoverage Or(
            EnumPatternCoverage left,
            EnumPatternCoverage right)
        {
            return new EnumPatternCoverage(
                left.Values.Concat(right.Values),
                left.CoversNull || right.CoversNull,
                left.IsKnown && right.IsKnown);
        }

        private EnumPatternCoverage And(
            EnumPatternCoverage left,
            EnumPatternCoverage right)
        {
            var values = left.IsKnown && right.IsKnown
                ? left.Values.Intersect(right.Values)
                : Enumerable.Empty<object>();

            return new EnumPatternCoverage(
                values,
                left.IsKnown && right.IsKnown
                    && left.CoversNull
                    && right.CoversNull,
                left.IsKnown && right.IsKnown);
        }

        private EnumPatternCoverage Top(bool coversNull = true)
        {
            return Known(_allValues, _nullable && coversNull);
        }

        private EnumPatternCoverage Null()
        {
            return Known(
                Enumerable.Empty<object>(),
                _nullable);
        }

        private EnumPatternCoverage Known(
            IEnumerable<object> values = null,
            bool coversNull = false)
        {
            return new EnumPatternCoverage(
                values ?? Enumerable.Empty<object>(),
                coversNull,
                true);
        }

        private EnumPatternCoverage Unsupported(SyntaxNode node)
        {
            if (node is PatternSyntax pattern
                && _reportedUnsupported.Add(pattern))
            {
                _reportUnsupported(pattern);
            }

            return new EnumPatternCoverage(
                Enumerable.Empty<object>(),
                false,
                false);
        }

        private bool IsTypeIdentifier(
            ExpressionSyntax expression,
            out ITypeSymbol typeSymbol)
        {
            typeSymbol = _context.SemanticModel
                .GetSymbolInfo(expression, _context.CancellationToken)
                .Symbol as ITypeSymbol;
            return typeSymbol != null;
        }

        private ITypeSymbol GetTypeInfo(TypeSyntax type)
            => _context.SemanticModel
                .GetTypeInfo(type, _context.CancellationToken)
                .Type;

        private bool TryGetConstantValue(
            ExpressionSyntax expression,
            out object value)
        {
            var optional = _context.SemanticModel.GetConstantValue(
                expression,
                _context.CancellationToken);

            if (optional.HasValue)
            {
                value = optional.Value;
                return true;
            }

            switch (expression)
            {
                case CastExpressionSyntax cast:
                    return TryGetConstantValue(cast.Expression, out value);
                case ParenthesizedExpressionSyntax parenthesized:
                    return TryGetConstantValue(parenthesized.Expression, out value);
                default:
                    value = null;
                    return false;
            }
        }

        private bool TryConvert(object value, out object converted)
        {
            try
            {
                converted = Convert.ChangeType(
                    value,
                    _underlyingTypeCode);
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }

        private static int Compare(object left, object right)
            => ((IComparable)left).CompareTo(right);

        private static bool MatchesRelationalOperator(
            SyntaxKind operatorKind,
            int comparison)
        {
            switch (operatorKind)
            {
                case SyntaxKind.GreaterThanToken:
                    return comparison > 0;
                case SyntaxKind.GreaterThanEqualsToken:
                    return comparison >= 0;
                case SyntaxKind.LessThanToken:
                    return comparison < 0;
                case SyntaxKind.LessThanEqualsToken:
                    return comparison <= 0;
                default:
                    return false;
            }
        }

        private bool HasCompilerError(SyntaxNode node)
        {
            try
            {
                return _context.SemanticModel
                    .GetDiagnostics(
                        node.Span,
                        _context.CancellationToken)
                    .Any(d => d.Severity == DiagnosticSeverity.Error);
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        private static object[] GetEnumValues(
            INamedTypeSymbol enumType,
            TypeCode typeCode)
        {
            var values = new List<object>();
            foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!field.IsConst || field.ConstantValue == null)
                    continue;

                try
                {
                    var value = Convert.ChangeType(field.ConstantValue, typeCode);
                    if (!values.Contains(value))
                        values.Add(value);
                }
                catch
                {
                    // Invalid metadata or an error symbol should not make the
                    // analyzer throw. The compiler will report the source error.
                }
            }

            return values.ToArray();
        }
    }
}
