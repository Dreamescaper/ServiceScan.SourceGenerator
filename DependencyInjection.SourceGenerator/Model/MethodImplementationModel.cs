namespace DependencyInjection.SourceGenerator.Model;

record MethodImplementationModel(
    MethodModel Method,
    EquatableArray<ServiceRegistrationModel> Registrations);
