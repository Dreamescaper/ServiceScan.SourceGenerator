using Microsoft.CodeAnalysis;

namespace DependencyInjection.SourceGenerator;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NotPartialDefinition = new("DI001", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor WrongReturnType = new("DI002", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor WrongMethodParameters = new("DI003", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
    public static readonly DiagnosticDescriptor NoMatchingTypesFound = new("DI004", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
}
