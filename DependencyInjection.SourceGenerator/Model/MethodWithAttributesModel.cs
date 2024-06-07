namespace DependencyInjection.SourceGenerator.Model;

record MethodWithAttributesModel(MethodModel Method, EquatableArray<AttributeModel> Attributes);
