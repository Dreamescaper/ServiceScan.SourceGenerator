using System.Linq;
using Microsoft.CodeAnalysis;

namespace DependencyInjection.SourceGenerator;

record MethodImplementationModel(MethodModel Method, EquatableArray<ServiceRegistrationModel> Registrations);

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
    string TypeAccessModifier,
    string TypeStatic,
    string MethodName,
    string MethodAccessModifier,
    string MethodStatic,
    bool IsExtensionMethod,
    bool ReturnsVoid)
{
    public static MethodModel Create(IMethodSymbol method)
    {
        return new MethodModel(
            Namespace: method.ContainingNamespace.ToDisplayString(),
            TypeName: method.ContainingType.Name,
            TypeMetadataName: method.ContainingType.ToFullMetadataName(),
            TypeAccessModifier: GetAccessModifier(method.ContainingType),
            TypeStatic: IsStatic(method.ContainingType),
            MethodName: method.Name,
            MethodAccessModifier: GetAccessModifier(method),
            MethodStatic: IsStatic(method),
            IsExtensionMethod: method.IsExtensionMethod,
            ReturnsVoid: method.ReturnsVoid);
    }

    private static string IsStatic(ISymbol symbol)
    {
        return symbol.IsStatic ? "static" : "";
    }

    private static string GetAccessModifier(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility.ToString().ToLowerInvariant();
    }
}

record AttributeModel(
    string? AssignableToTypeName,
    string? AssemblyOfTypeName,
    string Lifetime,
    string? TypeNameFilter,
    bool AsImplementedInterfaces)
{
    public static AttributeModel Create(AttributeData attribute)
    {
        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;
        var typeNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeNameFilter").Value.Value as string;
        var asImplementedInterfaces = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsImplementedInterfaces").Value.Value is true;

        var assemblyOfTypeName = assemblyType?.ToFullMetadataName();
        var assignableToTypeName = assignableTo?.ToFullMetadataName();
        var lifetime = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int? switch
        {
            0 => "Singleton",
            1 => "Scoped",
            _ => "Transient"
        };

        return new(assignableToTypeName, assemblyOfTypeName, lifetime, typeNameFilter, asImplementedInterfaces);
    }
}