using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;
using static ServiceScan.SourceGenerator.DiagnosticDescriptors;

namespace ServiceScan.SourceGenerator;

public partial class DependencyInjectionGenerator
{
    private static DiagnosticModel<MethodWithAttributesModel>? ParseRegisterMethodModel(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IMethodSymbol method)
            return null;

        if (!method.IsPartialDefinition)
            return Diagnostic.Create(NotPartialDefinition, method.Locations[0]);

        var attributeData = context.Attributes.Select(a => AttributeModel.Create(a, method)).ToArray();
        var hasCustomHandlers = attributeData.Any(a => a.CustomHandler != null);

        for (var i = 0; i < context.Attributes.Length; i++)
        {
            var attribute = AttributeModel.Create(context.Attributes[i], method);
            attributeData[i] = attribute;

            if (!attribute.HasSearchCriteria)
                return Diagnostic.Create(MissingSearchCriteria, attribute.Location);

            if (hasCustomHandlers && attribute.CustomHandler == null)
                return Diagnostic.Create(CantMixRegularAndCustomHandlerRegistrations, attribute.Location);

            if (attribute.AssemblyOfTypeName != null && attribute.AssemblyNameFilter != null)
                return Diagnostic.Create(CantUseBothFromAssemblyOfAndAssemblyNameFilter, attribute.Location);

            if (attribute.KeySelector != null)
            {
                var keySelectorMethod = method.ContainingType.GetMembers().OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsStatic && m.Name == attribute.KeySelector);

                if (keySelectorMethod is not null)
                {
                    if (keySelectorMethod.ReturnsVoid)
                        return Diagnostic.Create(KeySelectorMethodHasIncorrectSignature, attribute.Location);

                    var validGenericKeySelector = keySelectorMethod.TypeArguments.Length == 1 && keySelectorMethod.Parameters.Length == 0;
                    var validNonGenericKeySelector = !keySelectorMethod.IsGenericMethod && keySelectorMethod.Parameters is [{ Type.Name: nameof(Type) }];

                    if (!validGenericKeySelector && !validNonGenericKeySelector)
                        return Diagnostic.Create(KeySelectorMethodHasIncorrectSignature, attribute.Location);
                }
            }

            if (attribute.CustomHandler != null)
            {
                var customHandlerMethod = method.ContainingType.GetMembers().OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.IsStatic && m.Name == attribute.CustomHandler);

                if (customHandlerMethod is null)
                    return Diagnostic.Create(CustomHandlerMethodNotFound, attribute.Location);

                if (!customHandlerMethod.IsGenericMethod)
                    return Diagnostic.Create(CustomHandlerMethodHasIncorrectSignature, attribute.Location);

                var typesMatch = Enumerable.SequenceEqual(
                    method.Parameters.Select(p => p.Type),
                    customHandlerMethod.Parameters.Select(p => p.Type),
                    SymbolEqualityComparer.Default);

                if (!typesMatch)
                    return Diagnostic.Create(CustomHandlerMethodHasIncorrectSignature, attribute.Location);

                // If CustomHandler has more than 1 type parameters, we try to resolve them from 
                // matched assignableTo type arguments.
                // e.g. ApplyConfiguration<T, TEntity>(ModelBuilder modelBuilder) where T : IEntityTypeConfiguration<TEntity>
                if (customHandlerMethod.TypeParameters.Length > 1
                    && customHandlerMethod.TypeParameters.Length != attribute.AssignableToTypeParametersCount + 1)
                {
                    return Diagnostic.Create(CustomHandlerMethodHasIncorrectSignature, attribute.Location);
                }
            }

            if (attributeData[i].HasErrors)
                return null;
        }

        if (!hasCustomHandlers)
        {
            var serviceCollectionType = context.SemanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");

            if (!method.ReturnsVoid && !SymbolEqualityComparer.Default.Equals(method.ReturnType, serviceCollectionType))
                return Diagnostic.Create(WrongReturnType, method.Locations[0]);

            if (method.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, serviceCollectionType))
                return Diagnostic.Create(WrongMethodParameters, method.Locations[0]);
        }
        else
        {
            if (!method.ReturnsVoid &&
                (method.Parameters.Length == 0 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.ReturnType)))
            {
                return Diagnostic.Create(WrongReturnTypeForCustomHandler, method.Locations[0]);
            }
        }

        var model = MethodModel.Create(method, context.TargetNode);
        return new MethodWithAttributesModel(model, [.. attributeData]);
    }
}
