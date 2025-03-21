using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceScan.SourceGenerator.Model;

record ParameterModel(string Type, string Name);

record MethodModel(
    string Namespace,
    string TypeName,
    string TypeMetadataName,
    string TypeModifiers,
    string MethodName,
    string MethodModifiers,
    EquatableArray<ParameterModel> Parameters,
    bool IsExtensionMethod,
    bool ReturnsVoid,
    string ReturnType)
{
    public string ParameterName => Parameters.First().Name;

    public static MethodModel Create(IMethodSymbol method, SyntaxNode syntax)
    {
        EquatableArray<ParameterModel> parameters = [.. method.Parameters.Select(p => new ParameterModel(p.Type.ToDisplayString(), p.Name))];

        var typeSyntax = syntax.FirstAncestorOrSelf<TypeDeclarationSyntax>();

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
            ReturnType: method.ReturnType.ToDisplayString());
    }

    private static string GetModifiers(SyntaxNode syntax)
    {
        return (syntax as MemberDeclarationSyntax)?.Modifiers.ToString() ?? "";
    }
}
