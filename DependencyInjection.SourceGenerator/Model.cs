using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependencyInjection.SourceGenerator;

record DiagnosticModel<T>
{
    public T? Model { get; init; }
    public Diagnostic? Diagnostic { get; init; }

    public static implicit operator DiagnosticModel<T>(T model) => new() { Model = model };

    public static implicit operator DiagnosticModel<T>(Diagnostic diagnostic) => new() { Diagnostic = diagnostic };
}

record MethodImplementationModel(
    MethodModel Method,
    EquatableArray<ServiceRegistrationModel> Registrations);

record ServiceRegistrationModel(
    string Lifetime,
    string ServiceTypeName,
    string ImplementationTypeName,
    bool IsOpenGeneric);

record MethodWithAttributesModel(MethodModel Method, EquatableArray<AttributeModel> Attributes);

record MethodModel(
    string Namespace,
    string TypeName,
    string TypeMetadataName,
    string TypeModifiers,
    string MethodName,
    string MethodModifiers,
    bool IsExtensionMethod,
    bool ReturnsVoid)
{
    public static MethodModel Create(IMethodSymbol method, SyntaxNode syntax)
    {
        return new MethodModel(
            Namespace: method.ContainingNamespace.ToDisplayString(),
            TypeName: method.ContainingType.Name,
            TypeMetadataName: method.ContainingType.ToFullMetadataName(),
            TypeModifiers: GetModifiers(GetTypeSyntax(syntax)),
            MethodName: method.Name,
            MethodModifiers: GetModifiers(syntax),
            IsExtensionMethod: method.IsExtensionMethod,
            ReturnsVoid: method.ReturnsVoid);
    }

    private static TypeDeclarationSyntax GetTypeSyntax(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is TypeDeclarationSyntax t)
                return t;
            parent = parent.Parent;
        }
        return null;
    }

    private static string GetModifiers(SyntaxNode syntax)
    {
        return (syntax as MemberDeclarationSyntax)?.Modifiers.ToString() ?? "";
    }
}

record AttributeModel(
    string? AssignableToTypeName,
    string? AssemblyOfTypeName,
    string Lifetime,
    string? TypeNameFilter,
    bool AsImplementedInterfaces,
    Location Location)
{
    public static AttributeModel Create(AttributeData attribute)
    {
        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;
        var asImplementedInterfaces = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsImplementedInterfaces").Value.Value is true;
        var typeNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeNameFilter").Value.Value as string;

        if (string.IsNullOrWhiteSpace(typeNameFilter))
            typeNameFilter = null;

        var assemblyOfTypeName = assemblyType?.ToFullMetadataName();
        var assignableToTypeName = assignableTo?.ToFullMetadataName();
        var lifetime = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int? switch
        {
            0 => "Singleton",
            1 => "Scoped",
            _ => "Transient"
        };

        var syntax = attribute.ApplicationSyntaxReference.SyntaxTree;
        var textSpan = attribute.ApplicationSyntaxReference.Span;
        var location = Location.Create(syntax, textSpan);

        return new(assignableToTypeName, assemblyOfTypeName, lifetime, typeNameFilter, asImplementedInterfaces, location);
    }

    public bool HasSearchCriteria => TypeNameFilter != null || AssignableToTypeName != null;
}