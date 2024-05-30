using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DependencyInjection.SourceGenerator;

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
    bool ReturnsVoid,
    EquatableArray<AttributeModel> Attributes)
{
    public static MethodModel Create(IMethodSymbol method, IEnumerable<AttributeModel> attributes)
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
            ReturnsVoid: method.ReturnsVoid,
            Attributes: new EquatableArray<AttributeModel>(attributes.ToArray()));
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
    string Lifetime)
{
    public static AttributeModel Create(AttributeData attribute)
    {
        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;

        var assemblyOfTypeName = assemblyType?.ToFullMetadataName();
        var assignableToTypeName = assignableTo?.ToFullMetadataName();
        var lifetime = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int? switch
        {
            0 => "Singleton",
            1 => "Scoped",
            _ => "Transient"
        };

        return new(assignableToTypeName, assemblyOfTypeName, lifetime);
    }
}