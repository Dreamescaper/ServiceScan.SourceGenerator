using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace DependencyInjection.SourceGenerator.Tests;

public class Tests
{
    [Fact]
    public void Generate()
    {
        var compilation = CreateCompilation("""
            using DependencyInjection.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [Generate(AssignableTo = typeof(IService), Lifetime = ServiceLifetime.Scoped)]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
                    
            public interface IService { }
            public class MyService1 : IService { }
            public class MyService2 : IService { }
            """);

        var generator = new DependencyInjectionGenerator();
        var results = CSharpGeneratorDriver
            .Create(generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expectedResult = """
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial IServiceCollection AddServices(this IServiceCollection services)
                {
                    return services
                        .AddTransient<GeneratorTests.IService, GeneratorTests.MyService1>()
                        .AddTransient<GeneratorTests.IService, GeneratorTests.MyService2>();
                }
            }
            """;

        Assert.Equal(expectedResult, results.GeneratedTrees[1].ToString());
    }

    private static Compilation CreateCompilation(string source)
        => CSharpCompilation.Create("compilation",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(Binder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location) ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
