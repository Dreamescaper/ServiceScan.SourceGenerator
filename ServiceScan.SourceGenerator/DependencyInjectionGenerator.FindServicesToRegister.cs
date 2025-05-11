using System.Collections.Generic;
using System.Linq;
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

        foreach (var attribute in attributes)
        {
            bool typesFound = false;

            foreach (var (implementationType, matchedType) in FilterTypes(compilation, attribute, containingType))
            {
                typesFound = true;

                if (attribute.CustomHandler != null)
                {
                    customHandlers.Add(new CustomHandlerModel(attribute.CustomHandler, implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                }
                else
                {
                    var serviceTypes = (attribute.AsSelf, attribute.AsImplementedInterfaces) switch
                    {
                        (true, true) => new[] { implementationType }.Concat(GetSuitableInterfaces(implementationType)),
                        (false, true) => GetSuitableInterfaces(implementationType),
                        (true, false) => [implementationType],
                        _ => [matchedType ?? implementationType]
                    };

                    foreach (var serviceType in serviceTypes)
                    {
                        if (implementationType.IsGenericType)
                        {
                            var implementationTypeName = implementationType.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            var serviceTypeName = serviceType.IsGenericType
                                ? serviceType.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                : serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                            var registration = new ServiceRegistrationModel(
                                attribute.Lifetime,
                                serviceTypeName,
                                implementationTypeName,
                                false,
                                true,
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
                                implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                                shouldResolve,
                                false,
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

        var implementationModel = new MethodImplementationModel(method, [.. registrations], [.. customHandlers]);
        return new(diagnostic, implementationModel);
    }

    private static IEnumerable<INamedTypeSymbol> GetSuitableInterfaces(ITypeSymbol type)
    {
        return type.AllInterfaces.Where(x => !ExcludedInterfaces.Contains(x.ToDisplayString()));
    }
}
