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

    public static readonly DiagnosticDescriptor KeySelectorMethodHasIncorrectSignature = new("DI0007",
        "Provided KeySelector method has incorrect signature",
        "KeySelector should have non-void return type, and either be generic with no parameters, or non-generic with a single Type parameter",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor OnlyOneCustomHandlerAllowed = new("DI0008",
        "Only one GenerateServiceRegistrations attribute is allowed when CustomHandler used",
        "Only one GenerateServiceRegistrations attribute is allowed when CustomHandler used",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor WrongReturnTypeForCustomHandler = new("DI0009",
        "Wrong return type",
        "Method with CustomHandler must return void or 'this' parameter type",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor CustomHandlerMethodNotFound = new("DI0012",
        "Provided CustomHandler method is not found",
        "CustomHandler parameter should point to a static method in the class",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor CustomHandlerMethodHasIncorrectSignature = new("DI0011",
        "Provided CustomHandler method has incorrect signature",
        "CustomHandler method must be generic, and must have the same parameters as the method with an attribute",
        "Usage",
        DiagnosticSeverity.Error,
        true);
}
