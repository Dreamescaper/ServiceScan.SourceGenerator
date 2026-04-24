namespace External;

public interface IExternalService;
public class ExternalService1 : IExternalService { }
public class ExternalService2 : IExternalService { }
public static class ExternalHandlers
{
    public static string GetServiceName<T>() => typeof(T).Name;

    public static void Register<THandler, TRequest>(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
}

// Shouldn't be added as type is not accessible from other assembly
internal class InternalExternalService2 : IExternalService { }
