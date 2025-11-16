using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Analyzes code for LINQ-based usage of symbols.
    /// </summary>
    internal class LinqUsageAnalyzer
    {
        private readonly ISymbol _targetSymbol;
        private readonly Compilation _compilation;
        private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModelCache;

        public LinqUsageAnalyzer(ISymbol targetSymbol, Compilation compilation, Dictionary<SyntaxTree, SemanticModel> semanticModelCache)
        {
            _targetSymbol = targetSymbol;
            _compilation = compilation;
            _semanticModelCache = semanticModelCache;
        }

        public bool HasLinqUsage()
        {
            foreach (var syntaxTree in _compilation.SyntaxTrees)
            {
                // Use cached semantic model
                if (!_semanticModelCache.TryGetValue(syntaxTree, out var semanticModel))
                {
                    semanticModel = _compilation.GetSemanticModel(syntaxTree);
                    _semanticModelCache[syntaxTree] = semanticModel;
                }

                var root = syntaxTree.GetRoot();
                var visitor = new LinqUsageVisitor(_targetSymbol, semanticModel);
                visitor.Visit(root);

                if (visitor.FoundUsage)
                {
                    return true;
                }
            }

            return false;
        }

        private class LinqUsageVisitor : CSharpSyntaxWalker
        {
            private readonly ISymbol _targetSymbol;
            private readonly SemanticModel _semanticModel;

            public bool FoundUsage { get; private set; }

            public LinqUsageVisitor(ISymbol targetSymbol, SemanticModel semanticModel)
            {
                _targetSymbol = targetSymbol;
                _semanticModel = semanticModel;
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(node);
                if (symbolInfo.Symbol is IMethodSymbol method)
                {
                    // Check if this is a LINQ method that might use the target symbol
                    var containingType = method.ContainingType?.ToDisplayString();
                    if (containingType == "System.Linq.Enumerable" ||
                        containingType == "System.Linq.Queryable")
                    {
                        // Check lambda expressions in the arguments
                        var lambdaExpressions = node.ArgumentList.Arguments
                            .SelectMany(arg => arg.DescendantNodes().OfType<LambdaExpressionSyntax>());

                        foreach (var lambda in lambdaExpressions)
                        {
                            var lambdaSemanticModel = _semanticModel;
                            var lambdaSymbolInfo = lambdaSemanticModel.GetSymbolInfo(lambda);
                            
                            // Check if lambda references the target symbol
                            var identifierNames = lambda.DescendantNodes().OfType<IdentifierNameSyntax>();
                            foreach (var identifier in identifierNames)
                            {
                                var identifierSymbol = lambdaSemanticModel.GetSymbolInfo(identifier);
                                if (identifierSymbol.Symbol != null &&
                                    SymbolEqualityComparer.Default.Equals(identifierSymbol.Symbol, _targetSymbol))
                                {
                                    FoundUsage = true;
                                    return;
                                }
                            }

                            // Check member access in lambda
                            var memberAccesses = lambda.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
                            foreach (var memberAccess in memberAccesses)
                            {
                                var memberSymbol = lambdaSemanticModel.GetSymbolInfo(memberAccess);
                                if (memberSymbol.Symbol != null &&
                                    SymbolEqualityComparer.Default.Equals(memberSymbol.Symbol, _targetSymbol))
                                {
                                    FoundUsage = true;
                                    return;
                                }
                            }
                        }
                    }
                }

                base.VisitInvocationExpression(node);
            }

            public override void VisitQueryExpression(QueryExpressionSyntax node)
            {
                // Check query expressions for symbol usage
                var identifierNames = node.DescendantNodes().OfType<IdentifierNameSyntax>();
                foreach (var identifier in identifierNames)
                {
                    var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
                    if (symbolInfo.Symbol != null &&
                        SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, _targetSymbol))
                    {
                        FoundUsage = true;
                        return;
                    }
                }

                base.VisitQueryExpression(node);
            }
        }
    }
}

