using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;

namespace ServiceScan.SourceGenerator;

public partial class DependencyInjectionGenerator
{
    private static IEnumerable<(INamedTypeSymbol Type, INamedTypeSymbol[]? MatchedAssignableTypes)> FilterTypes
        (Compilation compilation, AttributeModel attribute, INamedTypeSymbol containingType)
    {
        var semanticModel = compilation.GetSemanticModel(attribute.Location.SourceTree);
        var position = attribute.Location.SourceSpan.Start;

        var assemblies = GetAssembliesToScan(compilation, attribute, containingType);

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

        var customHandlerMethod = attribute.CustomHandler != null && attribute.CustomHandlerType == CustomHandlerType.Method
            ? containingType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == attribute.CustomHandler)
            : null;

        foreach (var type in assemblies.SelectMany(GetTypesFromAssembly))
        {
            if (type.IsAbstract || !type.CanBeReferencedByName || type.TypeKind != TypeKind.Class)
                continue;

            // Static types are allowed for custom handlers (with type method)
            if (type.IsStatic && attribute.CustomHandlerType != CustomHandlerType.TypeMethod)
                continue;

            // Cannot use open generics with CustomHandler
            if (type.IsGenericType && attribute.CustomHandler != null)
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

            INamedTypeSymbol[] matchedTypes = null;
            if (assignableToType != null && !IsAssignableTo(type, assignableToType, out matchedTypes))
                continue;

            // Filter by custom handler method generic constraints
            if (customHandlerMethod != null && !SatisfiesGenericConstraints(type, customHandlerMethod))
            {
                continue;
            }

            if (!semanticModel.IsAccessible(position, type))
                continue;

            yield return (type, matchedTypes);
        }
    }

    private static bool IsAssignableTo(INamedTypeSymbol type, INamedTypeSymbol assignableTo, out INamedTypeSymbol[]? matchedTypes)
    {
        if (SymbolEqualityComparer.Default.Equals(type, assignableTo))
        {
            matchedTypes = [type];
            return true;
        }

        if (assignableTo.IsGenericType && assignableTo.IsDefinition)
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                matchedTypes = type.AllInterfaces
                    .Where(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, assignableTo))
                    .ToArray();

                return matchedTypes.Length > 0;
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, assignableTo))
                {
                    matchedTypes = [baseType];
                    return true;
                }

                baseType = baseType.BaseType;
            }
        }
        else
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                matchedTypes = [assignableTo];
                return type.AllInterfaces.Contains(assignableTo, SymbolEqualityComparer.Default);
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, assignableTo))
                {
                    matchedTypes = [baseType];
                    return true;
                }

                baseType = baseType.BaseType;
            }
        }

        matchedTypes = null;
        return false;
    }

    private static IEnumerable<IAssemblySymbol> GetAssembliesToScan(Compilation compilation, AttributeModel attribute, INamedTypeSymbol containingType)
    {
        var assemblyOfType = attribute.AssemblyOfTypeName is null
            ? null
            : compilation.GetTypeByMetadataName(attribute.AssemblyOfTypeName);

        if (assemblyOfType is not null)
        {
            return [assemblyOfType.ContainingAssembly];
        }

        if (attribute.AssemblyNameFilter is not null)
        {
            var assemblyNameRegex = BuildWildcardRegex(attribute.AssemblyNameFilter);

            return new[] { compilation.Assembly }
                .Concat(compilation.SourceModule.ReferencedAssemblySymbols)
                .Where(assembly => assemblyNameRegex.IsMatch(assembly.Name));
        }

        return [containingType.ContainingAssembly];
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

    [return: NotNullIfNotNull(nameof(wildcard))]
    private static Regex? BuildWildcardRegex(string? wildcard)
    {
        return wildcard is null
            ? null
            : new Regex($"^({Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(",", "|")})$");
    }

    private static bool SatisfiesGenericConstraints(INamedTypeSymbol type, IMethodSymbol customHandlerMethod)
    {
        if (customHandlerMethod.TypeParameters.Length == 0)
            return true;

        // Check constraints on the first type parameter (which will be the implementation type)
        // (Other type parameters could be checked recursively from the first type parameter)
        var typeParameter = customHandlerMethod.TypeParameters[0];

        var visitedTypeParameters = new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default);
        return SatisfiesGenericConstraints(type, typeParameter, customHandlerMethod, visitedTypeParameters);
    }

    private static bool SatisfiesGenericConstraints(INamedTypeSymbol type, ITypeParameterSymbol typeParameter, IMethodSymbol customHandlerMethod, HashSet<ITypeParameterSymbol> visitedTypeParameters)
    {
        // Prevent infinite recursion in circular constraint scenarios (e.g., X : ISmth<Y>, Y : ISmth<X>)
        if (!visitedTypeParameters.Add(typeParameter))
            return true;

        // Check reference type constraint
        if (typeParameter.HasReferenceTypeConstraint && type.IsValueType)
            return false;

        // Check value type constraint
        if (typeParameter.HasValueTypeConstraint && !type.IsValueType)
            return false;

        // Check unmanaged type constraint
        if (typeParameter.HasUnmanagedTypeConstraint && !type.IsUnmanagedType)
            return false;

        // Check constructor constraint
        if (typeParameter.HasConstructorConstraint)
        {
            var hasPublicParameterlessConstructor = type.Constructors.Any(c =>
                c.DeclaredAccessibility == Accessibility.Public &&
                c.Parameters.Length == 0 &&
                !c.IsStatic);

            if (!hasPublicParameterlessConstructor)
                return false;
        }

        // Check type constraints
        foreach (var constraintType in typeParameter.ConstraintTypes)
        {
            if (constraintType is INamedTypeSymbol namedConstraintType)
            {
                if (!SatisfiesConstraintType(type, namedConstraintType, customHandlerMethod, visitedTypeParameters))
                    return false;
            }
        }

        return true;
    }

    private static bool SatisfiesConstraintType(INamedTypeSymbol candidateType, INamedTypeSymbol constraintType, IMethodSymbol customHandlerMethod, HashSet<ITypeParameterSymbol> visitedTypeParameters)
    {
        var constraintHasTypeParameters = constraintType.TypeArguments.OfType<ITypeParameterSymbol>().Any();

        if (!constraintHasTypeParameters)
        {
            return IsAssignableTo(candidateType, constraintType, out _);
        }
        else
        {
            // We handle the case when method has multiple type arguments, e.g.
            // private static void CustomHandler<THandler, TCommand>(this IServiceCollection services)
            //      where THandler : class, ICommandHandler<TCommand>
            //      where TCommand : ISpecificCommand


            // First we check that type definitions match. E.g. if MyHandlerImplementation has interface (one or many) ICommandHandler<>.
            if (!IsAssignableTo(candidateType, constraintType.OriginalDefinition, out var matchedTypes))
                return false;

            // Then we need to check if any matched interfaces (let's say MyHandlerImplementation implements ICommandHandler<string> and ICommandHandler<MySpecificCommand>)
            // have matching type parameters (e.g. string does not implement ISpecificCommand, but MySpecificCommand - does).
            return matchedTypes.Any(matchedType => MatchedTypeSatisfiesConstraints(constraintType, customHandlerMethod, matchedType, visitedTypeParameters));
        }

        static bool MatchedTypeSatisfiesConstraints(INamedTypeSymbol constraintType, IMethodSymbol customHandlerMethod, INamedTypeSymbol matchedType, HashSet<ITypeParameterSymbol> visitedTypeParameters)
        {
            if (constraintType.TypeArguments.Length != matchedType.TypeArguments.Length)
                return false;

            for (var i = 0; i < constraintType.TypeArguments.Length; i++)
            {
                if (matchedType.TypeArguments[i] is not INamedTypeSymbol candidateTypeArgument)
                    return false;

                if (constraintType.TypeArguments[i] is ITypeParameterSymbol typeParameter)
                {
                    if (!SatisfiesGenericConstraints(candidateTypeArgument, typeParameter, customHandlerMethod, visitedTypeParameters))
                        return false;
                }
                else
                {
                    if (!SymbolEqualityComparer.Default.Equals(candidateTypeArgument, constraintType.TypeArguments[i]))
                        return false;
                }
            }

            return true;
        }
    }
}
