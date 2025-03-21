using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceScan.SourceGenerator.Tests;

public class CustomHandlerTests
{
    private readonly DependencyInjectionGenerator _generator = new();

    [Fact]
    public void CustomHandlerWithNoParameters()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService), CustomHandler = nameof(HandleType))]
                public static partial void ProcessServices();

                private static void HandleType<T>() => System.Console.WriteLine(typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { }
            public class MyService2 : IService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void ProcessServices()
                {
                    HandleType<GeneratorTests.MyService1>();
                    HandleType<GeneratorTests.MyService2>();
                }
            }
            """;
        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void CustomHandlerWithParameters()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(TypeNameFilter = "*Service", CustomHandler = nameof(HandleType))]
                public static partial void ProcessServices(string value, decimal number);

                private static void HandleType<T>(string value, decimal number) => System.Console.WriteLine(value + number.ToString() + typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;
            
            public class MyFirstService {}
            public class MySecondService {}
            public class ServiceWithNonMatchingName {}
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void ProcessServices( string value, decimal number)
                {
                    HandleType<GeneratorTests.MyFirstService>(value, number);
                    HandleType<GeneratorTests.MySecondService>(value, number);
                }
            }
            """;
        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void CustomHandlerExtensionMethod()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService), CustomHandler = nameof(HandleType))]
                public static partial IServices ProcessServices(this IServices services);

                private static void HandleType<T>(IServices services) where T:IService, new() => services.Add(new T());
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IServices
            {
                void Add(IService service);
            }

            public interface IService { }
            public class MyService1 : IService { }
            public class MyService2 : IService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial GeneratorTests.IServices ProcessServices(this GeneratorTests.IServices services)
                {
                    HandleType<GeneratorTests.MyService1>(services);
                    HandleType<GeneratorTests.MyService2>(services);
                    return services;
                }
            }
            """;
        Assert.Equal(expected, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void CustomHandlerWithParametersAndAttributeFilter()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(WithAttribute = typeof(ServiceAttribute), CustomHandler = nameof(HandleType))]
                public static partial IServiceCollection ProcessServices(this IServiceCollection services, decimal number);

                private static void HandleType<T>(IServiceCollection services, decimal number) => System.Console.WriteLine(number.ToString() + typeof(T).Name);
            }
            """;

        var services =
            """
            using System;

            namespace GeneratorTests;
            
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class ServiceAttribute : Attribute;
            
            [Service]
            public class MyFirstService {}
            
            [Service]
            public class MySecondService {}
            
            public class ServiceWithoutAttribute {}
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial IServiceCollection ProcessServices(this IServiceCollection services, decimal number)
                {
                    HandleType<GeneratorTests.MyFirstService>(services, number);
                    HandleType<GeneratorTests.MySecondService>(services, number);
                    return services;
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
