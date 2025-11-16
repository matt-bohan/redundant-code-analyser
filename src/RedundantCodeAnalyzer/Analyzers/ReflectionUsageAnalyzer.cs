using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Analyzes code for reflection-based usage of symbols.
    /// </summary>
    internal class ReflectionUsageAnalyzer
    {
        private readonly ISymbol _targetSymbol;
        private readonly Compilation _compilation;
        private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModelCache;

        public ReflectionUsageAnalyzer(ISymbol targetSymbol, Compilation compilation, Dictionary<SyntaxTree, SemanticModel> semanticModelCache)
        {
            _targetSymbol = targetSymbol;
            _compilation = compilation;
            _semanticModelCache = semanticModelCache;
        }

        public bool HasReflectionUsage()
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
                var visitor = new ReflectionUsageVisitor(_targetSymbol, semanticModel, _compilation);
                visitor.Visit(root);

                if (visitor.FoundUsage)
                {
                    return true;
                }
            }

            return false;
        }

        private class ReflectionUsageVisitor : CSharpSyntaxWalker
        {
            private readonly ISymbol _targetSymbol;
            private readonly SemanticModel _semanticModel;
            private readonly Compilation _compilation;
            private readonly HashSet<string> _reflectionMethodNames;

            public bool FoundUsage { get; private set; }

            public ReflectionUsageVisitor(ISymbol targetSymbol, SemanticModel semanticModel, Compilation compilation)
            {
                _targetSymbol = targetSymbol;
                _semanticModel = semanticModel;
                _compilation = compilation;
                _reflectionMethodNames = new HashSet<string>
                {
                    "GetType",
                    "GetMethod",
                    "GetProperty",
                    "GetField",
                    "GetEvent",
                    "GetMember",
                    "GetMembers",
                    "InvokeMember",
                    "CreateInstance",
                    "GetConstructor",
                    "GetNestedType",
                    "Assembly.GetType",
                    "Type.GetType",
                    "Activator.CreateInstance"
                };
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(node);
                if (symbolInfo.Symbol is IMethodSymbol method)
                {
                    var methodName = method.Name;
                    var containingType = method.ContainingType?.ToDisplayString();

                    // Check for common reflection methods
                    if (_reflectionMethodNames.Contains(methodName) ||
                        containingType == "System.Type" ||
                        containingType == "System.Reflection.Assembly" ||
                        containingType == "System.Activator")
                    {
                        // Analyze string literals in the invocation
                        var stringLiterals = node.DescendantNodes()
                            .OfType<LiteralExpressionSyntax>()
                            .Where(l => l.Kind() == SyntaxKind.StringLiteralExpression);

                        foreach (var literal in stringLiterals)
                        {
                            var literalValue = _semanticModel.GetConstantValue(literal);
                            if (literalValue.HasValue && literalValue.Value is string stringValue)
                            {
                                if (MatchesSymbolName(stringValue))
                                {
                                    FoundUsage = true;
                                    return;
                                }
                            }
                        }

                        // Check for nameof expressions
                        var nameofExpressions = node.DescendantNodes()
                            .OfType<InvocationExpressionSyntax>()
                            .Where(i => i.Expression.ToString() == "nameof");

                        foreach (var nameofExpr in nameofExpressions)
                        {
                            var nameofSymbol = _semanticModel.GetSymbolInfo(nameofExpr.ArgumentList.Arguments.First().Expression);
                            if (nameofSymbol.Symbol != null &&
                                SymbolEqualityComparer.Default.Equals(nameofSymbol.Symbol, _targetSymbol))
                            {
                                FoundUsage = true;
                                return;
                            }
                        }
                    }
                }

                base.VisitInvocationExpression(node);
            }

            public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                // Check for typeof() expressions
                if (node.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    var typeInfo = _semanticModel.GetTypeInfo(typeOfExpr.Type);
                    if (typeInfo.Type != null &&
                        SymbolEqualityComparer.Default.Equals(typeInfo.Type, _targetSymbol))
                    {
                        FoundUsage = true;
                        return;
                    }
                }

                base.VisitMemberAccessExpression(node);
            }

            private bool MatchesSymbolName(string name)
            {
                // Check exact match
                if (name == _targetSymbol.Name)
                {
                    return true;
                }

                // Check fully qualified name
                var fullName = _targetSymbol.ToDisplayString();
                if (name == fullName || name.EndsWith("." + fullName))
                {
                    return true;
                }

                // Check metadata name
                if (name == _targetSymbol.MetadataName)
                {
                    return true;
                }

                // For methods, check signature match
                if (_targetSymbol is IMethodSymbol method)
                {
                    var methodName = method.Name;
                    if (name == methodName || name.StartsWith(methodName + "("))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}

