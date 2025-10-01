using Microsoft.Extensions.DependencyInjection;

namespace ServiceScan.SourceGenerator.Playground;

public interface ICommandHandler<T> { }
public class CommandHandlerDecorator<T>(ICommandHandler<T> inner) : ICommandHandler<T>;

public class SpecificHandler1 : ICommandHandler<string>;
public class SpecificHandler2 : ICommandHandler<long>;

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
