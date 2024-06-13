using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NotPartialDefinition = new("DI0001",
        "Method is not partial",
        "Method with GenerateServiceRegistrations attribute must have partial modifier",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor WrongReturnType = new("DI0002",
        "Wrong return type",
        "Method with GenerateServiceRegistrations attribute must return void or IServiceCollection",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor WrongMethodParameters = new("DI0003",
        "Wrong method parameters",
        "Method with GenerateServiceRegistrations attribute must have a single IServiceCollection parameter",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MissingSearchCriteria = new("DI0004",
        "Missing search criteria",
        "GenerateServiceRegistrations must have at least one search criteria",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor NoMatchingTypesFound = new("DI0005",
        "No matching types found",
        "There are no types matching attribute's search criteria",
        "Usage",
        DiagnosticSeverity.Warning,
        true);
}
