using Microsoft.Extensions.DependencyInjection;

namespace DependencyInjection.SourceGenerator.Playground;

public static partial class ServiceCollectionExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IService), TypeNameFilter = "123")]
    public static partial IServiceCollection AddServices(this IServiceCollection services);
}
