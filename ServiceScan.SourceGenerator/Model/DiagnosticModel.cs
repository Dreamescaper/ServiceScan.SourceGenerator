using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Model;

record DiagnosticModel<T>
{
    public T? Model { get; init; }
    public Diagnostic? Diagnostic { get; init; }

    public static implicit operator DiagnosticModel<T>(T model) => new() { Model = model };

    public static implicit operator DiagnosticModel<T>(Diagnostic diagnostic) => new() { Diagnostic = diagnostic };
}
