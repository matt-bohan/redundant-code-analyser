using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Visitor that searches for dynamic invocations that might use a symbol.
    /// </summary>
    internal class DynamicUsageVisitor : CSharpSyntaxWalker
    {
        private readonly ISymbol _targetSymbol;
        private readonly SemanticModel _semanticModel;

        public bool FoundUsage { get; private set; }

        public DynamicUsageVisitor(ISymbol targetSymbol, SemanticModel semanticModel)
        {
            _targetSymbol = targetSymbol;
            _semanticModel = semanticModel;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Check if this is a dynamic invocation
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.CandidateReason == CandidateReason.LateBound)
            {
                // Check if the method name matches
                if (node.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var memberName = memberAccess.Name.Identifier.ValueText;
                    if (memberName == _targetSymbol.Name && _targetSymbol is IMethodSymbol)
                    {
                        FoundUsage = true;
                        return;
                    }
                }
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Check dynamic member access
            var typeInfo = _semanticModel.GetTypeInfo(node.Expression);
            if (typeInfo.Type != null && typeInfo.Type.TypeKind == TypeKind.Dynamic)
            {
                var memberName = node.Name.Identifier.ValueText;
                if (memberName == _targetSymbol.Name)
                {
                    FoundUsage = true;
                    return;
                }
            }

            base.VisitMemberAccessExpression(node);
        }
    }
}

