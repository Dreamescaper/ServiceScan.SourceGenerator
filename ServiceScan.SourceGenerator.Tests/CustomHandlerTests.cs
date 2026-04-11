using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace ServiceScan.SourceGenerator.Tests;

public class CustomHandlerTests
{
    private readonly DependencyInjectionGenerator _generator = new();

    [Test]
    public async Task CustomHandlerWithNoParameters()
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
                    HandleType<global::GeneratorTests.MyService1>();
                    HandleType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandlerWithParameters()
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
                    HandleType<global::GeneratorTests.MyFirstService>(value, number);
                    HandleType<global::GeneratorTests.MySecondService>(value, number);
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_NoTypesFound()
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
                    
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandlerExtensionMethod()
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
                public static partial global::GeneratorTests.IServices ProcessServices(this global::GeneratorTests.IServices services)
                {
                    HandleType<global::GeneratorTests.MyService1>(services);
                    HandleType<global::GeneratorTests.MyService2>(services);
                    return services;
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandlerWithParametersAndAttributeFilter()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AttributeFilter = typeof(ServiceAttribute), CustomHandler = nameof(HandleType))]
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
                    HandleType<global::GeneratorTests.MyFirstService>(services, number);
                    HandleType<global::GeneratorTests.MySecondService>(services, number);
                    return services;
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task AddMultipleCustomHandlerAttributesWithDifferentCustomHandler()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IFirstService), CustomHandler = nameof(HandleFirstType))]
                [GenerateServiceRegistrations(AssignableTo = typeof(ISecondService), CustomHandler = nameof(HandleSecondType))]
                public static partial void ProcessServices();

                private static void HandleFirstType<T>() => System.Console.WriteLine("First:" + typeof(T).Name);
                private static void HandleSecondType<T>() => System.Console.WriteLine("Second:" + typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IFirstService { }
            public interface ISecondService { }
            public class MyService1 : IFirstService { }
            public class MyService2 : ISecondService { }
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
                    HandleFirstType<global::GeneratorTests.MyService1>();
                    HandleSecondType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task AddMultipleCustomHandlerAttributesWithSameCustomHandler()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IFirstService), CustomHandler = nameof(HandleType))]
                [GenerateServiceRegistrations(AssignableTo = typeof(ISecondService), CustomHandler = nameof(HandleType))]
                public static partial void ProcessServices();

                private static void HandleType<T>() => System.Console.WriteLine(typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IFirstService { }
            public interface ISecondService { }
            public class MyService1 : IFirstService { }
            public class MyService2 : ISecondService { }
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
                    HandleType<global::GeneratorTests.MyService1>();
                    HandleType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ResolveCustomHandlerGenericArguments()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ModelBuilderExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IEntityTypeConfiguration<>), CustomHandler = nameof(ApplyConfiguration))]
                public static partial ModelBuilder ApplyEntityConfigurations(this ModelBuilder modelBuilder);

                private static void ApplyConfiguration<T, TEntity>(ModelBuilder modelBuilder)
                    where T : IEntityTypeConfiguration<TEntity>, new()
                    where TEntity : class
                {
                    modelBuilder.ApplyConfiguration(new T());
                }
            }
            """;

        var infra = """
            public interface IEntityTypeConfiguration<TEntity> where TEntity : class
            {
                void Configure(EntityTypeBuilder<TEntity> builder);
            }

            public class EntityTypeBuilder<TEntity> where TEntity : class;

            public class ModelBuilder
            {
                public ModelBuilder ApplyConfiguration<TEntity>(IEntityTypeConfiguration<TEntity> configuration) where TEntity : class
                {
                    return this;
                }
            }
            """;

        var configurations = """
            namespace GeneratorTests;
            
            public class EntityA;
            public class EntityB;

            public class EntityAConfiguration : IEntityTypeConfiguration<EntityA>
            {
                public void Configure(EntityTypeBuilder<EntityA> builder) { }
            }

            public class EntityBConfiguration : IEntityTypeConfiguration<EntityB>
            {
                public void Configure(EntityTypeBuilder<EntityB> builder) { }
            }
            """;

        var compilation = CreateCompilation(source, infra, configurations);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public static partial class ModelBuilderExtensions
            {
                public static partial global::ModelBuilder ApplyEntityConfigurations(this global::ModelBuilder modelBuilder)
                {
                    ApplyConfiguration<global::GeneratorTests.EntityAConfiguration, global::GeneratorTests.EntityA>(modelBuilder);
                    ApplyConfiguration<global::GeneratorTests.EntityBConfiguration, global::GeneratorTests.EntityB>(modelBuilder);
                    return modelBuilder;
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task UseInstanceCustomHandlerMethod()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService), CustomHandler = nameof(HandleType))]
                public partial void ProcessServices();

                private void HandleType<T>() => System.Console.WriteLine(typeof(T).Name);
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

            public partial class ServicesExtensions
            {
                public partial void ProcessServices()
                {
                    HandleType<global::GeneratorTests.MyService1>();
                    HandleType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task UseInstanceCustomHandlerMethod_FromParentType()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;

            public abstract class AbstractServiceProcessor
            {
                protected void HandleType<T>() => System.Console.WriteLine(typeof(T).Name);
            }
                    
            public partial class ServicesProcessor : AbstractServiceProcessor
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService), CustomHandler = nameof(HandleType))]
                public partial void ProcessServices();
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

            public partial class ServicesProcessor
            {
                public partial void ProcessServices()
                {
                    HandleType<global::GeneratorTests.MyService1>();
                    HandleType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task UseStaticMethodFromMatchedClassAsCustomHandler_WithoutParameters()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService), CustomHandler = "Handler"))]
                public partial void ProcessServices();
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }

            public class MyService1 : IService 
            {
                public static void Handler() { }
            }
            
            public class MyService2 : IService 
            {
                public static void Handler() { }
            }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public partial class ServicesExtensions
            {
                public partial void ProcessServices()
                {
                    global::GeneratorTests.MyService1.Handler();
                    global::GeneratorTests.MyService2.Handler();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task UseStaticMethodFromMatchedStaticClassAsCustomHandler_WithParameters()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(TypeNameFilter = "*StaticService", CustomHandler = "Handler"))]
                public partial void ProcessServices(IServiceCollection services);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public static class FirstStaticService 
            {
                public static void Handler(IServiceCollection services) { }
            }
            
            public static class SecondStaticService
            {
                public static void Handler(IServiceCollection services) { }
            }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public partial class ServicesExtensions
            {
                public partial void ProcessServices( global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    global::GeneratorTests.FirstStaticService.Handler(services);
                    global::GeneratorTests.SecondStaticService.Handler(services);
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task AddServicesWithDecorator()
    {
        var services = """
            namespace GeneratorTests;

            public interface ICommandHandler<T> { }
            public class CommandHandlerDecorator<T>(ICommandHandler<T> inner) : ICommandHandler<T>;

            public class SpecificHandler1 : ICommandHandler<string>;
            public class SpecificHandler2 : ICommandHandler<long>;
            """;

        var source = """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public static partial class ServiceCollectionExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(ICommandHandler<>), CustomHandler = nameof(AddDecoratedHandler))]
                public static partial IServiceCollection AddHandlers(this IServiceCollection services);

                private static void AddDecoratedHandler<THandler, TCommand>(this IServiceCollection services)
                    where THandler : class, ICommandHandler<TCommand>
                {
                    // Add handler itself to DI
                    services.AddScoped<THandler>();

                    // Register decorated handler as ICommandHandler
                    services.AddScoped<ICommandHandler<TCommand>>(s => new CommandHandlerDecorator<TCommand>(s.GetRequiredService<THandler>()));
                }
            }
            """;


        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = $$"""
            namespace GeneratorTests;

            public static partial class ServiceCollectionExtensions
            {
                public static partial global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddHandlers(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    AddDecoratedHandler<global::GeneratorTests.SpecificHandler1, string>(services);
                    AddDecoratedHandler<global::GeneratorTests.SpecificHandler2, long>(services);
                    return services;
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_FiltersByNewConstraint()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService), CustomHandler = nameof(HandleType))]
                public static partial void ProcessServices();

                private static void HandleType<T>() where T : IService, new() => System.Console.WriteLine(typeof(T).Name);
            }
            """;

        var services = """
            namespace GeneratorTests;

            public interface IService { }
            public class ServiceWithParameterlessConstructor : IService { }
            public class ServiceWithoutParameterlessConstructor : IService 
            { 
                public ServiceWithoutParameterlessConstructor(int value) { }
            }
            public class ServiceWithPrivateConstructor : IService
            {
                private ServiceWithPrivateConstructor() { }
            }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void ProcessServices()
                {
                    HandleType<global::GeneratorTests.ServiceWithParameterlessConstructor>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_FiltersByClassConstraint()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(TypeNameFilter = "*Service", CustomHandler = nameof(HandleType))]
                public static partial void ProcessServices();

                private static void HandleType<T>() where T : class => System.Console.WriteLine(typeof(T).Name);
            }
            """;

        var services = """
            namespace GeneratorTests;

            public class ClassService { }
            public struct StructService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void ProcessServices()
                {
                    HandleType<global::GeneratorTests.ClassService>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_FiltersByNestedTypeParameterConstraints()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServiceCollectionExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(ICommandHandler<>), CustomHandler = nameof(AddHandler))]
                public static partial void AddHandlers();

                private static void AddHandler<THandler, TCommand>()
                    where THandler : class, ICommandHandler<TCommand>
                    where TCommand : class, ICommand
                {
                }
            }
            """;

        var services = """
            namespace GeneratorTests;

            public interface ICommand { }
            public interface ICommandHandler<T> where T : ICommand { }
            
            public class ValidCommand : ICommand { }
            public class InvalidCommand { }
            
            public class ValidHandler : ICommandHandler<ValidCommand> { }
            public class InvalidHandler : ICommandHandler<InvalidCommand> { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServiceCollectionExtensions
            {
                public static partial void AddHandlers()
                {
                    AddHandler<global::GeneratorTests.ValidHandler, global::GeneratorTests.ValidCommand>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_FiltersByMultipleInterfacesWithDifferentTypeArguments()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServiceCollectionExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IHandler<>), CustomHandler = nameof(AddHandler))]
                public static partial void AddHandlers();

                private static void AddHandler<THandler, TArg>()
                    where THandler : class, IHandler<TArg>
                    where TArg : class
                {
                }
            }
            """;

        var services = """
            namespace GeneratorTests;

            public interface IHandler<T> { }
            
            public class Handler1 : IHandler<string> { }
            public class Handler2 : IHandler<object> { }
            public class Handler3 : IHandler<int> { }
            public class MultiHandler : IHandler<string>, IHandler<object> { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServiceCollectionExtensions
            {
                public static partial void AddHandlers()
                {
                    AddHandler<global::GeneratorTests.Handler1, string>();
                    AddHandler<global::GeneratorTests.Handler2, object>();
                    AddHandler<global::GeneratorTests.MultiHandler, string>();
                    AddHandler<global::GeneratorTests.MultiHandler, object>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_FiltersByValueTypeConstraint()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServiceCollectionExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IProcessor<>), CustomHandler = nameof(AddProcessor))]
                public static partial void AddProcessors();

                private static void AddProcessor<TProcessor, TValue>()
                    where TProcessor : class, IProcessor<TValue>
                    where TValue : struct
                {
                }
            }
            """;

        var services = """
            namespace GeneratorTests;

            public interface IProcessor<T> { }
            
            public class IntProcessor : IProcessor<int> { }
            public class StringProcessor : IProcessor<string> { }
            public class GuidProcessor : IProcessor<System.Guid> { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServiceCollectionExtensions
            {
                public static partial void AddProcessors()
                {
                    AddProcessor<global::GeneratorTests.IntProcessor, int>();
                    AddProcessor<global::GeneratorTests.GuidProcessor, global::System.Guid>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_CombinedConstraints()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;

            public interface IConfigurable { }
                    
            public static partial class ServiceCollectionExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IHandler<>), CustomHandler = nameof(AddHandler))]
                public static partial void AddHandlers();

                private static void AddHandler<THandler, TArg>()
                    where THandler : class, IHandler<TArg>, IConfigurable, new()
                    where TArg : class, new()
                {
                }
            }
            """;

        var services = """
            namespace GeneratorTests;

            public interface IHandler<T> { }
            
            public class Arg1 { }
            public class Arg2 { public Arg2(int x) { } }
            
            public class ValidHandler : IHandler<Arg1>, IConfigurable { }
            public class HandlerWithoutConfigurable : IHandler<Arg1> { }
            public class HandlerWithoutConstructor : IHandler<Arg1>, IConfigurable 
            { 
                public HandlerWithoutConstructor(int x) { }
            }
            public class HandlerWithNonConstructibleArg : IHandler<Arg2>, IConfigurable { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServiceCollectionExtensions
            {
                public static partial void AddHandlers()
                {
                    AddHandler<global::GeneratorTests.ValidHandler, global::GeneratorTests.Arg1>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task CustomHandler_HandlesRecursiveConstraints()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(TypeNameFilter = "*Smth*", CustomHandler = nameof(HandleType))]
                public static partial void ProcessServices();

                private static void HandleType<X, Y>() 
                    where X : ISmth<Y> 
                    where Y : ISmth<X>                   
                    => System.Console.WriteLine(typeof(X).Name);
            }
            """;

        var services = """
            namespace GeneratorTests;

            interface ISmth<T>;
            class SmthX: ISmth<SmthY>; 
            class SmthY: ISmth<SmthX>;
            class SmthString: ISmth<string>;
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void ProcessServices()
                {
                    HandleType<global::GeneratorTests.SmthX>();
                    HandleType<global::GeneratorTests.SmthY>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
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

    [Test]
    public async Task ScanForTypesAttribute_WithNoParameters()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), Handler = nameof(HandleType))]
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
                    HandleType<global::GeneratorTests.MyService1>();
                    HandleType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_WithParameters()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(TypeNameFilter = "*Service", Handler = nameof(HandleType))]
                public static partial void ProcessServices(string value);

                private static void HandleType<T>(string value) => System.Console.WriteLine(value + typeof(T).Name);
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
                public static partial void ProcessServices( string value)
                {
                    HandleType<global::GeneratorTests.MyFirstService>(value);
                    HandleType<global::GeneratorTests.MySecondService>(value);
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_MultipleAttributes()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IFirstService), Handler = nameof(HandleFirstType))]
                [ScanForTypes(AssignableTo = typeof(ISecondService), Handler = nameof(HandleSecondType))]
                public static partial void ProcessServices();

                private static void HandleFirstType<T>() => System.Console.WriteLine("First:" + typeof(T).Name);
                private static void HandleSecondType<T>() => System.Console.WriteLine("Second:" + typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IFirstService { }
            public interface ISecondService { }
            public class MyService1 : IFirstService { }
            public class MyService2 : ISecondService { }
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
                    HandleFirstType<global::GeneratorTests.MyService1>();
                    HandleSecondType<global::GeneratorTests.MyService2>();
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_MissingHandler_ReportsDiagnostic()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService))]
                public static partial void ProcessServices();
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService : IService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(DiagnosticDescriptors.MissingCustomHandlerOnGenerateServiceHandler).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task ScanForTypesAttribute_MissingSearchCriteria_ReportsDiagnostic()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(Handler = nameof(HandleType))]
                public static partial void ProcessServices();

                private static void HandleType<T>() { }
            }
            """;

        var compilation = CreateCompilation(source);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(DiagnosticDescriptors.MissingSearchCriteria).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task MixingGenerateServiceRegistrationsAndScanForTypes_ReportsDiagnostic()
    {
        var source = $$"""
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                [ScanForTypes(AssignableTo = typeof(IService), Handler = nameof(HandleType))]
                public static partial IServiceCollection ProcessServices(this IServiceCollection services);

                private static void HandleType<T>() { }
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService : IService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(results.Diagnostics).Contains(d => d.Descriptor == DiagnosticDescriptors.CantMixServiceRegistrationsAndServiceHandler);
    }

    [Test]
    public async Task ScanForTypesAttribute_ReturnsTypeArray_WithNoHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            using System;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService))]
                public static partial Type[] GetServiceTypes();
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

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::System.Type[] GetServiceTypes()
                {
                    return [
                        typeof(global::GeneratorTests.MyService1),
                        typeof(global::GeneratorTests.MyService2)
                    ];
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_ReturnsIEnumerableType_WithNoHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            using System;
            using System.Collections.Generic;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService))]
                public static partial IEnumerable<Type> GetServiceTypes();
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

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::System.Collections.Generic.IEnumerable<global::System.Type> GetServiceTypes()
                {
                    return [
                        typeof(global::GeneratorTests.MyService1),
                        typeof(global::GeneratorTests.MyService2)
                    ];
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_ReturnsResponseArray_WithHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), Handler = nameof(GetServiceInfo))]
                public static partial ServiceInfo[] GetServiceInfos();

                private static ServiceInfo GetServiceInfo<T>() => new ServiceInfo(typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { }
            public class MyService2 : IService { }

            public class ServiceInfo
            {
                public ServiceInfo(string name) { }
            }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::GeneratorTests.ServiceInfo[] GetServiceInfos()
                {
                    return [
                        GetServiceInfo<global::GeneratorTests.MyService1>(),
                        GetServiceInfo<global::GeneratorTests.MyService2>()
                    ];
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_ReturnsIEnumerableResponse_WithHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            using System.Collections.Generic;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), Handler = nameof(GetServiceInfo))]
                public static partial IEnumerable<ServiceInfo> GetServiceInfos();

                private static ServiceInfo GetServiceInfo<T>() => new ServiceInfo(typeof(T).Name);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { }
            public class MyService2 : IService { }

            public class ServiceInfo
            {
                public ServiceInfo(string name) { }
            }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::System.Collections.Generic.IEnumerable<global::GeneratorTests.ServiceInfo> GetServiceInfos()
                {
                    return [
                        GetServiceInfo<global::GeneratorTests.MyService1>(),
                        GetServiceInfo<global::GeneratorTests.MyService2>()
                    ];
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_ReturnsTypeArray_MultipleAttributes()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            using System;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IFirstService))]
                [ScanForTypes(AssignableTo = typeof(ISecondService))]
                public static partial Type[] GetServiceTypes();
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IFirstService { }
            public interface ISecondService { }
            public class MyService1 : IFirstService { }
            public class MyService2 : ISecondService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::System.Type[] GetServiceTypes()
                {
                    return [
                        typeof(global::GeneratorTests.MyService1),
                        typeof(global::GeneratorTests.MyService2)
                    ];
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_HandlerReturnTypeMismatch_ReportsDiagnostic()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), Handler = nameof(GetServiceName))]
                public static partial ServiceInfo[] GetServiceInfos();

                private static string GetServiceName<T>() => typeof(T).Name;
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService : IService { }

            public class ServiceInfo { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(DiagnosticDescriptors.WrongHandlerReturnTypeForCollectionReturn).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task ScanForTypesAttribute_NoHandlerNonTypeCollection_ReportsDiagnostic()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService))]
                public static partial string[] GetServiceNames();
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService : IService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(DiagnosticDescriptors.MissingCustomHandlerOnGenerateServiceHandler).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task ScanForTypesAttribute_HandlerTemplate_ReturnsCollection()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), HandlerTemplate = "new T(argument)")]
                public static partial IService[] GetServiceInstances(string argument);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { public MyService1(string x) { } }
            public class MyService2 : IService { public MyService2(string x) { } }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::GeneratorTests.IService[] GetServiceInstances( string argument)
                {
                    return [
                        new global::GeneratorTests.MyService1(argument),
                        new global::GeneratorTests.MyService2(argument)
                    ];
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_HandlerTemplate_VoidMethod()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), HandlerTemplate = "registry.Add(new T(argument))")]
                public static partial void RegisterServices(ServiceRegistry registry, string argument);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { public MyService1(string x) { } }
            public class MyService2 : IService { public MyService2(string x) { } }
            public class ServiceRegistry { public void Add(IService s) { } }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void RegisterServices( global::GeneratorTests.ServiceRegistry registry, string argument)
                {
                    registry.Add(new global::GeneratorTests.MyService1(argument));
                    registry.Add(new global::GeneratorTests.MyService2(argument));
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_HandlerTemplate_StatementWithSemicolon()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), HandlerTemplate = "registry.Add(new T(argument));")]
                public static partial void RegisterServices(ServiceRegistry registry, string argument);
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService1 : IService { public MyService1(string x) { } }
            public class ServiceRegistry { public void Add(IService s) { } }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        var expected = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void RegisterServices( global::GeneratorTests.ServiceRegistry registry, string argument)
                {
                    registry.Add(new global::GeneratorTests.MyService1(argument));
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_BothHandlerAndHandlerTemplate_ReportsDiagnostic()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IService), Handler = nameof(GetServiceInfo), HandlerTemplate = "new T()")]
                public static partial IService[] GetServiceInstances();

                private static IService GetServiceInfo<T>() where T : IService, new() => new T();
            }
            """;

        var services =
            """
            namespace GeneratorTests;

            public interface IService { }
            public class MyService : IService { }
            """;

        var compilation = CreateCompilation(source, services);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(DiagnosticDescriptors.CantUseBothHandlerAndHandlerTemplate).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }
}