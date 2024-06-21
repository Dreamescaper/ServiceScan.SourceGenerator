using System.Linq;
using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Model;

record AttributeModel(
    TypeModel? AssignableToType,
    string Lifetime,
    string? TypeNameFilter,
    bool AsImplementedInterfaces,
    bool AsSelf,
    Location Location,
    bool HasErrors)
{
    public bool HasSearchCriteria => TypeNameFilter != null || AssignableToType != null;

    public static AttributeModel Create(AttributeData attribute)
    {
        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;
        var asImplementedInterfaces = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsImplementedInterfaces").Value.Value is true;
        var asSelf = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsSelf").Value.Value is true;
        var typeNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeNameFilter").Value.Value as string;

        if (string.IsNullOrWhiteSpace(typeNameFilter))
            typeNameFilter = null;

        var assignableToTypeModel = assignableTo is not null ? TypeModel.Create(assignableTo) : null;

        var lifetime = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int? switch
        {
            0 => "Singleton",
            1 => "Scoped",
            _ => "Transient"
        };

        var syntax = attribute.ApplicationSyntaxReference.SyntaxTree;
        var textSpan = attribute.ApplicationSyntaxReference.Span;
        var location = Location.Create(syntax, textSpan);

        var hasError = assemblyType is { TypeKind: TypeKind.Error } || assignableTo is { TypeKind: TypeKind.Error };

        return new(assignableToTypeModel, lifetime, typeNameFilter, asImplementedInterfaces, asSelf, location, hasError);
    }
}