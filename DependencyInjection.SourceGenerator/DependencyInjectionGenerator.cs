using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DependencyInjection.SourceGenerator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static DependencyInjection.SourceGenerator.DiagnosticDescriptors;

namespace DependencyInjection.SourceGenerator;

[Generator]
public partial class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context => context.AddSource("GenerateServiceRegistrationsAttribute.Generated.cs", SourceText.From(GenerateAttributeSource.Source, Encoding.UTF8)));

        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "DependencyInjection.SourceGenerator.GenerateServiceRegistrationsAttribute",
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax methodSyntax,
                transform: static (context, ct) => ParseMethodModel(context))
            .Where(method => method != null);

        var combinedProvider = methodProvider.Combine(context.CompilationProvider)
            .WithComparer(CombinedProviderComparer.Instance);

        var methodImplementationsProvider = combinedProvider
            .Select(static (context, ct) => FindServicesToRegister(context));

        context.RegisterImplementationSourceOutput(methodImplementationsProvider,
            static (context, src) =>
            {
                if (src.Diagnostic != null)
                {
                    context.ReportDiagnostic(src.Diagnostic);
                    return;
                }

                var (method, registrations) = src.Model;

                var sb = new StringBuilder();

                foreach (var registration in registrations)
                {
                    if (registration.IsOpenGeneric)
                    {
                        sb.AppendLine($"            .Add{registration.Lifetime}(typeof({registration.ServiceTypeName}), typeof({registration.ImplementationTypeName}))");
                    }
                    else
                    {
                        sb.AppendLine($"            .Add{registration.Lifetime}<{registration.ServiceTypeName}, {registration.ImplementationTypeName}>()");
                    }
                }

                var returnType = method.ReturnsVoid ? "void" : "IServiceCollection";

                var source = $$"""
                using Microsoft.Extensions.DependencyInjection;

                namespace {{method.Namespace}};

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

    private static DiagnosticModel<MethodImplementationModel> FindServicesToRegister((DiagnosticModel<MethodWithAttributesModel>, Compilation) context)
    {
        var (diagnosticModel, compilation) = context;

        if (diagnosticModel.Diagnostic != null)
            return diagnosticModel.Diagnostic;

        var (method, attributes) = diagnosticModel.Model;

        var registrations = new List<ServiceRegistrationModel>();

        foreach (var attribute in attributes)
        {
            bool typesFound = false;

            var assembly = compilation.GetTypeByMetadataName(attribute.AssemblyOfTypeName ?? method.TypeMetadataName).ContainingAssembly;

            var assignableToType = attribute.AssignableToTypeName is null
                ? null
                : compilation.GetTypeByMetadataName(attribute.AssignableToTypeName);

            if (assignableToType != null && attribute.AssignableToGenericArguments != null)
            {
                var typeArguments = attribute.AssignableToGenericArguments.Value.Select(t => compilation.GetTypeByMetadataName(t)).ToArray();
                assignableToType = assignableToType.Construct(typeArguments);
            }

            var types = GetTypesFromAssembly(assembly)
                .Where(t => !t.IsAbstract && !t.IsStatic && t.CanBeReferencedByName && t.TypeKind == TypeKind.Class);

            if (attribute.TypeNameFilter != null)
            {
                var regex = $"^({Regex.Escape(attribute.TypeNameFilter).Replace(@"\*", ".*").Replace(",", "|")})$";
                types = types.Where(t => Regex.IsMatch(t.ToDisplayString(), regex));
            }

            foreach (var t in types)
            {
                var implementationType = t;

                INamedTypeSymbol matchedType = null;
                if (assignableToType != null && !IsAssignableTo(implementationType, assignableToType, out matchedType))
                    continue;

                IEnumerable<INamedTypeSymbol> serviceTypes = null;

                if (matchedType != null)
                {
                    serviceTypes = [matchedType];
                }
                else
                {
                    serviceTypes = attribute.AsImplementedInterfaces
                        ? implementationType.AllInterfaces
                        : [implementationType];
                }

                foreach (var serviceType in serviceTypes)
                {
                    if (implementationType.IsGenericType)
                    {
                        var implementationTypeName = implementationType.ConstructUnboundGenericType().ToDisplayString();
                        var serviceTypeName = serviceType.IsGenericType
                            ? serviceType.ConstructUnboundGenericType().ToDisplayString()
                            : serviceType.ToDisplayString();

                        var registration = new ServiceRegistrationModel(attribute.Lifetime, serviceTypeName, implementationTypeName, true);
                        registrations.Add(registration);
                    }
                    else
                    {

                        var registration = new ServiceRegistrationModel(attribute.Lifetime, serviceType.ToDisplayString(), implementationType.ToDisplayString(), false);
                        registrations.Add(registration);
                    }

                    typesFound = true;
                }
            }

            if (!typesFound)
                return Diagnostic.Create(NoMatchingTypesFound, attribute.Location);
        }

        return new MethodImplementationModel(method, new EquatableArray<ServiceRegistrationModel>([.. registrations]));
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
            attributeData[i] = AttributeModel.Create(context.Attributes[i]);

            if (!attributeData[i].HasSearchCriteria)
                return Diagnostic.Create(MissingSearchCriteria, attributeData[i].Location);
        }

        var model = MethodModel.Create(method, context.TargetNode);
        return new MethodWithAttributesModel(model, new EquatableArray<AttributeModel>(attributeData));
    }

    private static bool IsAssignableTo(INamedTypeSymbol type, INamedTypeSymbol assignableTo, out INamedTypeSymbol matchedType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, assignableTo))
        {
            matchedType = type;
            return true;
        }

        if (assignableTo.IsGenericType && assignableTo.IsDefinition)
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                var matchingInterface = type.AllInterfaces.FirstOrDefault(i => i.IsGenericType && SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, assignableTo));
                matchedType = matchingInterface;
                return matchingInterface != null;
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, assignableTo))
                {
                    matchedType = baseType;
                    return true;
                }

                baseType = baseType.BaseType;
            }
        }
        else
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                matchedType = assignableTo;
                return type.AllInterfaces.Contains(assignableTo, SymbolEqualityComparer.Default);
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, assignableTo))
                {
                    matchedType = baseType;
                    return true;
                }

                baseType = baseType.BaseType;
            }
        }

        matchedType = null;
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetTypesFromAssembly(IAssemblySymbol assemblySymbol)
    {
        var @namespace = assemblySymbol.GlobalNamespace;
        return GetTypesFromNamespace(@namespace);

        static IEnumerable<INamedTypeSymbol> GetTypesFromNamespace(INamespaceSymbol namespaceSymbol)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamedTypeSymbol namedType)
                {
                    yield return namedType;
                }
                else if (member is INamespaceSymbol nestedNamespace)
                {
                    foreach (var type in GetTypesFromNamespace(nestedNamespace))
                    {
                        yield return type;
                    }
                }
            }
        }
    }
}
