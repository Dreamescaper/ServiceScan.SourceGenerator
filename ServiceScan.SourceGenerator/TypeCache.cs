using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;

namespace ServiceScan.SourceGenerator;

// must not be re-used between Compilations
internal class TypeCache
{
    private readonly Dictionary<INamedTypeSymbol, TypeModel> _cache = new(SymbolEqualityComparer.Default);

    public bool TryGet(INamedTypeSymbol key, out TypeModel type)
        => _cache.TryGetValue(key, out type);

    public void Add(INamedTypeSymbol key, TypeModel typeModel)
        => _cache[key] = typeModel;
}
