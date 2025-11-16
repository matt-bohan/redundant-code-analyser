using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Visitor that searches for usage of an attribute type.
    /// </summary>
    internal class AttributeUsageVisitor : CSharpSyntaxWalker
    {
        private readonly INamedTypeSymbol _attributeType;
        private readonly SemanticModel _semanticModel;

        public bool FoundUsage { get; private set; }

        public AttributeUsageVisitor(INamedTypeSymbol attributeType, SemanticModel semanticModel)
        {
            _attributeType = attributeType;
            _semanticModel = semanticModel;
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                var attributeType = method.ContainingType;
                if (attributeType != null &&
                    SymbolEqualityComparer.Default.Equals(attributeType, _attributeType))
                {
                    FoundUsage = true;
                    return;
                }
            }

            base.VisitAttribute(node);
        }
    }
}

