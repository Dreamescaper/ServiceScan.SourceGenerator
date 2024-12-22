using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ServiceScan.SourceGenerator.Extensions;
using ServiceScan.SourceGenerator.Model;

namespace ServiceScan.SourceGenerator;

[Generator]
public partial class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context =>
        {
            context.AddSource("ServiceScanAttributes.Generated.cs", SourceText.From(GenerateAttributeSource.Source, Encoding.UTF8));
        });

        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ServiceScan.SourceGenerator.GenerateServiceRegistrationsAttribute",
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax methodSyntax,
                transform: static (context, ct) => ParseRegisterMethodModel(context))
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

                var (method, registrations, customHandling) = src.Model;
                string source = customHandling.Count > 0
                    ? GenerateCustomHandlingSource(method, customHandling)
                    : GenerateRegistrationsSource(method, registrations);

                source = source.ReplaceLineEndings();

                context.AddSource($"{method.TypeName}_{method.MethodName}.Generated.cs", SourceText.From(source, Encoding.UTF8));
            });
    }

    private static string GenerateRegistrationsSource(MethodModel method, EquatableArray<ServiceRegistrationModel> registrations)
    {
        var registrationsCode = string.Join("\n", registrations.Select(registration =>
        {
            if (registration.IsOpenGeneric)
            {
                return $"            .Add{registration.Lifetime}(typeof({registration.ServiceTypeName}), typeof({registration.ImplementationTypeName}))";
            }
            else
            {
                if (registration.ResolveImplementation)
                {
                    return $"            .Add{registration.Lifetime}<{registration.ServiceTypeName}>(s => s.GetRequiredService<{registration.ImplementationTypeName}>())";
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

                    return $"            .{addMethod}<{registration.ServiceTypeName}, {registration.ImplementationTypeName}>({keyMethodInvocation})";
                }
            }
        }));

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
                            {{registrationsCode.Trim()}};
                    }
                }
                """;

        return source;
    }

    private static string GenerateCustomHandlingSource(MethodModel method, EquatableArray<CustomHandlerModel> customHandlers)
    {
        var invocations = string.Join("\n", customHandlers.Select(h =>
            $"        {h.HandlerMethodName}<{h.TypeName}>({string.Join(", ", method.Parameters.Select(p => p.Name))});"));

        var namespaceDeclaration = method.Namespace is null ? "" : $"namespace {method.Namespace};";
        var parameters = string.Join(",", method.Parameters.Select((p, i) =>
            $"{(i == 0 && method.IsExtensionMethod ? "this" : "")} {p.Type} {p.Name}"));

        var methodBody = $$"""
                {{invocations.Trim()}}
                {{(method.ReturnsVoid ? "" : $"return {method.ParameterName};")}}
        """;

        var source = $$"""
                {{namespaceDeclaration}}

                {{method.TypeModifiers}} class {{method.TypeName}}
                {
                    {{method.MethodModifiers}} {{method.ReturnType}} {{method.MethodName}}({{parameters}})
                    {
                        {{methodBody.Trim()}}
                    }
                }
                """;

        return source;
    }
}
