using ExhaustiveMatching.Analyzer.Enums.Semantics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveMatching.Analyzer
{
    internal static class ExpressionAnalyzer
    {
        public static SwitchStatementKind SwitchStatementKindForThrown(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax thrownExpression)
        {
            var exceptionType = context.SemanticModel.GetTypeInfo(thrownExpression, context.CancellationToken).Type;
            if (exceptionType == null || exceptionType.TypeKind == TypeKind.Error)
                return new SwitchStatementKind(false, false);

            var isExhaustiveMatchFailedException =
                exceptionType.IsExhaustiveMatchFailedException();
            var isInvalidEnumArgumentException =
                exceptionType.IsInvalidEnumArgumentException();
            var isExhaustive = isExhaustiveMatchFailedException || isInvalidEnumArgumentException;

            return new SwitchStatementKind(isExhaustive, isInvalidEnumArgumentException);
        }
    }
}
