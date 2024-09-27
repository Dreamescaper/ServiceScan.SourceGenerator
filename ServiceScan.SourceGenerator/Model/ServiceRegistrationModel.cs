namespace ServiceScan.SourceGenerator.Model;

record ServiceRegistrationModel(
    string Lifetime,
    string ServiceTypeName,
    string ImplementationTypeName,
    bool ResolveImplementation,
    bool IsOpenGeneric,
    string? KeySelectorMethodName,
    bool? KeySelectorMethodGeneric);
