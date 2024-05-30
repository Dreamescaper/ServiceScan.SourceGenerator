using Microsoft.CodeAnalysis;

namespace DependencyInjection.SourceGenerator;

public static class SymbolExtensions
{
    public static string ToFullMetadataName(this ISymbol symbol)
    {
        return symbol.ContainingNamespace.ToDisplayString() + "." + symbol.MetadataName;
    }
}
