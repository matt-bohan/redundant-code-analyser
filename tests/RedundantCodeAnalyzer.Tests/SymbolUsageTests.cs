using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using RedundantCodeAnalyzer.Tests.Infrastructure;
using Xunit;

namespace RedundantCodeAnalyzer.Tests;

public class SymbolUsageTests
{
    [Fact]
    public Task ReportsUnusedPrivateField()
    {
        const string source = """
namespace Sample;

internal class C
{
    private int _unusedField;
}

public static class EntryPoint
{
    public static void Use() => _ = new C();
}
""";

        var expected = RedundantCodeAnalyzerVerifier.Diagnostic()
            .WithSpan(5,17,5,29)
            .WithArguments("_unusedField", "field");

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public Task PrivateFieldUsedInLambda_NoDiagnostic()
    {
        const string source = """
using System;

namespace Sample;

internal class Calculator
{
    private int _value = 10;

    public int Compute()
    {
        Func<int> lambda = () => _value;
        return lambda();
    }
}

public static class EntryPoint
{
    public static int Run() => new Calculator().Compute();
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task PrivateMethodUsedInLocalFunction_NoDiagnostic()
    {
        const string source = """
namespace Sample;

internal class Processor
{
    private int Transform(int value) => value * 2;

    public int Execute(int input)
    {
        int Local() => Transform(input);
        return Local();
    }
}

public static class EntryPoint
{
    public static int Run(int value) => new Processor().Execute(value);
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task PrivateFieldUsedInOverride_NoDiagnostic()
    {
        const string source = """
namespace Sample;

internal abstract class BaseWidget
{
    public abstract string Render();
}

internal class ConcreteWidget : BaseWidget
{
    private string _template = "value";

    public override string Render()
    {
        return _template;
    }
}

public static class EntryPoint
{
    public static string Run() => new ConcreteWidget().Render();
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }
}

