﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ServiceScan.SourceGenerator.Tests;

public class DiagnosticTests
{
    private const string Services = """
        namespace GeneratorTests;
        
        public interface IService { }
        public class MyService : IService { }
        """;

    private readonly DependencyInjectionGenerator _generator = new();

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

    [Fact]
    public void AttributeAddedToNonPartialMethod()
    {
        var compilation = CreateCompilation(Services,
            """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                public static void AddServices(this IServiceCollection services) {}
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.NotPartialDefinition);
    }

    [Fact]
    public void AttributeAddedToMethodReturningWrongType()
    {
        var compilation = CreateCompilation(Services,
            """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                public static partial IService AddServices(this IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.WrongReturnType);
    }

    [Fact]
    public void AttributeAddedToMethodWithoutParameters()
    {
        var compilation = CreateCompilation(Services,
            """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                public static partial IServiceCollection AddServices();
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.WrongMethodParameters);
    }

    [Fact]
    public void AttributeAddedToMethodWithWrongParameter()
    {
        var compilation = CreateCompilation(Services,
            """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IService))]
                public static partial IServiceCollection AddServices(IService service);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.WrongMethodParameters);
    }

    [Fact]
    public void SearchCriteriaInTheAttributeProducesNoResults()
    {
        var compilation = CreateCompilation(Services,
            """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;

            public interface IHasNoImplementations { }
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IHasNoImplementations))]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.NoMatchingTypesFound);

        var expectedFile = """
            using Microsoft.Extensions.DependencyInjection;

            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial IServiceCollection AddServices(this IServiceCollection services)
                {
                    return services
                        ;
                }
            }
            """;
        Assert.Equal(expectedFile, results.GeneratedTrees[1].ToString());
    }

    [Fact]
    public void SearchCriteriaInTheAttributeIsMissing()
    {
        var compilation = CreateCompilation(Services,
            """
            using ServiceScan.SourceGenerator;
            using Microsoft.Extensions.DependencyInjection;
            
            namespace GeneratorTests;
                    
            public static partial class ServicesExtensions
            {
                [GenerateServiceRegistrations]
                public static partial IServiceCollection AddServices(this IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.MissingSearchCriteria);
    }

    [Fact]
    public void KeySelectorMethod_GenericButHasParameters()
    {
        var attribute = @"
            private static string GetName<T>(string name) => typeof(T).Name.Replace(""Service"", name);

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

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.KeySelectorMethodHasIncorrectSignature);
    }

    [Fact]
    public void KeySelectorMethod_NonGenericWithoutParameters()
    {
        var attribute = @"
            private static string GetName() => ""const"";

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

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.KeySelectorMethodHasIncorrectSignature);
    }

    [Fact]
    public void KeySelectorMethod_Void()
    {
        var attribute = @"
            private static void GetName(Type type)
            {
                type.Name.ToString();
            }

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

        Assert.Equal(results.Diagnostics.Single().Descriptor, DiagnosticDescriptors.KeySelectorMethodHasIncorrectSignature);
    }
}
