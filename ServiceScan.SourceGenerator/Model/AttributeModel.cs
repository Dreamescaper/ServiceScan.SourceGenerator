using System.Linq;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Extensions;

namespace ServiceScan.SourceGenerator.Model;

enum KeySelectorType { Method, GenericMethod, TypeMember };
enum CustomHandlerType { Method, TypeMethod };

record AttributeModel(
    string? AssignableToTypeName,
    int AssignableToTypeParametersCount,
    string? AssemblyNameFilter,
    EquatableArray<string>? AssignableToGenericArguments,
    string? AssemblyOfTypeName,
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
    CustomHandlerType? CustomHandlerType,
    int CustomHandlerMethodTypeParametersCount,
    bool AsImplementedInterfaces,
    bool AsSelf,
    Location Location,
    bool HasErrors)
{
    public bool HasSearchCriteria => TypeNameFilter != null || AssignableToTypeName != null || AttributeFilterTypeName != null;

    public static AttributeModel Create(AttributeData attribute, IMethodSymbol method, SemanticModel semanticModel)
    {
        var position = attribute.ApplicationSyntaxReference?.Span.Start ?? 0;

        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assemblyNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssemblyNameFilter").Value.Value as string;
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

        var assignableToTypeParametersCount = assignableTo?.TypeParameters.Length ?? 0;

        KeySelectorType? keySelectorType = null;
        if (keySelector != null)
        {
            var keySelectorMethod = method.ContainingType.GetMethod(keySelector, semanticModel, position, isStatic: true);

            if (keySelectorMethod != null)
            {
                keySelectorType = keySelectorMethod.IsGenericMethod ? Model.KeySelectorType.GenericMethod : Model.KeySelectorType.Method;
            }
            else
            {
                keySelectorType = Model.KeySelectorType.TypeMember;
            }
        }

        CustomHandlerType? customHandlerType = null;
        var customHandlerGenericParameters = 0;
        if (customHandler != null)
        {
            var customHandlerMethod = method.ContainingType.GetMethod(customHandler, semanticModel, position);

            customHandlerType = customHandlerMethod != null ? Model.CustomHandlerType.Method : Model.CustomHandlerType.TypeMethod;
            customHandlerGenericParameters = customHandlerMethod?.TypeParameters.Length ?? 0;
        }

        if (string.IsNullOrWhiteSpace(typeNameFilter))
            typeNameFilter = null;

        if (string.IsNullOrWhiteSpace(excludeByTypeName))
            excludeByTypeName = null;

        if (string.IsNullOrWhiteSpace(assemblyNameFilter))
            assemblyNameFilter = null;

        var attributeFilterTypeName = attributeFilterType?.ToFullMetadataName();
        var excludeByAttributeTypeName = excludeByAttributeType?.ToFullMetadataName();
        var assemblyOfTypeName = assemblyType?.ToFullMetadataName();
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

        var hasError = assemblyType is { TypeKind: TypeKind.Error }
            || assignableTo is { TypeKind: TypeKind.Error }
            || attributeFilterType is { TypeKind: TypeKind.Error };

        return new(
            assignableToTypeName,
            assignableToTypeParametersCount,
            assemblyNameFilter,
            assignableToGenericArguments,
            assemblyOfTypeName,
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
            customHandlerType,
            customHandlerGenericParameters,
            asImplementedInterfaces,
            asSelf,
            location,
            hasError);
    }
}