using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Visitor that searches for symbol usage in lambdas, local functions, and overridden methods.
    /// This is important for detecting usage of private fields that are only used in these contexts.
    /// </summary>
    internal class LambdaLocalFunctionOverrideVisitor : CSharpSyntaxWalker
    {
        private readonly ISymbol _targetSymbol;
        private readonly SemanticModel _semanticModel;
        private readonly Compilation _compilation;

        public bool FoundUsage { get; private set; }

        public LambdaLocalFunctionOverrideVisitor(ISymbol targetSymbol, SemanticModel semanticModel, Compilation compilation)
        {
            _targetSymbol = targetSymbol;
            _semanticModel = semanticModel;
            _compilation = compilation;
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            // Check for symbol usage within the lambda
            CheckForSymbolUsage(node);
            base.VisitParenthesizedLambdaExpression(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            // Check for symbol usage within the lambda
            CheckForSymbolUsage(node);
            base.VisitSimpleLambdaExpression(node);
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            // Check for symbol usage within anonymous methods
            CheckForSymbolUsage(node);
            base.VisitAnonymousMethodExpression(node);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            // Check for symbol usage within local functions
            CheckForSymbolUsage(node);
            base.VisitLocalFunctionStatement(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Check for symbol usage in all methods (not just overridden ones)
            // This is important for detecting usage of private fields in regular methods
            CheckForSymbolUsage(node);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            // Check if this is an overridden property and if it uses the target symbol
            var propertySymbol = _semanticModel.GetDeclaredSymbol(node);
            if (propertySymbol != null && propertySymbol.IsOverride)
            {
                CheckForSymbolUsage(node);
            }
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            // Check for symbol usage in property/event accessors
            CheckForSymbolUsage(node);
            base.VisitAccessorDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            // Check for symbol usage in constructors
            CheckForSymbolUsage(node);
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            // Check for symbol usage in field initializers (which are part of the field declaration)
            CheckForSymbolUsage(node);
            base.VisitFieldDeclaration(node);
        }

        private void CheckForSymbolUsage(SyntaxNode node)
        {
            if (FoundUsage)
            {
                return;
            }

            // Visit all identifier names and member access expressions within this node
            var identifierNames = node.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var identifier in identifierNames)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
                if (IsTargetSymbol(symbolInfo))
                {
                    FoundUsage = true;
                    return;
                }
            }

            var memberAccesses = node.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            foreach (var memberAccess in memberAccesses)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(memberAccess);
                if (IsTargetSymbol(symbolInfo))
                {
                    FoundUsage = true;
                    return;
                }
            }

            // Check for usage in object creation
            var objectCreations = node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            foreach (var objectCreation in objectCreations)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(objectCreation);
                if (IsTargetSymbol(symbolInfo))
                {
                    FoundUsage = true;
                    return;
                }
            }

            // Check for usage in invocations
            var invocations = node.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(invocation);
                if (IsTargetSymbol(symbolInfo))
                {
                    FoundUsage = true;
                    return;
                }
            }
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
