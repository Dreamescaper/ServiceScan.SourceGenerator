using System;
using System.Collections.Generic;
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

                context.AddSource($"{method.TypeName}_{method.MethodName}.Generated.cs", SourceText.From(source, Encoding.UTF8));
            });
    }

    private static DiagnosticModel<MethodImplementationModel> FindServicesToRegister((DiagnosticModel<MethodWithAttributesModel>, Compilation) context)
    {
        var (diagnosticModel, compilation) = context;
        var diagnostic = diagnosticModel.Diagnostic;

        if (diagnostic != null)
            return diagnostic;

        var (method, attributes) = diagnosticModel.Model;

        var registrations = new List<ServiceRegistrationModel>();

        foreach (var attribute in attributes)
        {
            bool typesFound = false;

            var containingType = compilation.GetTypeByMetadataName(method.TypeMetadataName);

            var assembly = (attribute.AssemblyOfTypeName is null
                ? containingType
                : compilation.GetTypeByMetadataName(attribute.AssemblyOfTypeName)).ContainingAssembly;

            var assignableToType = attribute.AssignableToTypeName is null
                ? null
                : compilation.GetTypeByMetadataName(attribute.AssignableToTypeName);

            var keySelectorMethod = attribute.KeySelector is null
                ? null
                : containingType.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m =>
                    m.IsStatic && m.Name == attribute.KeySelector);

            if (attribute.KeySelector != null)
            {
                if (keySelectorMethod is null)
                    return Diagnostic.Create(KeySelectorMethodNotFound, attribute.Location);

                if (keySelectorMethod.ReturnsVoid)
                    return Diagnostic.Create(KeySelectorMethodHasIncorrectSignature, attribute.Location);

                var validGenericKeySelector = keySelectorMethod.TypeArguments.Length == 1 && keySelectorMethod.Parameters.Length == 0;
                var validNonGenericKeySelector = !keySelectorMethod.IsGenericMethod && keySelectorMethod.Parameters is [{ Type.Name: nameof(Type) }];

                if (!validGenericKeySelector && !validNonGenericKeySelector)
                    return Diagnostic.Create(KeySelectorMethodHasIncorrectSignature, attribute.Location);
            }

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

                IEnumerable<INamedTypeSymbol> serviceTypes = (attribute.AsSelf, attribute.AsImplementedInterfaces) switch
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
                        var implementationTypeName = implementationType.ConstructUnboundGenericType().ToDisplayString();
                        var serviceTypeName = serviceType.IsGenericType
                            ? serviceType.ConstructUnboundGenericType().ToDisplayString()
                            : serviceType.ToDisplayString();

                        var registration = new ServiceRegistrationModel(
                            attribute.Lifetime,
                            serviceTypeName,
                            implementationTypeName,
                            false,
                            true,
                            keySelectorMethod?.Name,
                            keySelectorMethod?.IsGenericMethod);

                        registrations.Add(registration);
                    }
                    else
                    {
                        var shouldResolve = attribute.AsSelf && attribute.AsImplementedInterfaces && !SymbolEqualityComparer.Default.Equals(implementationType, serviceType);
                        var registration = new ServiceRegistrationModel(
                            attribute.Lifetime,
                            serviceType.ToDisplayString(),
                            implementationType.ToDisplayString(),
                            shouldResolve,
                            false,
                            keySelectorMethod?.Name,
                            keySelectorMethod?.IsGenericMethod);
                        registrations.Add(registration);
                    }

                    typesFound = true;
                }
            }

            if (!typesFound)
                diagnostic ??= Diagnostic.Create(NoMatchingTypesFound, attribute.Location);
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
            attributeData[i] = AttributeModel.Create(context.Attributes[i]);

            if (!attributeData[i].HasSearchCriteria)
                return Diagnostic.Create(MissingSearchCriteria, attributeData[i].Location);

            if (attributeData[i].HasErrors)
                return null;
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
