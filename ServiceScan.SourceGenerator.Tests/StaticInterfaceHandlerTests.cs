using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceScan.SourceGenerator.Tests;

public class StaticInterfaceHandlerTests
{
    private readonly DependencyInjectionGenerator _generator = new();

    [Test]
    public async Task ScanForTypesAttribute_WithInheritedStaticAbstractInterfaceHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IEndpoint), Handler = nameof(IEndpoint.MapEndpoint))]
                public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);
            }

            public interface IBaseEndpoint
            {
                static abstract void MapEndpoint(IEndpointRouteBuilder endpoints);
            }

            public interface IEndpoint : IBaseEndpoint { }
            public interface IEndpointRouteBuilder { }

            public class Endpoint : IEndpoint
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial global::GeneratorTests.IEndpointRouteBuilder MapEndpoints(this global::GeneratorTests.IEndpointRouteBuilder endpoints)
                {
                    global::GeneratorTests.Endpoint.MapEndpoint(endpoints);
                    return endpoints;
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_WithStaticVirtualInterfaceHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IEndpoint), Handler = nameof(IEndpoint.MapEndpoint))]
                public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);
            }

            public interface IEndpoint
            {
                static virtual void MapEndpoint(IEndpointRouteBuilder endpoints) { }
            }

            public interface IEndpointRouteBuilder { }

            public class Endpoint : IEndpoint
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial global::GeneratorTests.IEndpointRouteBuilder MapEndpoints(this global::GeneratorTests.IEndpointRouteBuilder endpoints)
                {
                    global::GeneratorTests.Endpoint.MapEndpoint(endpoints);
                    return endpoints;
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_WithAliasedStaticAbstractInterfaceHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;
            using EndpointContract = GeneratorTests.IEndpoint;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IEndpoint), Handler = nameof(EndpointContract.MapEndpoint))]
                public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);
            }

            public interface IEndpoint
            {
                static abstract void MapEndpoint(IEndpointRouteBuilder endpoints);
            }

            public interface IEndpointRouteBuilder { }

            public class Endpoint : IEndpoint
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial global::GeneratorTests.IEndpointRouteBuilder MapEndpoints(this global::GeneratorTests.IEndpointRouteBuilder endpoints)
                {
                    global::GeneratorTests.Endpoint.MapEndpoint(endpoints);
                    return endpoints;
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_WithStaticAbstractInterfaceHandlerFromReferencedAssembly()
    {
        var source = """
            using External;
            using ServiceScan.SourceGenerator;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IExternalEndpoint), Handler = nameof(IExternalEndpoint.MapEndpoint))]
                public static partial IExternalEndpointRouteBuilder MapEndpoints(this IExternalEndpointRouteBuilder endpoints);
            }

            public class Endpoint : IExternalEndpoint
            {
                public static void MapEndpoint(IExternalEndpointRouteBuilder endpoints) { }
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial global::External.IExternalEndpointRouteBuilder MapEndpoints(this global::External.IExternalEndpointRouteBuilder endpoints)
                {
                    global::GeneratorTests.Endpoint.MapEndpoint(endpoints);
                    return endpoints;
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_WithStaticAbstractHandlerOnClosedGenericInterface()
    {
        var source = """
            using ServiceScan.SourceGenerator;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IEndpoint<>), Handler = nameof(IEndpoint<string>.MapEndpoint))]
                public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);
            }

            public interface IEndpoint<T>
            {
                static abstract void MapEndpoint(IEndpointRouteBuilder endpoints);
            }

            public interface IEndpointRouteBuilder { }

            public class StringEndpoint : IEndpoint<string>
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial global::GeneratorTests.IEndpointRouteBuilder MapEndpoints(this global::GeneratorTests.IEndpointRouteBuilder endpoints)
                {
                    global::GeneratorTests.StringEndpoint.MapEndpoint(endpoints);
                    return endpoints;
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    [Test]
    public async Task ScanForTypesAttribute_ReturnsCollection_WithStaticAbstractInterfaceHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [ScanForTypes(AssignableTo = typeof(IEndpoint), Handler = nameof(IEndpoint.GetRoute))]
                public static partial string[] GetRoutes();
            }

            public interface IEndpoint
            {
                static abstract string GetRoute();
            }

            public class Endpoint : IEndpoint
            {
                public static string GetRoute() => "/endpoint";
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial string[] GetRoutes()
                {
                    return [
                        global::GeneratorTests.Endpoint.GetRoute()
                    ];
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    [Test]
    public async Task GenerateServiceRegistrationsAttribute_WithStaticAbstractInterfaceCustomHandler()
    {
        var source = """
            using ServiceScan.SourceGenerator;

            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                [GenerateServiceRegistrations(AssignableTo = typeof(IEndpoint), CustomHandler = nameof(IEndpoint.MapEndpoint))]
                public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);
            }

            public interface IEndpoint
            {
                static abstract void MapEndpoint(IEndpointRouteBuilder endpoints);
            }

            public interface IEndpointRouteBuilder { }

            public class Endpoint : IEndpoint
            {
                public static void MapEndpoint(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var expected = """
            namespace GeneratorTests;

            public static partial class EndpointBuilderExtensions
            {
                public static partial global::GeneratorTests.IEndpointRouteBuilder MapEndpoints(this global::GeneratorTests.IEndpointRouteBuilder endpoints)
                {
                    global::GeneratorTests.Endpoint.MapEndpoint(endpoints);
                    return endpoints;
                }
            }
            """;

        await AssertGeneratedSource(source, expected);
    }

    private async Task AssertGeneratedSource(string source, string expected)
    {
        var compilation = CreateCompilation(source);
        var results = CSharpGeneratorDriver
            .Create(_generator)
            .RunGenerators(compilation)
            .GetRunResult();

        await Assert.That(results.Diagnostics).IsEmpty();
        await Assert.That(results.GeneratedTrees[2].ToString()).IsEqualTo(expected);
    }

    private static Compilation CreateCompilation(params string[] source)
    {
        var path = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeAssemblyPath = Path.Combine(path, "System.Runtime.dll");

        return CSharpCompilation.Create(
            "compilation",
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
