using Microsoft.CodeAnalysis;
using ServiceScan.SourceGenerator.Model;
using static ServiceScan.SourceGenerator.DiagnosticDescriptors;

namespace ServiceScan.SourceGenerator;

public partial class DependencyInjectionGenerator
{
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
}
