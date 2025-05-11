using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;

namespace ServiceScan.SourceGenerator;

public partial class DependencyInjectionGenerator
{
    private static IEnumerable<(INamedTypeSymbol Type, INamedTypeSymbol? MatchedAssignableType)> FilterTypes
        (Compilation compilation, AttributeModel attribute, INamedTypeSymbol containingType)
    {
        var assemblyOfType = attribute.AssemblyOfTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(attribute.AssemblyOfTypeName);
        
        var assembly = (assemblyOfType ?? containingType).ContainingAssembly;

        var assignableToType = attribute.AssignableToTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(attribute.AssignableToTypeName);

        var excludeAssignableToType = attribute.ExcludeAssignableToTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(attribute.ExcludeAssignableToTypeName);

        var attributeFilterType = attribute.AttributeFilterTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(attribute.AttributeFilterTypeName);

        var excludeByAttributeType = attribute.ExcludeByAttributeTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(attribute.ExcludeByAttributeTypeName);

        var typeNameFilterRegex = BuildWildcardRegex(attribute.TypeNameFilter);
        var excludeByTypeNameRegex = BuildWildcardRegex(attribute.ExcludeByTypeName);

        if (assignableToType != null && attribute.AssignableToGenericArguments != null)
        {
            var typeArguments = attribute.AssignableToGenericArguments.Value.Select(t => compilation.GetTypeByMetadataName(t)).ToArray();
            assignableToType = assignableToType.Construct(typeArguments);
        }

        if (excludeAssignableToType != null && attribute.ExcludeAssignableToGenericArguments != null)
        {
            var typeArguments = attribute.ExcludeAssignableToGenericArguments.Value.Select(t => compilation.GetTypeByMetadataName(t)).ToArray();
            excludeAssignableToType = excludeAssignableToType.Construct(typeArguments);
        }

        foreach (var type in GetTypesFromAssembly(assembly))
        {
            if (type.IsAbstract || type.IsStatic || !type.CanBeReferencedByName || type.TypeKind != TypeKind.Class)
                continue;

            if (attributeFilterType != null)
            {
                if (!type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeFilterType)))
                    continue;
            }

            if (excludeByAttributeType != null)
            {
                if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, excludeByAttributeType)))
                    continue;
            }

            if (typeNameFilterRegex != null && !typeNameFilterRegex.IsMatch(type.ToDisplayString()))
                continue;

            if (excludeByTypeNameRegex != null && excludeByTypeNameRegex.IsMatch(type.ToDisplayString()))
                continue;
            
            if (excludeAssignableToType != null && IsAssignableTo(type, excludeAssignableToType, out _))
                continue;

            INamedTypeSymbol matchedType = null;
            if (assignableToType != null && !IsAssignableTo(type, assignableToType, out matchedType))
                continue;

            yield return (type, matchedType);
        }
    }

    private static bool IsAssignableTo(INamedTypeSymbol type, INamedTypeSymbol assignableTo, out INamedTypeSymbol? matchedType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, assignableTo))
        {
            matchedType = type;
            return true;
        }

        if (assignableTo.IsGenericType && assignableTo.IsDefinition)
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                var matchingInterface = type.AllInterfaces.FirstOrDefault(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, assignableTo));
                matchedType = matchingInterface;
                return matchingInterface != null;
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, assignableTo))
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
                return type.AllInterfaces.Contains(assignableTo, SymbolEqualityComparer.Default);
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, assignableTo))
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

    private static IEnumerable<INamedTypeSymbol> GetTypesFromAssembly(IAssemblySymbol assemblySymbol)
    {
        var @namespace = assemblySymbol.GlobalNamespace;
        return GetTypesFromNamespaceOrType(@namespace);

        static IEnumerable<INamedTypeSymbol> GetTypesFromNamespaceOrType(INamespaceOrTypeSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                if (member is INamespaceOrTypeSymbol namespaceOrType)
                {
                    if (member is INamedTypeSymbol namedType)
                    {
                        yield return namedType;
                    }

                    foreach (var type in GetTypesFromNamespaceOrType(namespaceOrType))
                    {
                        yield return type;
                    }
                }
            }
        }
    }

    private static Regex? BuildWildcardRegex(string? wildcard)
    {
        return wildcard is null
            ? null
            : new Regex($"^({Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(",", "|")})$");
    }
}
