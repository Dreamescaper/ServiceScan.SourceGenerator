using System.Collections.Generic;
using DependencyInjection.SourceGenerator.Model;
using Microsoft.CodeAnalysis;

namespace DependencyInjection.SourceGenerator;

using CombinedModel = (DiagnosticModel<MethodWithAttributesModel> Model, Compilation Compilation);

// We only compare Model here and ignore Compilation, as I don't want to run it on every input.
internal class CombinedProviderComparer : IEqualityComparer<CombinedModel>
{
    public static CombinedProviderComparer Instance = new();

    public bool Equals(CombinedModel x, CombinedModel y)
    {
        return x.Model.Equals(y.Model);
    }

    public int GetHashCode(CombinedModel obj)
    {
        return obj.Model.GetHashCode();
    }
}
