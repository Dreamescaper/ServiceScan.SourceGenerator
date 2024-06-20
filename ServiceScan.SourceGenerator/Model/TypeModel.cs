using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceScan.SourceGenerator.Model;

sealed record TypeModel
{
    public required bool IsAbstract { get; init; }

    public required bool IsStatic { get; init; }

    public required bool CanBeReferencedByName { get; init; }

    public required TypeKind TypeKind { get; init; }

    public required string DisplayString { get; init; }

    public required string UnboundGenericDisplayString { get; init; }

    public required bool IsGenericType { get; init; }

    public required bool IsUnboundGenericType { get; init; }

    //public required bool IsDefinition { get; init; }

    public required EquatableArray<TypeModel> AllInterfaces { get; init; }

    // OriginalDefinition is null for unbound generic types. For bound generic types, OriginalDefinition is the TypeModel representing the unbound generic type.
    public required TypeModel? OriginalDefinition { get; init; }

    public required TypeModel? BaseType { get; init; }

    public static TypeModel? Create(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancel)
    {
        if (node is not TypeDeclarationSyntax typeDeclaration)
            return null;

        var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration, cancel) as INamedTypeSymbol;
        if (symbol is null)
            return null;

        return Create(symbol);
    }

    public static TypeModel? Create(INamedTypeSymbol symbol)
    {
        return new TypeModel
        {
            IsAbstract = symbol.IsAbstract,
            IsStatic = symbol.IsStatic,
            IsGenericType = symbol.IsGenericType,
            IsUnboundGenericType = symbol.IsUnboundGenericType,
            //IsDefinition = symbol.IsDefinition,
            CanBeReferencedByName = symbol.CanBeReferencedByName,
            TypeKind = symbol.TypeKind,
            DisplayString = symbol.ToDisplayString(),
            UnboundGenericDisplayString = symbol.IsGenericType
                ? symbol.ConstructUnboundGenericType().ToDisplayString()
                : symbol.ToDisplayString(),
            AllInterfaces = new([.. symbol.AllInterfaces.Select(Create)]),
            OriginalDefinition = !symbol.IsGenericType
                ? null
                : symbol.IsUnboundGenericType ? null : Create(symbol.ConstructUnboundGenericType()),
            BaseType = symbol.BaseType is not null ? Create(symbol.BaseType) : null,
        };
    }
}
