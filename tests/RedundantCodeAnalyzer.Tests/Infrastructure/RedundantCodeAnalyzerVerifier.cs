using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using RedundantCodeAnalyzer.Analyzers;

namespace RedundantCodeAnalyzer.Tests.Infrastructure;

internal static class RedundantCodeAnalyzerVerifier
{
    private class Test : CSharpAnalyzerTest<RedundantCodeAnalyzer.Analyzers.RedundantCodeAnalyzer, XUnitVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        }
    }

    public static DiagnosticResult Diagnostic(string diagnosticId = RedundantCodeAnalyzer.Analyzers.RedundantCodeAnalyzer.DiagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expectedDiagnostics)
    {
        var test = new Test
        {
            TestCode = source
        };

        if (expectedDiagnostics.Length == 0)
        {
            test.ExpectedDiagnostics.Clear();
        }
        else
        {
            foreach (var diagnostic in expectedDiagnostics)
            {
                test.ExpectedDiagnostics.Add(diagnostic);
            }
        }

        return test.RunAsync();
    }
}

