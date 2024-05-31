namespace DependencyInjection.SourceGenerator;

internal static class GenerateAttributeSource
{
    public static string Source => """
        #nullable enable

        using System;
        using System.Diagnostics;
        using Microsoft.Extensions.DependencyInjection;

        namespace DependencyInjection.SourceGenerator;                

        [Conditional("CODE_ANALYSIS")]
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        internal class GenerateServiceRegistrationsAttribute : Attribute
        {
            public Type? FromAssemblyOf { get; set; }
            public Type? AssignableTo { get; set; }
            public ServiceLifetime Lifetime { get; set; }
            public bool AsImplementedInterfaces { get; set; }
            public string? TypeNameFilter { get; set; }
        }
        """;

}