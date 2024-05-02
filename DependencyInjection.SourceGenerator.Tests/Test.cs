using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

// Tests
// Return types - void, IServiceCollection
// Access modifiers - public, private, internal
// Static / non-static
// Extension method / regular method
// Errors - other return type, non-partial, no arguments, wrong arguments
// Open-generics



namespace DependencyInjection.SourceGenerator
{
    public static partial class Test
    {
        [Generate(AssignableTo = typeof(IService<string>), Lifetime = ServiceLifetime.Scoped)]
        public static partial IServiceCollection AddServices(this IServiceCollection services);
    }

    public interface IService<T> { }

    public class MyService : IService<string> { }

    [Conditional("CODE_ANALYSIS")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class GenerateAttribute : Attribute
    {
        public Type FromAssemblyOf { get; set; }
        public Type AssignableTo { get; set; }
        public ServiceLifetime Lifetime { get; set; }
        public bool AsImplementedInterfaces { get; set; }
        public string TypeNameFilter { get; set; }
    }
}
