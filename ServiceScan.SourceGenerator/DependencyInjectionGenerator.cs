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
        var statements = registrations.Select(registration =>
            GenerateRegistrationStatement(method.ParameterName, registration));

        return GenerateMethodSource(
            method,
            statements,
            usingNamespace: "Microsoft.Extensions.DependencyInjection",
            finalReturnFragment: GenerateParameterReturnFragment(method));
    }

    private static string GenerateRegistrationStatement(
        string parameterName,
        ServiceRegistrationModel registration)
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

        return $"{parameterName}.{call};";
    }

    private static string GenerateCollectionSource(MethodModel method, EquatableArray<string> collectionItems)
    {
        var itemsCode = string.Join(",\n", collectionItems.Select(item => $"            {item}"));
        var returnFragment = $"return [\n{itemsCode}\n        ];";

        return GenerateMethodSource(method, [], finalReturnFragment: returnFragment);
    }

    private static string GenerateCustomHandlingSource(MethodModel method, EquatableArray<CustomHandlerModel> customHandlers)
    {
        var invocations = customHandlers.Select(handler =>
            GenerateCustomHandlerStatement(method, handler));

        return GenerateMethodSource(
            method,
            invocations,
            finalReturnFragment: GenerateParameterReturnFragment(method));
    }

    private static string GenerateCustomHandlerStatement(MethodModel method, CustomHandlerModel handler)
    {
        if (handler.CustomHandlerType == CustomHandlerType.Template)
        {
            return handler.HandlerTarget;
        }

        var arguments = string.Join(", ", method.Parameters.Select(p => p.Name));
        var genericArguments = handler.CustomHandlerType == CustomHandlerType.Method
            ? $"<{string.Join(", ", handler.TypeArguments)}>"
            : "";

        return $"{handler.HandlerTarget}{genericArguments}({arguments});";
    }

    private static string GenerateMethodSource(
        MethodModel method,
        IEnumerable<string> bodyFragments,
        string? usingNamespace = null,
        string? finalReturnFragment = null)
    {
        var fragments = bodyFragments.ToList();
        if (finalReturnFragment != null)
            fragments.Add(finalReturnFragment);

        var methodBody = string.Join("\n        ", fragments);
        var usingDeclaration = usingNamespace is null ? "" : $"using {usingNamespace};\n\n";
        var namespaceDeclaration = method.Namespace is null ? "" : $"namespace {method.Namespace};";
        var parameters = string.Join(",", method.Parameters.Select((p, i) =>
            $"{(i == 0 && method.IsExtensionMethod ? "this" : "")} {p.Type} {p.Name}"));

        return $$"""
                {{usingDeclaration}}{{namespaceDeclaration}}

                {{method.TypeModifiers}} class {{method.TypeName}}
                {
                    {{method.MethodModifiers}} {{method.ReturnType}} {{method.MethodName}}({{parameters}})
                    {
                        {{methodBody}}
                    }
                }
                """;
    }

    private static string? GenerateParameterReturnFragment(MethodModel method)
    {
        return method.ReturnsVoid ? null : $"return {method.ParameterName};";
    }
}
