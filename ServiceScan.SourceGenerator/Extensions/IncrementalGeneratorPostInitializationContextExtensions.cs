using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ServiceScan.SourceGenerator.Extensions;

internal static class IncrementalGeneratorPostInitializationContextExtensions
{
    private const string EmbeddedAttributeSource = """
        namespace Microsoft.CodeAnalysis
        {
            internal sealed partial class EmbeddedAttribute : global::System.Attribute
            {
            }
        }
        """;

    public static void AddEmbeddedAttributeDefinition(this IncrementalGeneratorPostInitializationContext context)
    {
        context.AddSource("Microsoft.CodeAnalysis.EmbeddedAttribute", SourceText.From(EmbeddedAttributeSource, Encoding.UTF8));
    }
}
