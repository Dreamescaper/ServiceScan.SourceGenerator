using System.Collections.Generic;
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
            context.AddEmbeddedAttributeDefinition();
            context.AddSource("ServiceScanAttributes.Generated.cs", SourceText.From(GenerateAttributeInfo.Source, Encoding.UTF8));
        });

        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                GenerateAttributeInfo.MetadataName,
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax methodSyntax,
                transform: static (context, ct) => ParseRegisterMethodModel(context))
            .Where(method => method != null);

        var combinedProvider = methodProvider.Combine(context.CompilationProvider)
            .WithComparer(CombinedProviderComparer.Instance);

        var methodImplementationsProvider = combinedProvider
            .Select(static (context, ct) => FindServicesToRegister(context));

        context.RegisterSourceOutput(methodImplementationsProvider,
            static (context, src) => GenerateSource(context, src));

        var handlerMethodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                GenerateAttributeInfo.HandlerMetadataName,
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax,
                transform: static (context, ct) => ParseHandlerMethodModel(context))
            .Where(method => method != null);

        var combinedHandlerProvider = handlerMethodProvider.Combine(context.CompilationProvider)
            .WithComparer(CombinedProviderComparer.Instance);

        var handlerImplementationsProvider = combinedHandlerProvider
            .Select(static (context, ct) => FindServicesToRegister(context));

        context.RegisterSourceOutput(handlerImplementationsProvider,
            static (context, src) => GenerateSource(context, src));
    }

    private static void GenerateSource(SourceProductionContext context, DiagnosticModel<MethodImplementationModel> src)
    {
        if (src.Diagnostic != null)
            context.ReportDiagnostic(src.Diagnostic);

        if (src.Model == null)
            return;

        var (method, registrations, customHandling, collectionItems) = src.Model;
        string source = (registrations.Count, method.ReturnTypeIsCollection) switch
        {
            ( > 0, _) => GenerateRegistrationsSource(method, registrations),
            (_, true) => GenerateCollectionSource(method, collectionItems),
            _ => GenerateCustomHandlingSource(method, customHandling)
        };

        source = source.ReplaceLineEndings();

        context.AddSource($"{method.TypeName}_{method.MethodName}.Generated.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateRegistrationsSource(MethodModel method, EquatableArray<ServiceRegistrationModel> registrations)
    {
        // Emit one AddXxx call per statement rather than a single fluent chain. A chained
        // shape (services.Add()...Add()...Add();) is a single expression whose parse tree
        // depth grows linearly with the registration count; Roslyn's CS8078 ("expression is
        // too long or complex to compile") caps that around ~1000 links. Statement-per-line
        // avoids the cap entirely and matches the shape already used by
        // GenerateCustomHandlingSource.
        var statements = registrations
            .Select(registration =>
            {
                string call;
                if (registration.IsOpenGeneric)
                {
                    call = $"Add{registration.Lifetime}(typeof({registration.ServiceTypeName}), typeof({registration.ImplementationTypeName}))";
                }
                else if (registration.ResolveImplementation)
                {
                    call = $"Add{registration.Lifetime}<{registration.ServiceTypeName}>(s => s.GetRequiredService<{registration.ImplementationTypeName}>())";
                }
                else
                {
                    var addMethod = registration.KeySelector != null
                        ? $"AddKeyed{registration.Lifetime}"
                        : $"Add{registration.Lifetime}";

                    var keySelectorInvocation = registration.KeySelectorType switch
                    {
                        KeySelectorType.GenericMethod => $"{registration.KeySelector}<{registration.ImplementationTypeName}>()",
                        KeySelectorType.Method => $"{registration.KeySelector}(typeof({registration.ImplementationTypeName}))",
                        KeySelectorType.TypeMember => $"{registration.ImplementationTypeName}.{registration.KeySelector}",
                        _ => null
                    };

                    call = $"{addMethod}<{registration.ServiceTypeName}, {registration.ImplementationTypeName}>({keySelectorInvocation})";
                }

                return $"{method.ParameterName}.{call};";
            })
            .ToList();

        var returnType = method.ReturnsVoid ? "void" : "IServiceCollection";
        var namespaceDeclaration = method.Namespace is null ? "" : $"namespace {method.Namespace};";

        var registrationsCode = string.Join("\n        ", statements);
        var returnStatement = method.ReturnsVoid ? "" : $"return {method.ParameterName};";

        string methodBody;
        if (statements.Count == 0)
            methodBody = returnStatement;
        else if (method.ReturnsVoid)
            methodBody = registrationsCode;
        else
            methodBody = $"{registrationsCode}\n        {returnStatement}";

        var source = $$"""
                using Microsoft.Extensions.DependencyInjection;

                {{namespaceDeclaration}}

                {{method.TypeModifiers}} class {{method.TypeName}}
                {
                    {{method.MethodModifiers}} {{returnType}} {{method.MethodName}}({{(method.IsExtensionMethod ? "this" : "")}} IServiceCollection {{method.ParameterName}})
                    {
                        {{methodBody}}
                    }
                }
                """;

        return source;
    }

    private static string GenerateCollectionSource(MethodModel method, EquatableArray<string> collectionItems)
    {
        var namespaceDeclaration = method.Namespace is null ? "" : $"namespace {method.Namespace};";
        var parameters = string.Join(",", method.Parameters.Select((p, i) =>
            $"{(i == 0 && method.IsExtensionMethod ? "this" : "")} {p.Type} {p.Name}"));

        var itemsCode = string.Join(",\n", collectionItems.Select(item => $"            {item}"));
        var methodBody = $"return [\n{itemsCode}\n        ];";

        var source = $$"""
                {{namespaceDeclaration}}

                {{method.TypeModifiers}} class {{method.TypeName}}
                {
                    {{method.MethodModifiers}} {{method.ReturnType}} {{method.MethodName}}({{parameters}})
                    {
                        {{methodBody}}
                    }
                }
                """;

        return source;
    }

    private static string GenerateCustomHandlingSource(MethodModel method, EquatableArray<CustomHandlerModel> customHandlers)
    {
        var invocations = string.Join("\n", customHandlers.Select(h =>
        {
            if (h.CustomHandlerType == CustomHandlerType.Template)
            {
                return $"        {h.HandlerMethodName}";
            }
            else if (h.CustomHandlerType == CustomHandlerType.Method)
            {
                var genericArguments = string.Join(", ", h.TypeArguments);
                var arguments = string.Join(", ", method.Parameters.Select(p => p.Name));
                return $"        {h.HandlerMethodName}<{genericArguments}>({arguments});";
            }
            else
            {
                var arguments = string.Join(", ", method.Parameters.Select(p => p.Name));
                return $"        {h.TypeName}.{h.HandlerMethodName}({arguments});";
            }
        }));

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