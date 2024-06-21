using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

        // types from external assemblies
        var refs = context.MetadataReferencesProvider
            .Collect()
            .SelectMany(static (refs, ct) =>
            {
                var comp = CSharpCompilation.Create("temp", references: refs);
                return GetTypesFromNamespace(comp.GlobalNamespace).Select(TypeModel.Create);
            })
            .Collect();

        // types from source code
        var typeProvider = context.SyntaxProvider.CreateSyntaxProvider(
            (node, _) => node is TypeDeclarationSyntax,
            (ctx, ct) => TypeModel.Create(ctx.Node, ctx.SemanticModel, ct))
            .Where(x => x is not null)
            .Collect();

        var asmName = context.CompilationProvider.Select((c, _) => c.AssemblyName);

        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ServiceScan.SourceGenerator.GenerateServiceRegistrationsAttribute",
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax methodSyntax,
                transform: static (context, ct) => ParseMethodModel(context))
            .Where(method => method != null);

        var combinedProvider = methodProvider.Combine(refs.Combine(typeProvider).Combine(asmName));

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
                            sb.AppendLine($"            .Add{registration.Lifetime}<{registration.ServiceTypeName}>(s => s.GetRequiredService<{registration.ImplementationTypeName}>())");
                        else
                            sb.AppendLine($"            .Add{registration.Lifetime}<{registration.ServiceTypeName}, {registration.ImplementationTypeName}>()");
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

    private static DiagnosticModel<MethodImplementationModel> FindServicesToRegister(
        (DiagnosticModel<MethodWithAttributesModel>, ((ImmutableArray<TypeModel>, ImmutableArray<TypeModel>), string)) context)
    {
        var (diagnosticModel, ((refTypes, sourceTypes), asmName)) = context;
        var diagnostic = diagnosticModel.Diagnostic;

        var allTypes = refTypes.GroupBy(t => t.AssemblyName).ToDictionary(g => g.Key, g => g.ToList());
        // distinct-by fully qualified name to account for partial types
        allTypes[asmName] = sourceTypes.GroupBy(t => t.DisplayString).Select(g => g.First()).ToList(); // add the source-code collected types to the dictionary under the assembly name of the current compilation

        if (diagnostic != null)
            return diagnostic;

        var (method, attributes) = diagnosticModel.Model;

        var registrations = new List<ServiceRegistrationModel>();

        foreach (var attribute in attributes)
        {
            bool typesFound = false;

            if (!allTypes.TryGetValue(attribute.AssignableToType?.AssemblyName ?? asmName, out var asmTypes))
                continue; // TODO raise diagnostic

            var types = asmTypes
                .Where(t => !t.IsAbstract && !t.IsStatic && t.CanBeReferencedByName && t.TypeKind == TypeKind.Class);

            if (attribute.TypeNameFilter != null)
            {
                var regex = $"^({Regex.Escape(attribute.TypeNameFilter).Replace(@"\*", ".*").Replace(",", "|")})$";
                types = types.Where(t => Regex.IsMatch(t.DisplayString, regex));
            }

            foreach (var t in types)
            {
                var implementationType = t;

                TypeModel matchedType = null;
                if (attribute.AssignableToType != null && !IsAssignableTo(implementationType, attribute.AssignableToType, out matchedType))
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

    private static bool IsAssignableTo(TypeModel type, TypeModel assignableTo, out TypeModel matchedType)
    {
        if (type == assignableTo)
        {
            matchedType = type;
            return true;
        }

        if (assignableTo.IsGenericType && assignableTo.IsUnboundGenericType)
        {
            if (assignableTo.TypeKind == TypeKind.Interface)
            {
                var matchingInterface = type.AllInterfaces.FirstOrDefault(i => i.IsGenericType && i.OriginalDefinition == assignableTo);
                matchedType = matchingInterface;
                return matchingInterface != null;
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.OriginalDefinition == assignableTo)
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
                return type.AllInterfaces.Contains(assignableTo);
            }

            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType == assignableTo)
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

    private static IEnumerable<INamedTypeSymbol> GetTypesFromNamespace(INamespaceSymbol namespaceSymbol)
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
