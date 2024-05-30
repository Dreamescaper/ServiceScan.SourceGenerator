namespace DependencyInjection.SourceGenerator.Tests;

public static class Sources
{
    public const string Services = """

        """;

    public static string MethodWithAttribute(string attribute)
    {
        attribute = attribute.Replace("\n", "\n    ");

        return $$"""
            using DependencyInjection.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                {{attribute}}
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """;
    }

    public static string GetMethodImplementation(string services)
    {
        services = services.Replace("\n", "\n        ");

        return $$"""
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial IServiceCollection AddServices(this IServiceCollection services)
                {
                    {{services}}
                }
            }
            """;
    }
}
