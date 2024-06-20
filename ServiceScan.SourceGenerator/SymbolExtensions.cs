using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;

namespace ServiceScan.SourceGenerator;

internal static class SymbolExtensions
{
    public static string ToFullMetadataName(this ISymbol symbol)
    {
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? symbol.MetadataName
            : symbol.ContainingNamespace.ToDisplayString() + "." + symbol.MetadataName;
    }

    public static IEnumerable<INamedTypeSymbol> GetTypesFromAssembly(this IAssemblySymbol assemblySymbol)
    {
        var @namespace = assemblySymbol.GlobalNamespace;
        return GetTypesFromNamespace(@namespace);

        static IEnumerable<INamedTypeSymbol> GetTypesFromNamespace(INamespaceSymbol namespaceSymbol)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamedTypeSymbol namedType)
                {
                    yield return namedType;
                }
                else if (member is INamespaceSymbol nestedNamespace)
                {
                    foreach (var type in GetTypesFromNamespace(nestedNamespace))
                    {
                        yield return type;
                    }
                }
            }
        }
    }

    public static List<ServiceRegistrationModel> GetRegistrations(
        IEnumerable<TypeModel> types,
        TypeModel? assignableToType,
        string? typeNameFilter,
        bool asSelf,
        bool asImplementedInterfaces,
        string lifetime)
    {
        var registrations = new List<ServiceRegistrationModel>();

        types = types.Where(t => !t.IsAbstract && !t.IsStatic && t.CanBeReferencedByName && t.TypeKind == TypeKind.Class);

        if (typeNameFilter != null)
        {
            var regex = $"^({Regex.Escape(typeNameFilter).Replace(@"\*", ".*").Replace(",", "|")})$";
            types = types.Where(t => Regex.IsMatch(t.DisplayString, regex));
        }

        foreach (var t in types)
        {
            var implementationType = t;

            TypeModel? matchedType = null;
            if (assignableToType != null && !IsAssignableTo(implementationType, assignableToType, out matchedType))
                continue;

            IEnumerable<TypeModel> serviceTypes = (asSelf, asImplementedInterfaces) switch
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
                    var implementationTypeName = implementationType.UnboundGenericDisplayString;
                    var serviceTypeName = serviceType.IsGenericType
                        ? serviceType.UnboundGenericDisplayString
                        : serviceType.DisplayString;

                    var registration = new ServiceRegistrationModel(lifetime, serviceTypeName, implementationTypeName, false, true);
                    registrations.Add(registration);
                }
                else
                {
                    var shouldResolve = asSelf && asImplementedInterfaces && implementationType != serviceType;
                    var registration = new ServiceRegistrationModel(lifetime, serviceType.DisplayString, implementationType.DisplayString, shouldResolve, false);
                    registrations.Add(registration);
                }
            }
        }

        return registrations;
    }

    public static bool IsAssignableTo(TypeModel type, TypeModel assignableTo, out TypeModel matchedType)
    {
        if (type == assignableTo)
        {
            matchedType = type;
            return true;
        }

        if (assignableTo.IsGenericType && assignableTo.IsUnboundGenericType)
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                var matchingInterface = type.AllInterfaces.FirstOrDefault(i => i.IsGenericType && i.OriginalDefinition == assignableTo);
                matchedType = matchingInterface;
                return matchingInterface != null;
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.OriginalDefinition == assignableTo)
                {
                    matchedType = baseType;
                    return true;
                }

                baseType = baseType.BaseType;
            }
        }
        else
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                matchedType = assignableTo;
                return type.AllInterfaces.Contains(assignableTo);
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType == assignableTo)
                {
                    matchedType = baseType;
                    return true;
                }

                baseType = baseType.BaseType;
            }
        }

        matchedType = null;
        return false;
    }
}
