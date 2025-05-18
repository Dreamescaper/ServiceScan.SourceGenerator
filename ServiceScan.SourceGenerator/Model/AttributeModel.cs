using System.Linq;
using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Model;

enum KeySelectorType { Method, GenericMethod, TypeMember };

record AttributeModel(
    string? AssignableToTypeName,
    EquatableArray<string>? AssignableToGenericArguments,
    string[]? AssembliesOfTypesNames,
    string Lifetime,
    string? AttributeFilterTypeName,
    string? TypeNameFilter,
    string? ExcludeByAttributeTypeName,
    string? ExcludeByTypeName,
    string? ExcludeAssignableToTypeName,
    EquatableArray<string>? ExcludeAssignableToGenericArguments,
    string? KeySelector,
    KeySelectorType? KeySelectorType,
    string? CustomHandler,
    bool AsImplementedInterfaces,
    bool AsSelf,
    Location Location,
    bool HasErrors)
{
    public bool HasSearchCriteria => TypeNameFilter != null || AssignableToTypeName != null || AttributeFilterTypeName != null;

    public static AttributeModel Create(AttributeData attribute, IMethodSymbol method)
    {
        var assembliesTypes = attribute.NamedArguments
            .FirstOrDefault(a => a.Key == "FromAssembliesOf").Value is { Kind: TypedConstantKind.Array } constantValue
            ? constantValue.Values.Select(tc => tc.Value as INamedTypeSymbol).ToArray()
            : null!;

        if (assembliesTypes is null)
        {
            assembliesTypes = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value is INamedTypeSymbol namedTypeSymbol
                ? [namedTypeSymbol]
                : null!;
        }
        
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;
        var asImplementedInterfaces = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsImplementedInterfaces").Value.Value is true;
        var asSelf = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsSelf").Value.Value is true;
        var attributeFilterType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AttributeFilter").Value.Value as INamedTypeSymbol;
        var typeNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeNameFilter").Value.Value as string;
        var excludeByAttributeType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "ExcludeByAttribute").Value.Value as INamedTypeSymbol;
        var excludeByTypeName = attribute.NamedArguments.FirstOrDefault(a => a.Key == "ExcludeByTypeName").Value.Value as string;
        var excludeAssignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "ExcludeAssignableTo").Value.Value as INamedTypeSymbol;
        var keySelector = attribute.NamedArguments.FirstOrDefault(a => a.Key == "KeySelector").Value.Value as string;
        var customHandler = attribute.NamedArguments.FirstOrDefault(a => a.Key == "CustomHandler").Value.Value as string;

        KeySelectorType? keySelectorType = null;
        if (keySelector != null)
        {
            var keySelectorMethod = method.ContainingType.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic && m.Name == keySelector);

            if (keySelectorMethod != null)
            {
                keySelectorType = keySelectorMethod.IsGenericMethod ? Model.KeySelectorType.GenericMethod : Model.KeySelectorType.Method;
            }
            else
            {
                keySelectorType = Model.KeySelectorType.TypeMember;
            }
        }

        if (string.IsNullOrWhiteSpace(typeNameFilter))
            typeNameFilter = null;

        if (string.IsNullOrWhiteSpace(excludeByTypeName))
            excludeByTypeName = null;

        var attributeFilterTypeName = attributeFilterType?.ToFullMetadataName();
        var excludeByAttributeTypeName = excludeByAttributeType?.ToFullMetadataName();
        var assembliesOfTypesNames = assembliesTypes?.Select(x => x.ToFullMetadataName()).ToArray();
        var assignableToTypeName = assignableTo?.ToFullMetadataName();
        var excludeAssignableToTypeName = excludeAssignableTo?.ToFullMetadataName();
        EquatableArray<string>? assignableToGenericArguments = assignableTo != null && assignableTo.IsGenericType && !assignableTo.IsUnboundGenericType
            ? [.. assignableTo?.TypeArguments.Select(t => t.ToFullMetadataName())]
            : null;
        EquatableArray<string>? excludeAssignableToGenericArguments = excludeAssignableTo != null && excludeAssignableTo.IsGenericType && !excludeAssignableTo.IsUnboundGenericType
            ? [.. excludeAssignableTo?.TypeArguments.Select(t => t.ToFullMetadataName())]
            : null;

        var lifetime = (attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int?) switch
        {
            0 => "Singleton",
            1 => "Scoped",
            _ => "Transient"
        };

        var syntax = attribute.ApplicationSyntaxReference.SyntaxTree;
        var textSpan = attribute.ApplicationSyntaxReference.Span;
        var location = Location.Create(syntax, textSpan);

        var hasError = (assembliesTypes?.Any(x => x is { TypeKind: TypeKind.Error }) ?? false)
            || assignableTo is { TypeKind: TypeKind.Error }
            || attributeFilterType is { TypeKind: TypeKind.Error };

        return new(
            assignableToTypeName,
            assignableToGenericArguments,
            assembliesOfTypesNames,
            lifetime,
            attributeFilterTypeName,
            typeNameFilter,
            excludeByAttributeTypeName,
            excludeByTypeName,
            excludeAssignableToTypeName,
            excludeAssignableToGenericArguments,
            keySelector,
            keySelectorType,
            customHandler,
            asImplementedInterfaces,
            asSelf,
            location,
            hasError);
    }
}