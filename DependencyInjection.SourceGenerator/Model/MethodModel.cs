using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DependencyInjection.SourceGenerator.Model;

record MethodModel(
    string Namespace,
    string TypeName,
    string TypeMetadataName,
    string TypeModifiers,
    string MethodName,
    string MethodModifiers,
    bool IsExtensionMethod,
    bool ReturnsVoid)
{
    public static MethodModel Create(IMethodSymbol method, SyntaxNode syntax)
    {
        return new MethodModel(
            Namespace: method.ContainingNamespace.ToDisplayString(),
            TypeName: method.ContainingType.Name,
            TypeMetadataName: method.ContainingType.ToFullMetadataName(),
            TypeModifiers: GetModifiers(GetTypeSyntax(syntax)),
            MethodName: method.Name,
            MethodModifiers: GetModifiers(syntax),
            IsExtensionMethod: method.IsExtensionMethod,
            ReturnsVoid: method.ReturnsVoid);
    }

    private static TypeDeclarationSyntax GetTypeSyntax(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is TypeDeclarationSyntax t)
                return t;
            parent = parent.Parent;
        }
        return null;
    }

    private static string GetModifiers(SyntaxNode syntax)
    {
        return (syntax as MemberDeclarationSyntax)?.Modifiers.ToString() ?? "";
    }
}
