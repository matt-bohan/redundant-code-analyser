using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Analyzes whether a symbol is used anywhere in the solution.
    /// </summary>
    internal class SymbolUsageAnalyzer
    {
        private readonly ISymbol _targetSymbol;
        private readonly Compilation _compilation;
        private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModelCache;

        public SymbolUsageAnalyzer(ISymbol targetSymbol, Compilation compilation, Dictionary<SyntaxTree, SemanticModel> semanticModelCache)
        {
            _targetSymbol = targetSymbol;
            _compilation = compilation;
            _semanticModelCache = semanticModelCache;
        }

        public bool IsUsed()
        {
            // Check direct references
            if (HasDirectReferences())
            {
                return true;
            }

            // Check reflection usage
            if (HasReflectionUsage())
            {
                return true;
            }

            // Check LINQ usage
            if (HasLinqUsage())
            {
                return true;
            }

            // Check attribute usage
            if (HasAttributeUsage())
            {
                return true;
            }

            // Check interface/abstract method implementations
            if (IsInterfaceMember() || IsAbstractOverride())
            {
                return true;
            }

            // Check serialization usage
            if (HasSerializationUsage())
            {
                return true;
            }

            // Check dynamic usage
            if (HasDynamicUsage())
            {
                return true;
            }

            return false;
        }

        private bool HasDirectReferences()
        {
            foreach (var syntaxTree in _compilation.SyntaxTrees)
            {
                // Skip the file where the symbol is declared
                if (IsSymbolDeclaredInTree(syntaxTree))
                {
                    continue;
                }

                // Use cached semantic model for performance
                if (!_semanticModelCache.TryGetValue(syntaxTree, out var semanticModel))
                {
                    semanticModel = _compilation.GetSemanticModel(syntaxTree);
                    _semanticModelCache[syntaxTree] = semanticModel;
                }

                var root = syntaxTree.GetRoot();
                var visitor = new SymbolReferenceVisitor(_targetSymbol, semanticModel);
                visitor.Visit(root);

                if (visitor.FoundReference)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasReflectionUsage()
        {
            var reflectionAnalyzer = new ReflectionUsageAnalyzer(_targetSymbol, _compilation, _semanticModelCache);
            return reflectionAnalyzer.HasReflectionUsage();
        }

        private bool HasLinqUsage()
        {
            var linqAnalyzer = new LinqUsageAnalyzer(_targetSymbol, _compilation, _semanticModelCache);
            return linqAnalyzer.HasLinqUsage();
        }

        private bool HasAttributeUsage()
        {
            // Check if symbol is used as an attribute
            if (_targetSymbol is INamedTypeSymbol typeSymbol)
            {
                var attributeType = _compilation.GetTypeByMetadataName("System.Attribute");
                if (attributeType != null && IsDerivedFrom(typeSymbol, attributeType))
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
                        var attributeVisitor = new AttributeUsageVisitor(typeSymbol, semanticModel);
                        attributeVisitor.Visit(root);

                        if (attributeVisitor.FoundUsage)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            var current = type.BaseType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }

        private bool IsInterfaceMember()
        {
            if (_targetSymbol is IMethodSymbol method)
            {
                // Check if method is part of an interface
                if (method.ContainingType.TypeKind == TypeKind.Interface)
                {
                    return true;
                }

                // Check if method implements an interface
                foreach (var iface in method.ContainingType.AllInterfaces)
                {
                    var interfaceMethod = iface.GetMembers().OfType<IMethodSymbol>()
                        .FirstOrDefault(m => method.Equals(
                            method.ContainingType.FindImplementationForInterfaceMember(m),
                            SymbolEqualityComparer.Default));

                    if (interfaceMethod != null)
                    {
                        return true;
                    }
                }
            }

            if (_targetSymbol is IPropertySymbol property)
            {
                if (property.ContainingType.TypeKind == TypeKind.Interface)
                {
                    return true;
                }

                foreach (var iface in property.ContainingType.AllInterfaces)
                {
                    var interfaceProperty = iface.GetMembers().OfType<IPropertySymbol>()
                        .FirstOrDefault(p => property.Equals(
                            property.ContainingType.FindImplementationForInterfaceMember(p),
                            SymbolEqualityComparer.Default));

                    if (interfaceProperty != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsAbstractOverride()
        {
            if (_targetSymbol is IMethodSymbol method)
            {
                return method.IsAbstract || method.IsOverride || method.IsVirtual;
            }

            if (_targetSymbol is IPropertySymbol property)
            {
                return property.IsAbstract || property.IsOverride || property.IsVirtual;
            }

            return false;
        }

        private bool HasSerializationUsage()
        {
            // Check if symbol has serialization attributes
            var hasSerializable = _targetSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "System.SerializableAttribute");

            if (hasSerializable)
            {
                return true;
            }

            // Check if type is used in serialization contexts
            if (_targetSymbol is INamedTypeSymbol typeSymbol)
            {
                var serializationTypes = new[]
                {
                    "System.Runtime.Serialization.ISerializable",
                    "System.Xml.Serialization.IXmlSerializable"
                };

                foreach (var serializationType in serializationTypes)
                {
                    var serializationInterface = _compilation.GetTypeByMetadataName(serializationType);
                    if (serializationInterface != null && 
                        typeSymbol.AllInterfaces.Contains(serializationInterface, SymbolEqualityComparer.Default))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasDynamicUsage()
        {
            // Check for dynamic invocations that might use this symbol
            foreach (var syntaxTree in _compilation.SyntaxTrees)
            {
                // Use cached semantic model
                if (!_semanticModelCache.TryGetValue(syntaxTree, out var semanticModel))
                {
                    semanticModel = _compilation.GetSemanticModel(syntaxTree);
                    _semanticModelCache[syntaxTree] = semanticModel;
                }

                var root = syntaxTree.GetRoot();
                var dynamicVisitor = new DynamicUsageVisitor(_targetSymbol, semanticModel);
                dynamicVisitor.Visit(root);

                if (dynamicVisitor.FoundUsage)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSymbolDeclaredInTree(SyntaxTree syntaxTree)
        {
            return _targetSymbol.Locations.Any(loc => 
                loc.SourceTree != null && 
                loc.SourceTree.FilePath == syntaxTree.FilePath);
        }
    }
}

