using System.Threading.Tasks;
using RedundantCodeAnalyzer.Tests.Infrastructure;
using Xunit;

namespace RedundantCodeAnalyzer.Tests;

public class IndirectUsageTests
{
    [Fact]
    public Task TypeUsedViaReflection_NoDiagnostic()
    {
        const string source = """
using System;
using System.Reflection;

namespace Sample;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class CustomAttribute : Attribute
{
}

[Custom]
internal class Target
{
    public string Name { get; set; } = string.Empty;
}

internal static class ReflectionConsumer
{
    public static PropertyInfo? GetProperty()
        => typeof(Target).GetProperty(nameof(Target.Name));
}

public static class EntryPoint
{
    public static PropertyInfo? Run() => ReflectionConsumer.GetProperty();
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task PrivateMemberUsedInLinq_NoDiagnostic()
    {
        const string source = """
using System.Linq;

namespace Sample;

internal class Filter
{
    private bool IsEven(int value) => value % 2 == 0;

    public int[] Apply(int[] input)
        => input.Where(IsEven).ToArray();
}

public static class EntryPoint
{
    public static int[] Run(int[] input) => new Filter().Apply(input);
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task AttributeUsedOnMember_NoDiagnostic()
    {
        const string source = """
using System;

namespace Sample;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class TrackAttribute : Attribute
{
}

public class Model
{
    [Track]
    public int Value { get; set; }
}

public static class EntryPoint
{
    public static Model Create() => new Model { Value = 5 };
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public Task MethodUsedViaDynamic_NoDiagnostic()
    {
        const string source = """
namespace Sample;

internal class DynamicTarget
{
    private string Execute() => "done";

    public string Dispatch()
    {
        dynamic self = this;
        return self.Execute();
    }
}

public static class EntryPoint
{
    public static string Run() => new DynamicTarget().Dispatch();
}
""";

        return RedundantCodeAnalyzerVerifier.VerifyAnalyzerAsync(source);
    }
}

