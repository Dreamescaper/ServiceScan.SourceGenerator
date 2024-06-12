using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator;

public static class SymbolExtensions
{
    public static string ToFullMetadataName(this ISymbol symbol)
    {
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? symbol.MetadataName
            : symbol.ContainingNamespace.ToDisplayString() + "." + symbol.MetadataName;
    }
}
