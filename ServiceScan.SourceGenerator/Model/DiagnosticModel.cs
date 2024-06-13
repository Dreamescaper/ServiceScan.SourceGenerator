using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Model;

record DiagnosticModel<T>(Diagnostic? Diagnostic, T? Model)
{
    public static implicit operator DiagnosticModel<T>(T model) => new(null, model);
    public static implicit operator DiagnosticModel<T>(Diagnostic diagnostic) => new(diagnostic, default);
}
