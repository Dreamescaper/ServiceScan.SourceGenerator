using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceScan.SourceGenerator.Tests;

public class AddServicesTests
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
                .Add{lifetime}<global::GeneratorTests.IService, global::GeneratorTests.MyService1>()
                .Add{lifetime}<global::GeneratorTests.IService, global::GeneratorTests.MyService2>();
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
                .AddTransient<global::External.IExternalService, global::External.ExternalService1>()
                .AddTransient<global::External.IExternalService, global::External.ExternalService2>();
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
                .AddTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService1>()
                .AddTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService2>();
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
                .AddTransient<global::GeneratorTests.BaseType, global::GeneratorTests.MyService1>()
                .AddTransient<global::GeneratorTests.BaseType, global::GeneratorTests.MyService2>();
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
                .AddTransient<global::GeneratorTests.IService<int>, global::GeneratorTests.MyIntService>()
                .AddTransient<global::GeneratorTests.IService<string>, global::GeneratorTests.MyStringService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAssignableToClosedGenericInterface()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IService<int>))]";

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
                .AddTransient<global::GeneratorTests.IService<int>, global::GeneratorTests.MyIntService>();
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
                .AddTransient<global::GeneratorTests.AbstractService, global::GeneratorTests.MyService1>()
                .AddTransient<global::GeneratorTests.AbstractService, global::GeneratorTests.MyService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAssignableToAbstractClassAsSelf()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(AbstractService), AsSelf = true)]";

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
                .AddTransient<global::GeneratorTests.MyService1, global::GeneratorTests.MyService1>()
                .AddTransient<global::GeneratorTests.MyService2, global::GeneratorTests.MyService2>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServiceAssignableToSelf()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(MyService))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public class MyService { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<global::GeneratorTests.MyService, global::GeneratorTests.MyService>();
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
                .AddTransient<global::GeneratorTests.AbstractService<int>, global::GeneratorTests.MyIntService>()
                .AddTransient<global::GeneratorTests.AbstractService<string>, global::GeneratorTests.MyStringService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddGenericServicesImplementingGenericInterfaceAsOpenGenerics()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IGenericService<>))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IGenericService<T> { }
            public class MyService1<T> : IGenericService<T> { }
            public class MyService2<T> : IGenericService<T> { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient(typeof(global::GeneratorTests.IGenericService<>), typeof(global::GeneratorTests.MyService1<>))
                .AddTransient(typeof(global::GeneratorTests.IGenericService<>), typeof(global::GeneratorTests.MyService2<>));
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddGenericServicesImplementingNonGenericInterfaceAsOpenGenerics()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IService))]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1<T> : IService { }
            public class MyService2<T> : IService { }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient(typeof(global::GeneratorTests.IService), typeof(global::GeneratorTests.MyService1<>))
                .AddTransient(typeof(global::GeneratorTests.IService), typeof(global::GeneratorTests.MyService2<>));
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
                .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>()
                .AddTransient<global::GeneratorTests.MySecondService, global::GeneratorTests.MySecondService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAttributeFilterFilter()
    {
        var attribute = """[GenerateServiceRegistrations(AttributeFilter = typeof(ServiceAttribute))]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
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
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>()
                .AddTransient<global::GeneratorTests.MySecondService, global::GeneratorTests.MySecondService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesAttributeFilterFilterAndTypeNameFilter()
    {
        var attribute = """[GenerateServiceRegistrations(AttributeFilter = typeof(ServiceAttribute), TypeNameFilter = "*Service")]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            using System;

            namespace GeneratorTests;

            [AttributeUsage(AttributeTargets.Class)]
            public sealed class ServiceAttribute : Attribute;

            [Service]
            public class MyFirstService {}
            
            public class MySecondServiceWithoutAttribute {}
            
            public class ServiceWithNonMatchingName {}
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesWithTypeNameFilter_MultipleGroups()
    {

        var attribute = """[GenerateServiceRegistrations(TypeNameFilter = "*First*,*Second*"))]""";

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
                .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>()
                .AddTransient<global::GeneratorTests.MySecondService, global::GeneratorTests.MySecondService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServices_ExcludeByTypeName()
    {
        var attribute = """[GenerateServiceRegistrations(TypeNameFilter = "*Service", ExcludeByTypeName = "*Second*")]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
        namespace GeneratorTests;

        public class MyFirstService {}
        public class MySecondService {}
        public class ThirdService {}
        """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
        return services
            .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>()
            .AddTransient<global::GeneratorTests.ThirdService, global::GeneratorTests.ThirdService>();
        """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServices_ExcludeByAttribute()
    {
        var attribute = """[GenerateServiceRegistrations(TypeNameFilter = "*Service", ExcludeByAttribute = typeof(ExcludeAttribute))]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
        using System;

        namespace GeneratorTests;

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class ExcludeAttribute : Attribute;

        public class MyFirstService {}
        
        [Exclude]
        public class MySecondService {}
        
        public class ThirdService {}
        """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
        return services
            .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>()
            .AddTransient<global::GeneratorTests.ThirdService, global::GeneratorTests.ThirdService>();
        """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServices_ExcludeByTypeNameAndAttribute()
    {
        var attribute = """[GenerateServiceRegistrations(TypeNameFilter = "*Service", ExcludeByTypeName = "*Third*", ExcludeByAttribute = typeof(ExcludeAttribute))]""";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
        using System;

        namespace GeneratorTests;

        [AttributeUsage(AttributeTargets.Class)]
        public sealed class ExcludeAttribute : Attribute;

        public class MyFirstService {}
        
        [Exclude]
        public class MySecondService {}
        
        public class ThirdService {}
        
        public class FourthService {}
        """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
        return services
            .AddTransient<global::GeneratorTests.MyFirstService, global::GeneratorTests.MyFirstService>()
            .AddTransient<global::GeneratorTests.FourthService, global::GeneratorTests.FourthService>();
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
                .AddTransient<global::GeneratorTests.IServiceA, global::GeneratorTests.MyFirstService>()
                .AddTransient<global::GeneratorTests.IServiceB, global::GeneratorTests.MySecondService>()
                .AddTransient<global::GeneratorTests.IServiceC, global::GeneratorTests.MySecondService>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddServicesBothAsSelfAndAsImplementedInterfaces()
    {
        var attribute = """
            [GenerateServiceRegistrations(
                TypeNameFilter = "*Service", 
                AsImplementedInterfaces = true, 
                AsSelf = true, 
                Lifetime = ServiceLifetime.Singleton))]
            """;

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IServiceA {}
            public interface IServiceB {}
            public class MyService: IServiceA, IServiceB {}
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddSingleton<global::GeneratorTests.MyService, global::GeneratorTests.MyService>()
                .AddSingleton<global::GeneratorTests.IServiceA>(s => s.GetRequiredService<global::GeneratorTests.MyService>())
                .AddSingleton<global::GeneratorTests.IServiceB>(s => s.GetRequiredService<global::GeneratorTests.MyService>());
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddNestedTypes()
    {
        var attribute = "[GenerateServiceRegistrations(AssignableTo = typeof(IService))]";
        var compilation = CreateCompilation(Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IService { }
            
            public class ParentType1
            {
                public class MyService1 : IService { }
                public class MyService2 : IService { }
            }
            
            public class ParentType2
            {
                public class MyService1 : IService { }
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddTransient<global::GeneratorTests.IService, global::GeneratorTests.ParentType1.MyService1>()
                .AddTransient<global::GeneratorTests.IService, global::GeneratorTests.ParentType1.MyService2>()
                .AddTransient<global::GeneratorTests.IService, global::GeneratorTests.ParentType2.MyService1>();
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddAsKeyedServices_GenericMethod()
    {
        var attribute = @"
            private static string GetName<T>() => typeof(T).Name.Replace(""Service"", """");

            [GenerateServiceRegistrations(AssignableTo = typeof(IService), KeySelector = nameof(GetName))]";

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
                .AddKeyedTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService1>(GetName<global::GeneratorTests.MyService1>())
                .AddKeyedTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService2>(GetName<global::GeneratorTests.MyService2>());
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddAsKeyedServices_MethodWithTypeParameter()
    {
        var attribute = @"
            private static string GetName(Type type) => type.Name.Replace(""Service"", """");

            [GenerateServiceRegistrations(AssignableTo = typeof(IService), KeySelector = nameof(GetName))]";

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
                .AddKeyedTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService1>(GetName(typeof(global::GeneratorTests.MyService1)))
                .AddKeyedTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService2>(GetName(typeof(global::GeneratorTests.MyService2)));
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void AddAsKeyedServices_ConstantFieldInType()
    {
        var attribute = @"[GenerateServiceRegistrations(AssignableTo = typeof(IService), KeySelector = ""Key"")]";

        var compilation = CreateCompilation(
            Sources.MethodWithAttribute(attribute),
            """
            namespace GeneratorTests;

            public interface IService { }

            public class MyService1 : IService 
            {
                public const string Key = "MSR1";
            }

            public class MyService2 : IService 
            {
                public const string Key = "MSR2";            
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var registrations = $"""
            return services
                .AddKeyedTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService1>(global::GeneratorTests.MyService1.Key)
                .AddKeyedTransient<global::GeneratorTests.IService, global::GeneratorTests.MyService2>(global::GeneratorTests.MyService2.Key);
            """;
        Assert.Equal(Sources.GetMethodImplementation(registrations), results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void DontGenerateAnythingIfTypeIsInvalid()
    {
        var attribute = $"[GenerateServiceRegistrations(AssignableTo = typeof(IWrongService))]";

        var compilation = CreateCompilation(Sources.MethodWithAttribute(attribute));

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        // One file for generated attribute itself.
        Assert.Single(results.GeneratedTrees);
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
