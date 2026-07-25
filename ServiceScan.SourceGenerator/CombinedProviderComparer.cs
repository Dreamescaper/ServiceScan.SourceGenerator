using System.Collections.Generic;
using ServiceScan.SourceGenerator.Model;
using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator;

using CombinedModel = (DiagnosticModel<MethodWithAttributesModel> Model, Compilation Compilation);

// Intentionally ignore Compilation changes when the attributed method model is unchanged.
//
// Type discovery scans whole assemblies and can walk base types and implemented interfaces.
// Treating every new IDE Compilation as a change would repeat that scan on virtually every
// typed character. The tradeoff is that the IDE's generated-code view can remain stale when
// only a candidate type changes. A command-line/MSBuild compilation creates a fresh generator
// driver and therefore still scans the current compilation. See
// docs/incremental-generator-design.md for the full rationale.
internal class CombinedProviderComparer : IEqualityComparer<CombinedModel>
{
    public static readonly CombinedProviderComparer Instance = new();

    public bool Equals(CombinedModel x, CombinedModel y)
    {
        return x.Model.Equals(y.Model);
    }

    public int GetHashCode(CombinedModel obj)
    {
        return obj.Model.GetHashCode();
    }
}
