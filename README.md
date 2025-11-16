# Redundant Code Analyzer

A Roslyn code analyzer that identifies potentially redundant code across the entire solution. This analyzer detects unused types, methods, properties, fields, and events by analyzing the entire solution, including usage via reflection, LINQ, and other indirect methods.

## Features

- **Solution-wide analysis**: Analyzes code usage across the entire solution, not just within a single file
- **Comprehensive detection**: Identifies usage through:
  - Direct references
  - Reflection (GetType, GetMethod, GetProperty, etc.)
  - LINQ expressions
  - Dynamic invocations
  - Attributes
  - Interface implementations
  - Serialization contexts
- **Performance optimized**: Uses semantic model caching and concurrent execution
- **Best practices**: Follows Microsoft Roslyn analyzer best practices

## Requirements

- .NET Standard 2.1
- Microsoft.CodeAnalysis.CSharp 4.5.0 or later

## Installation

1. Build the solution:
   ```bash
   dotnet build
   ```

2. Reference the analyzer in your project:
   ```xml
   <ItemGroup>
     <Analyzer Include="path\to\RedundantCodeAnalyzer.dll" />
   </ItemGroup>
   ```

## Usage

The analyzer automatically runs during compilation and reports warnings for potentially unused code. The diagnostic ID is `RCA001`.

## Testing

Run the analyzer and unit tests with:

```bash
dotnet test
```

### Example

```csharp
// This method is never called anywhere in the solution
private void UnusedMethod()
{
    // ...
}
```

The analyzer will report:
```
RCA001: The method 'UnusedMethod' appears to be unused and may be redundant.
```

## What Gets Analyzed

The analyzer checks the following symbol types:
- Named types (classes, structs, interfaces, enums)
- Methods
- Properties
- Fields
- Events

## What Gets Skipped

The analyzer automatically skips:
- Generated code (marked with `[GeneratedCode]` or `[CompilerGenerated]`)
- Implicitly declared members (constructors, property accessors, etc.)
- Entry points (Main methods)
- Public or protected APIs (may be used externally)

## Performance Considerations

- Uses `CompilationStartAction` for efficient initialization
- Caches semantic models to avoid redundant computations
- Enables concurrent execution for better performance
- Skips generated code analysis

## Limitations

- The analyzer works at the compilation level, which includes the current project and its referenced assemblies. For true solution-wide analysis across all projects, each project should reference the analyzer.
- Some edge cases may not be detected, such as:
  - Usage through complex string manipulation that constructs type/method names
  - Usage in external assemblies not referenced by the current project
  - Usage through configuration files or other non-code sources

## Contributing

Contributions are welcome! Please ensure that any changes follow Microsoft Roslyn analyzer best practices and maintain performance characteristics.

## License

See LICENSE file for details.

