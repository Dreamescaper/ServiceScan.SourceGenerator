using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DependencyInjection.SourceGenerator.Tests;

public class GeneratedMethodTests
{
    private readonly DependencyInjectionGenerator _generator = new();

    private const string Services = """
        namespace GeneratorTests;
        
        public interface IService { }
        public class MyService : IService { }
        """;

    [Theory]
    [InlineData("public", "public")]
    [InlineData("public", "private")]
    [InlineData("internal", "private")]
    [InlineData("internal", "public")]
    public void StaticExtensionMethodReturningServices(string classAccessModifier, string methodAccessModifier)
    {
        var compilation = CreateCompilation(Services,
            $$"""
            using DependencyInjection.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            {{classAccessModifier}} static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                {{methodAccessModifier}} static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            {{classAccessModifier}} static partial class ServicesExtensions
            {
                {{methodAccessModifier}} static partial IServiceCollection AddServices(this IServiceCollection services)
                {
                    return services
                        .AddTransient<GeneratorTests.IService, GeneratorTests.MyService>();
                }
            }
            """;

        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void StaticExtensionVoidMethod()
    {
        var compilation = CreateCompilation(Services,
            """
            using DependencyInjection.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                public static partial void AddServices(this IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void AddServices(this IServiceCollection services)
                {
                    services
                        .AddTransient<GeneratorTests.IService, GeneratorTests.MyService>();
                }
            }
            """;

        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void StaticMethodReturningServices()
    {
        var compilation = CreateCompilation(Services,
            """
            using DependencyInjection.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                public static partial IServiceCollection AddServices(IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial IServiceCollection AddServices( IServiceCollection services)
                {
                    return services
                        .AddTransient<GeneratorTests.IService, GeneratorTests.MyService>();
                }
            }
            """;

        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void InstanceVoidMethod()
    {
        var compilation = CreateCompilation(Services,
            """
            using DependencyInjection.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public partial class ServiceType
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                private partial void AddServices(IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public  partial class ServiceType
            {
                private  partial void AddServices( IServiceCollection services)
                {
                    services
                        .AddTransient<GeneratorTests.IService, GeneratorTests.MyService>();
                }
            }
            """;

        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    private static Compilation CreateCompilation(params string[] source)
    {
        var path = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeAssemblyPath = Path.Combine(path, "System.Runtime.dll");

        var runtimeReference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        return CSharpCompilation.Create("compilation",
                source.Select(s => CSharpSyntaxTree.ParseText(s)),
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(runtimeAssemblyPath),
                    MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(External.IExternalService).Assembly.Location),
                ],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
