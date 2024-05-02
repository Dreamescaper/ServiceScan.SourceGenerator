using System.Collections;
using System.Collections.Generic;
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
        [Generate(AssignableTo = typeof(IService), Lifetime = ServiceLifetime.Scoped)]
        [Generate(FromAssemblyOf = typeof(IEnumerable), AssignableTo = typeof(IEnumerable), Lifetime = ServiceLifetime.Singleton)]
        [Generate(FromAssemblyOf = typeof(List<>), AssignableTo = typeof(IEnumerable), Lifetime = ServiceLifetime.Transient)]
        public static partial IServiceCollection AddServices(this IServiceCollection services);
    }

    public interface IService { }

    public class MyService1 : IService { }
    public class MyService2 : IService { }
    public class MyService3 : IService { }
}
