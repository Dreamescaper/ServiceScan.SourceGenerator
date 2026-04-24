using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;
using static ServiceScan.SourceGenerator.DiagnosticDescriptors;

namespace ServiceScan.SourceGenerator;

public partial class DependencyInjectionGenerator
{
    private static readonly string[] ExcludedInterfaces = [
        "System.IDisposable",
        "System.IAsyncDisposable"
    ];

    private static readonly Regex TypePlaceholderRegex = new(@"\bT\b", RegexOptions.Compiled);

    private static DiagnosticModel<MethodImplementationModel> FindServicesToRegister((DiagnosticModel<MethodWithAttributesModel>, Compilation) context)
    {
        var (diagnosticModel, compilation) = context;
        var diagnostic = diagnosticModel.Diagnostic;

        if (diagnostic != null)
            return diagnostic;

        var (method, attributes) = diagnosticModel.Model;

        var containingType = compilation.GetTypeByMetadataName(method.TypeMetadataName);
        var registrations = new List<ServiceRegistrationModel>();
        var customHandlers = new List<CustomHandlerModel>();
        var collectionItems = new List<string>();

        foreach (var attribute in attributes)
        {
            bool typesFound = false;

            foreach (var (implementationType, matchedTypes) in FilterTypes(compilation, attribute, containingType))
            {
                typesFound = true;

                if (method.ReturnTypeIsCollection)
                    AddCollectionItems(implementationType, matchedTypes, attribute, method, collectionItems);
                else if (attribute.CustomHandler != null)
                    AddCustomHandlerItems(implementationType, matchedTypes, attribute, customHandlers);
                else if (attribute.HandlerTemplate != null)
                    AddTemplateStatementItem(implementationType, attribute, customHandlers);
                else
                {
                    var implementationTypeName = implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var serviceTypes = (attribute.AsSelf, attribute.AsImplementedInterfaces) switch
                    {
                        (true, true) => [implementationType, .. GetSuitableInterfaces(implementationType)],
                        (false, true) => GetSuitableInterfaces(implementationType),
                        (true, false) => [implementationType],
                        _ => matchedTypes ?? [implementationType]
                    };

                    foreach (var serviceType in serviceTypes)
                    {
                        if (implementationType.IsGenericType)
                        {
                            var implementationTypeNameUnbound = implementationType.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            var serviceTypeName = serviceType.IsGenericType
                                ? serviceType.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                : serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                            var registration = new ServiceRegistrationModel(
                                attribute.Lifetime,
                                serviceTypeName,
                                implementationTypeNameUnbound,
                                ResolveImplementation: false,
                                IsOpenGeneric: true,
                                attribute.KeySelector,
                                attribute.KeySelectorType);

                            registrations.Add(registration);
                        }
                        else
                        {
                            var shouldResolve = attribute.AsSelf && attribute.AsImplementedInterfaces && !SymbolEqualityComparer.Default.Equals(implementationType, serviceType);
                            var registration = new ServiceRegistrationModel(
                                attribute.Lifetime,
                                serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                implementationTypeName,
                                shouldResolve,
                                IsOpenGeneric: false,
                                attribute.KeySelector,
                                attribute.KeySelectorType);

                            registrations.Add(registration);
                        }
                    }
                }
            }

            if (!typesFound)
                diagnostic ??= Diagnostic.Create(NoMatchingTypesFound, attribute.Location);
        }

        var implementationModel = new MethodImplementationModel(method, [.. registrations], [.. customHandlers], [.. collectionItems]);
        return new(diagnostic, implementationModel);
    }

    private static void AddCollectionItems(
        INamedTypeSymbol implementationType,
        IEnumerable<INamedTypeSymbol>? matchedTypes,
        AttributeModel attribute,
        MethodModel method,
        List<string> collectionItems)
    {
        var implementationTypeName = implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (attribute.CustomHandler == null && attribute.HandlerTemplate == null)
        {
            collectionItems.Add($"typeof({implementationTypeName})");
        }
        else if (attribute.HandlerTemplate != null)
        {
            collectionItems.Add(ExpandTemplate(attribute.HandlerTemplate, implementationTypeName));
        }
        else
        {
            var arguments = string.Join(", ", method.Parameters.Select(p => p.Name));

            if (attribute.CustomHandlerMethodTypeParametersCount > 1 && matchedTypes != null)
            {
                foreach (var matchedType in matchedTypes)
                {
                    var typeArguments = string.Join(", ", new[] { implementationTypeName }
                        .Concat(matchedType.TypeArguments.Select(a => a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));

                    if (attribute.CustomHandlerType == CustomHandlerType.Method)
                        collectionItems.Add(FormatCustomHandlerInvocation(attribute.CustomHandlerDeclaringTypeName, attribute.CustomHandler, typeArguments, arguments));
                    else
                        collectionItems.Add($"{implementationTypeName}.{attribute.CustomHandler}({arguments})");
                }
            }
            else
            {
                if (attribute.CustomHandlerType == CustomHandlerType.Method)
                    collectionItems.Add(FormatCustomHandlerInvocation(attribute.CustomHandlerDeclaringTypeName, attribute.CustomHandler, implementationTypeName, arguments));
                else
                    collectionItems.Add($"{implementationTypeName}.{attribute.CustomHandler}({arguments})");
            }
        }
    }

    private static void AddCustomHandlerItems(
        INamedTypeSymbol implementationType,
        IEnumerable<INamedTypeSymbol>? matchedTypes,
        AttributeModel attribute,
        List<CustomHandlerModel> customHandlers)
    {
        var implementationTypeName = implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (attribute.CustomHandlerMethodTypeParametersCount > 1 && matchedTypes != null)
        {
            foreach (var matchedType in matchedTypes)
            {
                EquatableArray<string> typeArguments =
                    [
                        implementationTypeName,
                        .. matchedType.TypeArguments.Select(a => a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    ];

                customHandlers.Add(new CustomHandlerModel(
                    attribute.CustomHandlerType.Value,
                    attribute.CustomHandler,
                    attribute.CustomHandlerType == CustomHandlerType.Method
                        ? attribute.CustomHandlerDeclaringTypeName
                        : implementationTypeName,
                    typeArguments));
            }
        }
        else
        {
            customHandlers.Add(new CustomHandlerModel(
                attribute.CustomHandlerType.Value,
                attribute.CustomHandler,
                attribute.CustomHandlerType == CustomHandlerType.Method
                    ? attribute.CustomHandlerDeclaringTypeName
                    : implementationTypeName,
                [implementationTypeName]));
        }
    }

    private static void AddTemplateStatementItem(
        INamedTypeSymbol implementationType,
        AttributeModel attribute,
        List<CustomHandlerModel> customHandlers)
    {
        var implementationTypeName = implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var statement = ExpandTemplate(attribute.HandlerTemplate!, implementationTypeName);
        if (!statement.EndsWith(";") && !statement.TrimEnd().EndsWith(";"))
            statement += ";";

        customHandlers.Add(new CustomHandlerModel(
            Model.CustomHandlerType.Template,
            statement,
            implementationTypeName,
            []));
    }

    private static string ExpandTemplate(string template, string typeName)
    {
        return TypePlaceholderRegex.Replace(template, typeName);
    }

    private static string FormatCustomHandlerInvocation(string? typeName, string handlerName, string typeArguments, string arguments)
    {
        var target = typeName is null ? "" : $"{typeName}.";
        return $"{target}{handlerName}<{typeArguments}>({arguments})";
    }

    private static IEnumerable<INamedTypeSymbol> GetSuitableInterfaces(ITypeSymbol type)
    {
        return type.AllInterfaces.Where(x => !ExcludedInterfaces.Contains(x.ToDisplayString()));
    }
}
