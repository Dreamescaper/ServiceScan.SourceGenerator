using Microsoft.Extensions.DependencyInjection;

namespace DependencyInjection.SourceGenerator.Playground;

public static partial class ServiceCollectionExtensions
{
    [Generate(AssignableTo = typeof(IService))]
    public static partial IServiceCollection AddServices(this IServiceCollection services);
}
