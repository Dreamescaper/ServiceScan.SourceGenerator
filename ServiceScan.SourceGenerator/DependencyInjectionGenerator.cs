using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ServiceScan.SourceGenerator.Model;

namespace ServiceScan.SourceGenerator;

[Generator]
public partial class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context => context.AddSource("GenerateServiceRegistrationsAttribute.Generated.cs", SourceText.From(GenerateAttributeSource.Source, Encoding.UTF8)));

        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ServiceScan.SourceGenerator.GenerateServiceRegistrationsAttribute",
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax methodSyntax,
                transform: static (context, ct) => ParseMethodModel(context))
            .Where(method => method != null);

        var combinedProvider = methodProvider.Combine(context.CompilationProvider)
            .WithComparer(CombinedProviderComparer.Instance);

        var methodImplementationsProvider = combinedProvider
            .Select(static (context, ct) => FindServicesToRegister(context));

        context.RegisterSourceOutput(methodImplementationsProvider,
            static (context, src) =>
            {
                if (src.Diagnostic != null)
                    context.ReportDiagnostic(src.Diagnostic);

                if (src.Model == null)
                    return;

                var (method, registrations) = src.Model;
                string source = GenerateSource(method, registrations);

                context.AddSource($"{method.TypeName}_{method.MethodName}.Generated.cs", SourceText.From(source, Encoding.UTF8));
            });
    }

    private static string GenerateSource(MethodModel method, EquatableArray<ServiceRegistrationModel> registrations)
    {
        var sb = new StringBuilder();

        foreach (var registration in registrations)
        {
            if (registration.IsOpenGeneric)
            {
                sb.AppendLine($"            .Add{registration.Lifetime}(typeof({registration.ServiceTypeName}), typeof({registration.ImplementationTypeName}))");
            }
            else
            {
                if (registration.ResolveImplementation)
                {
                    sb.AppendLine($"            .Add{registration.Lifetime}<{registration.ServiceTypeName}>(s => s.GetRequiredService<{registration.ImplementationTypeName}>())");
                }
                else
                {
                    var addMethod = registration.KeySelectorMethodName != null
                        ? $"AddKeyed{registration.Lifetime}"
                        : $"Add{registration.Lifetime}";

                    var keyMethodInvocation = registration.KeySelectorMethodGeneric switch
                    {
                        true => $"{registration.KeySelectorMethodName}<{registration.ImplementationTypeName}>()",
                        false => $"{registration.KeySelectorMethodName}(typeof({registration.ImplementationTypeName}))",
                        null => null
                    };
                    sb.AppendLine($"            .{addMethod}<{registration.ServiceTypeName}, {registration.ImplementationTypeName}>({keyMethodInvocation})");
                }
            }
        }

        var returnType = method.ReturnsVoid ? "void" : "IServiceCollection";

        var namespaceDeclaration = method.Namespace is null ? "" : $"namespace {method.Namespace};";

        var source = $$"""
                using Microsoft.Extensions.DependencyInjection;

                {{namespaceDeclaration}}

                {{method.TypeModifiers}} class {{method.TypeName}}
                {
                    {{method.MethodModifiers}} {{returnType}} {{method.MethodName}}({{(method.IsExtensionMethod ? "this" : "")}} IServiceCollection {{method.ParameterName}})
                    {
                        {{(method.ReturnsVoid ? "" : "return ")}}{{method.ParameterName}}
                            {{sb.ToString().Trim()}};
                    }
                }
                """;

        return source;
    }
}
