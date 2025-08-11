namespace ServiceScan.SourceGenerator.Model;

record ServiceRegistrationModel(
    string Lifetime,
    string ServiceTypeName,
    string ImplementationTypeName,
    bool ResolveImplementation,
    bool IsOpenGeneric,
    string? KeySelector,
    KeySelectorType? KeySelectorType);

record CustomHandlerModel(
    CustomHandlerType CustomHandlerType,
    string HandlerMethodName,
    string TypeName,
    EquatableArray<string> TypeArguments);
