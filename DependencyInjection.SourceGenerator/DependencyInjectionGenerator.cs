using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DependencyInjection.SourceGenerator;

[Generator]
public partial class DependencyInjectionGenerator : IIncrementalGenerator
{
    //static MethodModel Previous;
    //static int Iteration = 0;

    private static readonly DiagnosticDescriptor NotPartialDefinition = new("DI001", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor WrongReturnType = new("DI002", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor WrongMethodParameters = new("DI003", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor NoMatchingTypesFound = new("DI004", "Error shouldn't happen", "Test", "DI", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context => context.AddSource("GenerateServiceRegistrationsAttribute.Generated.cs", SourceText.From(GenerateAttributeSource.Source, Encoding.UTF8)));

        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName("DependencyInjection.SourceGenerator.GenerateServiceRegistrationsAttribute",
                predicate: static (syntaxNode, ct) => syntaxNode is MethodDeclarationSyntax methodSyntax,
                transform: static (context, ct) =>
                {
                    if (context.TargetSymbol is not IMethodSymbol method)
                        return null;

                    if (!method.IsPartialDefinition)
                        return null;

                    var serviceCollectionType = context.SemanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
                    var attributeType = context.SemanticModel.Compilation.GetTypeByMetadataName("DependencyInjection.SourceGenerator.GenerateServiceRegistrationsAttribute");

                    if (serviceCollectionType is null)
                        return null;

                    if (!method.ReturnsVoid && !SymbolEqualityComparer.Default.Equals(method.ReturnType, serviceCollectionType))
                        return null;

                    if (method.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceCollectionType))
                        return null;

                    var attributeData = context.Attributes.Select(AttributeModel.Create);
                    var model = MethodModel.Create(method, attributeData);

                    //if (Previous != null && !model.Equals(Previous))
                    //    System.Diagnostics.Debugger.Launch();

                    //Previous = model;

                    return model;
                })
            .Where(method => method != null);

        var combinedProvider = methodProvider.Combine(context.CompilationProvider)
            .WithComparer(CombinedProviderComparer.Instance);

        // We require all matching type symbols, and create the generated files.
        context.RegisterImplementationSourceOutput(combinedProvider,
            static (context, src) =>
            {
                var (model, compilation) = src;

                //var sw = System.Diagnostics.Stopwatch.StartNew();

                var sb = new StringBuilder();
                var attributes = model.Attributes;

                foreach (var attribute in attributes)
                {
                    var assembly = compilation.GetTypeByMetadataName(attribute.AssemblyOfTypeName ?? model.TypeMetadataName).ContainingAssembly;

                    var assignableToType = attribute.AssignableToTypeName is null
                        ? null
                        : compilation.GetTypeByMetadataName(attribute.AssignableToTypeName);

                    var types = GetTypesFromAssembly(assembly)
                        .Where(t => !t.IsAbstract && !t.IsStatic && t.TypeKind == TypeKind.Class);

                    if (attribute.TypeNameFilter != null)
                    {
                        var regex = $"^{Regex.Escape(attribute.TypeNameFilter).Replace(@"\*", ".*")}$";
                        types = types.Where(t => Regex.IsMatch(t.ToDisplayString(), regex));
                    }

                    bool anyFound = false;

                    foreach (var t in types)
                    {
                        var implementationType = t;

                        INamedTypeSymbol matchedType = null;
                        if (assignableToType != null && !IsAssignableTo(implementationType, assignableToType, out matchedType))
                            continue;

                        anyFound = true;

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

                                sb.AppendLine($"            .Add{attribute.Lifetime}(typeof({serviceTypeName}), typeof({implementationTypeName}))");
                            }
                            else
                            {
                                sb.AppendLine($"            .Add{attribute.Lifetime}<{serviceType.ToDisplayString()}, {implementationType.ToDisplayString()}>()");
                            }
                        }
                    }

                    if (!anyFound)
                    {
                        //context.ReportDiagnostic(Diagnostic.Create(NoMatchingTypesFound, method.Locations[0]));
                        return;
                    }
                }

                var returnType = model.ReturnsVoid ? "void" : "IServiceCollection";


                var source = $$"""
                using Microsoft.Extensions.DependencyInjection;

                namespace {{model.Namespace}};

                {{model.TypeAccessModifier}} {{model.TypeStatic}} partial class {{model.TypeName}}
                {
                    {{model.MethodAccessModifier}} {{model.MethodStatic}} partial {{returnType}} {{model.MethodName}}({{(model.IsExtensionMethod ? "this" : "")}} IServiceCollection services)
                    {
                        {{(model.ReturnsVoid ? "" : "return ")}}services
                            {{sb.ToString().Trim()}};
                    }
                }
                """;

                //source = $$"""
                //// Iteration: {{Iteration}}
                //// Elapsed: {{sw.Elapsed.TotalMilliseconds}}ms

                //""" + source;
                //Iteration++;

                context.AddSource($"{model.TypeName}_{model.MethodName}.Generated.cs", SourceText.From(source, Encoding.UTF8));
            });
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
