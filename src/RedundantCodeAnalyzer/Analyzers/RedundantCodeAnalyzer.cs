using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RedundantCodeAnalyzer;

namespace RedundantCodeAnalyzer.Analyzers
{
    /// <summary>
    /// Analyzer that identifies potentially redundant code across the entire solution.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RedundantCodeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RCA001";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
            nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString Description = new LocalizableResourceString(
            nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        private const string Category = "CodeQuality";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: null,
            customTags: new[] {  WellKnownDiagnosticTags.Unnecessary ,"AnalyzerReleaseTracking"  });  

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Use CompilationStartAction for better performance and caching
            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                // Cache semantic models for performance
                var semanticModelCache = new Dictionary<SyntaxTree, SemanticModel>();

                compilationStartContext.RegisterSymbolAction(symbolContext =>
                {
                    AnalyzeSymbol(symbolContext, semanticModelCache);
                },
                SymbolKind.NamedType,
                SymbolKind.Method,
                SymbolKind.Property,
                SymbolKind.Field,
                SymbolKind.Event);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, Dictionary<SyntaxTree, SemanticModel> semanticModelCache)
        {
            var symbol = context.Symbol;

            // Skip if symbol is:
            // - Generated code
            // - Implicitly declared (constructors, property accessors, etc.)
            // - Entry point (Main method)
            // - Public API that might be used externally
            if (symbol.IsImplicitlyDeclared ||
                IsGeneratedCode(symbol) ||
                IsEntryPoint(symbol) ||
                IsPublicApi(symbol))
            {
                return;
            }

            // Get the compilation to analyze the entire solution
            var compilation = context.Compilation;

            // Check if symbol is used anywhere in the solution
            var usageAnalyzer = new SymbolUsageAnalyzer(symbol, compilation, semanticModelCache);
            if (usageAnalyzer.IsUsed())
            {
                return;
            }

            // Symbol appears to be unused - report diagnostic
            var location = symbol.Locations.FirstOrDefault();
            if (location != null && location.IsInSource)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    location,
                    symbol.Name,
                    GetSymbolKindName(symbol));

                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsGeneratedCode(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "System.CodeDom.Compiler.GeneratedCodeAttribute" ||
                attr.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }

        private static bool IsEntryPoint(ISymbol symbol)
        {
            if (symbol is IMethodSymbol method)
            {
                return method.Name == "Main" &&
                       method.IsStatic &&
                       (method.ReturnType.SpecialType == SpecialType.System_Int32 ||
                        method.ReturnType.SpecialType == SpecialType.System_Void) &&
                       method.DeclaredAccessibility == Accessibility.Private;
            }
            return false;
        }

        private static bool IsPublicApi(ISymbol symbol)
        {
            // Consider public or protected members as potentially used externally
            return symbol.DeclaredAccessibility == Accessibility.Public ||
                   symbol.DeclaredAccessibility == Accessibility.Protected ||
                   symbol.DeclaredAccessibility == Accessibility.ProtectedOrInternal;
        }

        private static string GetSymbolKindName(ISymbol symbol)
        {
            return symbol.Kind switch
            {
                SymbolKind.NamedType => "type",
                SymbolKind.Method => "method",
                SymbolKind.Property => "property",
                SymbolKind.Field => "field",
                SymbolKind.Event => "event",
                _ => "symbol"
            };
        }
    }
}
