using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

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

    [Test]
    public async Task AttributeAddedToNonPartialMethod()
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

        await Assert.That(DiagnosticDescriptors.NotPartialDefinition).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task AttributeAddedToMethodReturningWrongType()
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

        await Assert.That(DiagnosticDescriptors.WrongReturnType).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task AttributeAddedToMethodWithoutParameters()
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

        await Assert.That(DiagnosticDescriptors.WrongMethodParameters).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task AttributeAddedToMethodWithWrongParameter()
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

        await Assert.That(DiagnosticDescriptors.WrongMethodParameters).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task SearchCriteriaInTheAttributeProducesNoResults_ReturnsIServiceCollection()
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

        await Assert.That(DiagnosticDescriptors.NoMatchingTypesFound).IsEqualTo(results.Diagnostics.Single().Descriptor);

        var expectedFile = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddServices(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    return services;
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expectedFile);
    }

    [Test]
    public async Task SearchCriteriaInTheAttributeProducesNoResults_ReturnsVoid()
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
                public static partial void AddServices(this IServiceCollection services);
            }
            """);

        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(DiagnosticDescriptors.NoMatchingTypesFound).IsEqualTo(results.Diagnostics.Single().Descriptor);

        var expectedFile = """
            namespace GeneratorTests;

            public static partial class ServicesExtensions
            {
                public static partial void AddServices(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    
                }
            }
            """;
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expectedFile);
    }

    [Test]
    public async Task SearchCriteriaInTheAttributeIsMissing()
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

        await Assert.That(DiagnosticDescriptors.MissingSearchCriteria).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task KeySelectorMethod_GenericButHasParameters()
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

        await Assert.That(DiagnosticDescriptors.KeySelectorMethodHasIncorrectSignature).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task KeySelectorMethod_NonGenericWithoutParameters()
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

        await Assert.That(DiagnosticDescriptors.KeySelectorMethodHasIncorrectSignature).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }

    [Test]
    public async Task KeySelectorMethod_Void()
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

        await Assert.That(DiagnosticDescriptors.KeySelectorMethodHasIncorrectSignature).IsEqualTo(results.Diagnostics.Single().Descriptor);
    }
}