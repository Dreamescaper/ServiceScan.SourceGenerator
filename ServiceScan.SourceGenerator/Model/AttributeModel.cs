﻿using System.Linq;
using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Model;

enum KeySelectorType { Method, GenericMethod, TypeMember };

record AttributeModel(
    string? AssignableToTypeName,
    EquatableArray<string>? AssignableToGenericArguments,
    string? AssemblyOfTypeName,
    string? AttributeFilterTypeName,
    string Lifetime,
    string? TypeNameFilter,
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
        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;
        var attributeFilterType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AttributeFilter").Value.Value as INamedTypeSymbol;
        var asImplementedInterfaces = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsImplementedInterfaces").Value.Value is true;
        var asSelf = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsSelf").Value.Value is true;
        var typeNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeNameFilter").Value.Value as string;
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

        var attributeFilterTypeName = attributeFilterType?.ToFullMetadataName();
        var assemblyOfTypeName = assemblyType?.ToFullMetadataName();
        var assignableToTypeName = assignableTo?.ToFullMetadataName();
        EquatableArray<string>? assignableToGenericArguments = assignableTo != null && assignableTo.IsGenericType && !assignableTo.IsUnboundGenericType
            ? [.. assignableTo?.TypeArguments.Select(t => t.ToFullMetadataName())]
            : null;

        var lifetime = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int? switch
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
            assignableToGenericArguments,
            assemblyOfTypeName,
            attributeFilterTypeName,
            lifetime,
            typeNameFilter,
            keySelector,
            keySelectorType,
            customHandler,
            asImplementedInterfaces,
            asSelf,
            location,
            hasError);
    }
}