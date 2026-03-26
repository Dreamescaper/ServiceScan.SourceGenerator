namespace ServiceScan.SourceGenerator.Model;

record MethodImplementationModel(
    MethodModel Method,
    EquatableArray<ServiceRegistrationModel> Registrations,
    EquatableArray<CustomHandlerModel?> CustomHandlers,
    EquatableArray<string> CollectionItems);
