using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ServiceScan.SourceGenerator.Model;
using static ServiceScan.SourceGenerator.DiagnosticDescriptors;

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

        var typeProvider = context.SyntaxProvider.CreateSyntaxProvider(
            (node, _) => node is TypeDeclarationSyntax,
            (ctx, ct) => TypeModel.Create(ctx.Node, ctx.SemanticModel, ct))
            .Where(x => x is not null)
            .Collect();

        var combinedProvider = methodProvider.Combine(typeProvider);

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

                var sb = new StringBuilder();

                foreach (var registration in registrations)
                {
                    if (registration.IsOpenGeneric)
                    {
                        sb.Append($"            .Add{registration.Lifetime}(typeof({registration.ServiceTypeName}), typeof({registration.ImplementationTypeName}))\n");
                    }
                    else
                    {
                        if (registration.ResolveImplementation)
                            sb.Append($"            .Add{registration.Lifetime}<{registration.ServiceTypeName}>(s => s.GetRequiredService<{registration.ImplementationTypeName}>())\n");
                        else
                            sb.Append($"            .Add{registration.Lifetime}<{registration.ServiceTypeName}, {registration.ImplementationTypeName}>()\n");
                    }
                }

                var returnType = method.ReturnsVoid ? "void" : "IServiceCollection";

                var namespaceDeclaration = method.Namespace is null ? "" : $"namespace {method.Namespace};";

                var source = $$"""
                using Microsoft.Extensions.DependencyInjection;

                {{namespaceDeclaration}}

                {{method.TypeModifiers}} class {{method.TypeName}}
                {
                    {{method.MethodModifiers}} {{returnType}} {{method.MethodName}}({{(method.IsExtensionMethod ? "this" : "")}} IServiceCollection services)
                    {
                        {{(method.ReturnsVoid ? "" : "return ")}}services
                            {{sb.ToString().Trim()}};
                    }
                }
                """;

                context.AddSource($"{method.TypeName}_{method.MethodName}.Generated.cs", SourceText.From(source, Encoding.UTF8));
            });
    }

    private static List<ServiceRegistrationModel> GetRegistrationsFromSourceCode(AttributeModel attribute, ImmutableArray<TypeModel> sourceTypes)
    {
        var assignableToType = attribute.AssignableToType;

        var types = sourceTypes
            .GroupBy(t => t.DisplayString).Select(g => g.First()) // distinct-by fully qualified name to account for partial classes
            .Where(t => !t.IsAbstract && !t.IsStatic && t.CanBeReferencedByName && t.TypeKind == TypeKind.Class);

        if (attribute.TypeNameFilter != null)
        {
            var regex = $"^({Regex.Escape(attribute.TypeNameFilter).Replace(@"\*", ".*").Replace(",", "|")})$";
            types = types.Where(t => Regex.IsMatch(t.DisplayString, regex));
        }

        var registrations = new List<ServiceRegistrationModel>();
        foreach (var t in types)
        {
            var implementationType = t;

            TypeModel? matchedType = null;
            if (assignableToType != null && !SymbolExtensions.IsAssignableTo(implementationType, assignableToType, out matchedType))
                continue;

            IEnumerable<TypeModel> serviceTypes = (attribute.AsSelf, attribute.AsImplementedInterfaces) switch
            {
                (true, true) => new[] { implementationType }.Concat(implementationType.AllInterfaces),
                (false, true) => implementationType.AllInterfaces,
                (true, false) => [implementationType],
                _ => [matchedType ?? implementationType]
            };

            foreach (var serviceType in serviceTypes)
            {
                if (implementationType.IsGenericType)
                {
                    var implementationTypeName = implementationType.UnboundGenericDisplayString;
                    var serviceTypeName = serviceType.IsGenericType
                        ? serviceType.UnboundGenericDisplayString
                        : serviceType.DisplayString;

                    var registration = new ServiceRegistrationModel(attribute.Lifetime, serviceTypeName, implementationTypeName, false, true);
                    registrations.Add(registration);
                }
                else
                {
                    var shouldResolve = attribute.AsSelf && attribute.AsImplementedInterfaces && implementationType != serviceType;
                    var registration = new ServiceRegistrationModel(attribute.Lifetime, serviceType.DisplayString, implementationType.DisplayString, shouldResolve, false);
                    registrations.Add(registration);
                }
            }
        }

        return registrations;
    }

    private static DiagnosticModel<MethodImplementationModel> FindServicesToRegister((DiagnosticModel<MethodWithAttributesModel>, ImmutableArray<TypeModel>) context)
    {
        var (diagnosticModel, sourceTypes) = context;
        var diagnostic = diagnosticModel.Diagnostic;

        if (diagnostic != null)
            return diagnostic;

        var (method, attributes) = diagnosticModel.Model;

        var registrations = new List<ServiceRegistrationModel>();

        foreach (var attribute in attributes)
        {
            // get registrations from the assembly specified in the attribute or from source code
            var regs = attribute.RegistrationsFromAssembly?.ToList()
                ?? GetRegistrationsFromSourceCode(attribute, sourceTypes);

            if (!regs.Any())
                diagnostic ??= Diagnostic.Create(NoMatchingTypesFound, attribute.Location);

            registrations.AddRange(regs);
        }

        var implementationModel = new MethodImplementationModel(method, new EquatableArray<ServiceRegistrationModel>([.. registrations]));
        return new(diagnostic, implementationModel);
    }

    private static DiagnosticModel<MethodWithAttributesModel> ParseMethodModel(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IMethodSymbol method)
            return null;

        if (!method.IsPartialDefinition)
            return Diagnostic.Create(NotPartialDefinition, method.Locations[0]);

        var serviceCollectionType = context.SemanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

        if (!method.ReturnsVoid && !SymbolEqualityComparer.Default.Equals(method.ReturnType, serviceCollectionType))
            return Diagnostic.Create(WrongReturnType, method.Locations[0]);

        if (method.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceCollectionType))
            return Diagnostic.Create(WrongMethodParameters, method.Locations[0]);

        var attributeData = new AttributeModel[context.Attributes.Length];
        for (var i = 0; i < context.Attributes.Length; i++)
        {
            attributeData[i] = AttributeModel.Create(context.Attributes[i], context.SemanticModel.Compilation);

            if (!attributeData[i].HasSearchCriteria)
                return Diagnostic.Create(MissingSearchCriteria, attributeData[i].Location);

            if (attributeData[i].HasErrors)
                return null;
        }

        var model = MethodModel.Create(method, context.TargetNode);
        return new MethodWithAttributesModel(model, new EquatableArray<AttributeModel>(attributeData));
    }
}
