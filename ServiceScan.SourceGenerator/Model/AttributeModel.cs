using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Model;

record AttributeModel(
    EquatableArray<string>? AssignableToGenericArguments,
    EquatableArray<ServiceRegistrationModel>? RegistrationsFromAssembly, // if null, use types found from source code
    string Lifetime,
    string? TypeNameFilter,
    bool AsImplementedInterfaces,
    bool AsSelf,
    Location Location,
    bool HasErrors,
    TypeModel? AssignableToType)
{
    public bool HasSearchCriteria => TypeNameFilter != null || AssignableToType != null;

    public static AttributeModel Create(AttributeData attribute, Compilation compilation)
    {
        var assemblyType = attribute.NamedArguments.FirstOrDefault(a => a.Key == "FromAssemblyOf").Value.Value as INamedTypeSymbol;
        var assignableTo = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AssignableTo").Value.Value as INamedTypeSymbol;
        var asImplementedInterfaces = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsImplementedInterfaces").Value.Value is true;
        var asSelf = attribute.NamedArguments.FirstOrDefault(a => a.Key == "AsSelf").Value.Value is true;
        var typeNameFilter = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeNameFilter").Value.Value as string;
        
        EquatableArray<string>? assignableToGenericArguments = assignableTo != null && assignableTo.IsGenericType && !assignableTo.IsUnboundGenericType
            ? new EquatableArray<string>([.. assignableTo?.TypeArguments.Select(t => t.ToFullMetadataName())])
            : null;

        if (string.IsNullOrWhiteSpace(typeNameFilter))
            typeNameFilter = null;

        var lifetime = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Lifetime").Value.Value as int? switch
        {
            0 => "Singleton",
            1 => "Scoped",
            _ => "Transient"
        };

        var registrations = GetRegistrationsFromAssembly(assemblyType, compilation, typeNameFilter, asSelf, asImplementedInterfaces, assignableTo, assignableToGenericArguments, lifetime);

        var typeModel = assignableTo is not null ? TypeModel.Create(assignableTo) : null;

        var syntax = attribute.ApplicationSyntaxReference.SyntaxTree;
        var textSpan = attribute.ApplicationSyntaxReference.Span;
        var location = Location.Create(syntax, textSpan);

        var hasError = assemblyType is { TypeKind: TypeKind.Error } || assignableTo is { TypeKind: TypeKind.Error };

        return new(assignableToGenericArguments, registrations, lifetime, typeNameFilter, asImplementedInterfaces, asSelf, location, hasError, typeModel);
    }

    private static EquatableArray<ServiceRegistrationModel>? GetRegistrationsFromAssembly(
        INamedTypeSymbol? fromAssemblyOf,
        Compilation compilation,
        string? typeNameFilter,
        bool asSelf,
        bool asImplementedInterfaces,
        INamedTypeSymbol? assignableToType,
        EquatableArray<string>? assignableToGenericArguments,
        string lifetime)
    {
        if (fromAssemblyOf is null)
            return null;

        // if user specifies FromAssemblyOf = typeof(SomeType), but SomeType is from the same assembly as the method with the attribute
        if (SymbolEqualityComparer.Default.Equals(fromAssemblyOf.ContainingAssembly, compilation.Assembly))
            return null;

        var registrations = new List<ServiceRegistrationModel>();

        var types = fromAssemblyOf.ContainingAssembly.GetTypesFromAssembly()
            .Where(t => !t.IsAbstract && !t.IsStatic && t.CanBeReferencedByName && t.TypeKind == TypeKind.Class);

        if (typeNameFilter != null)
        {
            var regex = $"^({Regex.Escape(typeNameFilter).Replace(@"\*", ".*").Replace(",", "|")})$";
            types = types.Where(t => Regex.IsMatch(t.ToDisplayString(), regex));
        }

        foreach (var t in types)
        {
            var implementationType = t;

            INamedTypeSymbol matchedType = null;
            if (assignableToType != null && !SymbolExtensions.IsAssignableTo(implementationType, assignableToType, out matchedType))
                continue;

            IEnumerable<INamedTypeSymbol> serviceTypes = (asSelf, asImplementedInterfaces) switch
            {
                (true, true) => new[] { implementationType }.Concat(implementationType.AllInterfaces),
                (false, true) => implementationType.AllInterfaces,
                (true, false) => [implementationType],
                _ => [matchedType ?? implementationType]
            };

            foreach (var serviceType in serviceTypes)
            {
                if (implementationType.IsGenericType)
                {
                    var implementationTypeName = implementationType.ConstructUnboundGenericType().ToDisplayString();
                    var serviceTypeName = serviceType.IsGenericType
                        ? serviceType.ConstructUnboundGenericType().ToDisplayString()
                        : serviceType.ToDisplayString();

                    var registration = new ServiceRegistrationModel(lifetime, serviceTypeName, implementationTypeName, false, true);
                    registrations.Add(registration);
                }
                else
                {
                    var shouldResolve = asSelf && asImplementedInterfaces && !SymbolEqualityComparer.Default.Equals(implementationType, serviceType);
                    var registration = new ServiceRegistrationModel(lifetime, serviceType.ToDisplayString(), implementationType.ToDisplayString(), shouldResolve, false);
                    registrations.Add(registration);
                }
            }
        }

        return new(registrations.ToArray());
    }
}