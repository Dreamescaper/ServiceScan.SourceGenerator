using Microsoft.Extensions.DependencyInjection;

namespace ServiceScan.SourceGenerator.Playground;

public static partial class ServiceCollectionExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IService), KeySelector = "Key", TypeNameFilter = "*Ser*")]
    public static partial IServiceCollection AddServices(this IServiceCollection services);
}
