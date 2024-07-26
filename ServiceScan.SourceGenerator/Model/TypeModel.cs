using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceScan.SourceGenerator.Model;

sealed record TypeModel
{
    public required string AssemblyName { get; init; }

    public required bool IsAbstract { get; init; }

    public required bool IsStatic { get; init; }

    public required bool CanBeReferencedByName { get; init; }

    public required TypeKind TypeKind { get; init; }

    public required string DisplayString { get; init; }

    public required string UnboundGenericDisplayString { get; init; }

    public required bool IsGenericType { get; init; }

    public required bool IsUnboundGenericType { get; init; }

    public required EquatableArray<TypeModel> AllInterfaces { get; init; }

    // OriginalDefinition is null for unbound generic types (to avoid recursion). For bound generic types, OriginalDefinition is the TypeModel representing the unbound generic type.
    // For non-generic types, OriginalDefinition is null.
    public required TypeModel? OriginalDefinition { get; init; }

    public required TypeModel? BaseType { get; init; }

    public static TypeModel? Create(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancel)
    {
        if (node is not TypeDeclarationSyntax typeDeclaration)
            return null;

        if (semanticModel.GetDeclaredSymbol(typeDeclaration, cancel) is not INamedTypeSymbol symbol)
            return null;

        return Create(symbol, new());
    }

    public static TypeModel Create(INamedTypeSymbol symbol, TypeCache cache)
    {
        if (cache.TryGet(symbol, out var typeModel))
            return typeModel;

        typeModel = new TypeModel
        {
            AssemblyName = symbol.ContainingAssembly.Name,
            IsAbstract = symbol.IsAbstract,
            IsStatic = symbol.IsStatic,
            IsGenericType = symbol.IsGenericType,
            IsUnboundGenericType = symbol.IsUnboundGenericType,
            CanBeReferencedByName = symbol.CanBeReferencedByName,
            TypeKind = symbol.TypeKind,
            DisplayString = symbol.ToDisplayString(),
            UnboundGenericDisplayString = symbol.IsGenericType
                ? symbol.ConstructUnboundGenericType().ToDisplayString()
                : symbol.ToDisplayString(),
            AllInterfaces = new([.. symbol.AllInterfaces.Select(t => Create(t, cache))]),
            OriginalDefinition = !symbol.IsGenericType
                ? null
                : symbol.IsUnboundGenericType ? null : Create(symbol.ConstructUnboundGenericType(), cache),
            BaseType = symbol.BaseType is not null ? Create(symbol.BaseType, cache) : null,
        };
        cache.Add(symbol, typeModel);
        return typeModel;
    }
}
