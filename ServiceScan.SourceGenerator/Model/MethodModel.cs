using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceScan.SourceGenerator.Model;

record ParameterModel(string Type, string Name);

record MethodModel(
    string? Namespace,
    string TypeName,
    string TypeMetadataName,
    string TypeModifiers,
    string MethodName,
    string MethodModifiers,
    EquatableArray<ParameterModel> Parameters,
    bool IsExtensionMethod,
    bool ReturnsVoid,
    string ReturnType,
    bool ReturnTypeIsCollection,
    string? CollectionElementTypeName)
{
    public string ParameterName => Parameters.First().Name;

    public static MethodModel Create(IMethodSymbol method, SyntaxNode syntax)
    {
        EquatableArray<ParameterModel> parameters = [.. method.Parameters
            .Select(p => new ParameterModel(p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), p.Name))];

        var typeSyntax = syntax.FirstAncestorOrSelf<TypeDeclarationSyntax>();

        var (returnTypeIsCollection, collectionElementTypeSymbol) = GetCollectionReturnInfo(method.ReturnType);
        var collectionElementTypeName = collectionElementTypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new MethodModel(
            Namespace: method.ContainingNamespace.IsGlobalNamespace ? null : method.ContainingNamespace.ToDisplayString(),
            TypeName: method.ContainingType.Name,
            TypeMetadataName: method.ContainingType.ToFullMetadataName(),
            TypeModifiers: GetModifiers(typeSyntax),
            MethodName: method.Name,
            MethodModifiers: GetModifiers(syntax),
            Parameters: parameters,
            IsExtensionMethod: method.IsExtensionMethod,
            ReturnsVoid: method.ReturnsVoid,
            ReturnType: method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ReturnTypeIsCollection: returnTypeIsCollection,
            CollectionElementTypeName: collectionElementTypeName);
    }

    public static (bool isCollection, ITypeSymbol? elementTypeSymbol) GetCollectionReturnInfo(ITypeSymbol returnType)
    {
        if (returnType is IArrayTypeSymbol arrayType)
            return (true, arrayType.ElementType);

        if (returnType is INamedTypeSymbol { IsGenericType: true, Arity: 1 } namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            return (true, namedType.TypeArguments[0]);

        return (false, null);
    }

    private static string GetModifiers(SyntaxNode? syntax)
    {
        return (syntax as MemberDeclarationSyntax)?.Modifiers.ToString() ?? "";
    }
}
