using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DependencyInjection.SourceGenerator.Tests;

public class Tests
{
    private readonly DependencyInjectionGenerator _generator = new();

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Singleton)]
    public void AddServicesWithLifetime(ServiceLifetime lifetime)
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IService), Lifetime = ServiceLifetime.{lifetime})]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { }
            public class MyService2 : IService { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .Add{lifetime}<GeneratorTests.IService, GeneratorTests.MyService1>()
                .Add{lifetime}<GeneratorTests.IService, GeneratorTests.MyService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesFromAnotherAssembly()
    {
        var attribute = "[GenerateServiceRegistrations(FromAssemblyOf = typeof(External.IExternalService), AssignableTo = typeof(External.IExternalService))]";
        var compilation = CreateCompilation(Sources.MethodWithAttribute(attribute));

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<External.IExternalService, External.ExternalService1>()
                .AddTransient<External.IExternalService, External.ExternalService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServiceWithNonDirectInterface()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IService))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IService { }
            public abstract class AbstractService : IService { }
            public class MyService1 : AbstractService { }
            public class MyService2 : AbstractService { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.IService, GeneratorTests.MyService1>()
                .AddTransient<GeneratorTests.IService, GeneratorTests.MyService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServiceWithNonDirectAbstractClass()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(BaseType))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public abstract class BaseType { }
            public abstract class AbstractService : BaseType { }
            public class MyService1 : AbstractService { }
            public class MyService2 : AbstractService { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.BaseType, GeneratorTests.MyService1>()
                .AddTransient<GeneratorTests.BaseType, GeneratorTests.MyService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAssignableToOpenGenericInterface()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IService<>))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IService<T> { }
            public class MyIntService : IService<int> { }
            public class MyStringService : IService<string> { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.IService<int>, GeneratorTests.MyIntService>()
                .AddTransient<GeneratorTests.IService<string>, GeneratorTests.MyStringService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAssignableToAbstractClass()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(AbstractService))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public abstract class AbstractService { }
            public class MyService1 : AbstractService { }
            public class MyService2 : AbstractService { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.AbstractService, GeneratorTests.MyService1>()
                .AddTransient<GeneratorTests.AbstractService, GeneratorTests.MyService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAssignableToOpenGenericAbstractClass()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(AbstractService<>))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public abstract class AbstractService<T> { }
            public class MyIntService : AbstractService<int> { }
            public class MyStringService : AbstractService<string> { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.AbstractService<int>, GeneratorTests.MyIntService>()
                .AddTransient<GeneratorTests.AbstractService<string>, GeneratorTests.MyStringService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesWithTypeNameFilter()
    {

        var attribute = """[GenerateServiceRegistrations(TypeNameFilter = "*Service"))]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public class MyFirstService {}
            public class MySecondService {}
            public class ServiceWithNonMatchingName {}
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.MyFirstService, GeneratorTests.MyFirstService>()
                .AddTransient<GeneratorTests.MySecondService, GeneratorTests.MySecondService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesWithTypeNameFilterAsImplementedInterfaces()
    {
        var attribute = """[GenerateServiceRegistrations(TypeNameFilter = "*Service", AsImplementedInterfaces = true))]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IServiceA {}
            public interface IServiceB {}
            public interface IServiceC {}
            public class MyFirstService: IServiceA {}
            public class MySecondService: IServiceB, IServiceC {}
            public class InterfacelessService {}
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<GeneratorTests.IServiceA, GeneratorTests.MyFirstService>()
                .AddTransient<GeneratorTests.IServiceB, GeneratorTests.MySecondService>()
                .AddTransient<GeneratorTests.IServiceC, GeneratorTests.MySecondService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
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
