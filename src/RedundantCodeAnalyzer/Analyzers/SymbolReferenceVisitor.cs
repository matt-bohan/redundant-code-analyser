using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Visitor that searches for direct references to a symbol.
    /// </summary>
    internal class SymbolReferenceVisitor : CSharpSyntaxWalker
    {
        private readonly ISymbol _targetSymbol;
        private readonly SemanticModel _semanticModel;

        public bool FoundReference { get; private set; }

        public SymbolReferenceVisitor(ISymbol targetSymbol, SemanticModel semanticModel)
        {
            _targetSymbol = targetSymbol;
            _semanticModel = semanticModel;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Skip if this is the declaration location itself
            if (IsDeclarationLocation(node))
            {
                base.VisitIdentifierName(node);
                return;
            }

            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (IsTargetSymbol(symbolInfo))
            {
                FoundReference = true;
                return;
            }

            base.VisitIdentifierName(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (IsTargetSymbol(symbolInfo))
            {
                FoundReference = true;
                return;
            }

            base.VisitMemberAccessExpression(node);
        }

        public override void VisitGenericName(GenericNameSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (IsTargetSymbol(symbolInfo))
            {
                FoundReference = true;
                return;
            }

            base.VisitGenericName(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (IsTargetSymbol(symbolInfo))
            {
                FoundReference = true;
                return;
            }

            base.VisitObjectCreationExpression(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (IsTargetSymbol(symbolInfo))
            {
                FoundReference = true;
                return;
            }

            base.VisitInvocationExpression(node);
        }

        private bool IsDeclarationLocation(SyntaxNode node)
        {
            var nodeLocation = node.GetLocation();
            if (nodeLocation == null || !nodeLocation.IsInSource)
            {
                return false;
            }

            // Check if this node is at one of the declaration locations of the target symbol
            foreach (var declarationLocation in _targetSymbol.Locations)
            {
                if (declarationLocation.IsInSource && 
                    declarationLocation.SourceTree == nodeLocation.SourceTree)
                {
                    var declarationSpan = declarationLocation.SourceSpan;
                    var nodeSpan = nodeLocation.SourceSpan;
                    
                    // If the node's span overlaps with or is within the declaration span, it's likely the declaration
                    if (nodeSpan.Start >= declarationSpan.Start && nodeSpan.End <= declarationSpan.End)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsTargetSymbol(SymbolInfo symbolInfo)
        {
            if (symbolInfo.Symbol != null && 
                SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, _targetSymbol))
            {
                return true;
            }

            // Check original definition for generic types/methods
            if (symbolInfo.Symbol is IMethodSymbol method && 
                SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, _targetSymbol))
            {
                return true;
            }

            if (symbolInfo.Symbol is INamedTypeSymbol type && 
                SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _targetSymbol))
            {
                return true;
            }

            // Check candidate symbols
            foreach (var candidate in symbolInfo.CandidateSymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, _targetSymbol))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

