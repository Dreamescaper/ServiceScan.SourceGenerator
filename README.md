# ServiceScan.SourceGenerator

Source generator for services registrations inspired by [Scrutor](https://github.com/khellang/Scrutor/).
Code generation allows to have AOT compatible code, without additional hit on startup performance due to runtime assembly scanning.

## Installation 
Add the NuGet Package to your project:
```
dotnet add package ServiceScan.SourceGenerator
```

## Usage

`ServiceScan` generates a partial method implementation based on `GenerateServiceRegistrations` attribute. This attribute can be added to a partial method with `IServiceCollection` parameter. 
For examples, based on the following partial method:
```csharp
public static partial class ServicesExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IMyService), Lifetime = ServiceLifetime.Scoped)]
    public static partial IServiceCollection AddServices(this IServiceCollection services);
}
```

`ServiceScan` will generate the following implementation:
```csharp
public static partial class ServicesExtensions
{
    public static partial IServiceCollection AddServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IMyService, ServiceImplementation1>()
            .AddScoped<IMyService, ServiceImplementation2>();
    }
}
```

The only thing left is to invoke this method on your `IServiceCollection` instance.

## Examples

### Register all [FluentValidation](https://github.com/FluentValidation/FluentValidation) validators
Unlike using `FluentValidation.DependencyInjectionExtensions` package, `ServiceScan` is AOT-compatible, and doesn't affect startup performance:
```chsarp
    [GenerateServiceRegistrations(AssignableTo = typeof(IValidator<>), Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddValidators(this IServiceCollection services);
```

### Add [MediatR](https://github.com/jbogard/MediatR) handlers
```csharp
    public static IServiceCollection AddMediatR(this IServiceCollection services)
    {
        return services
            .AddTransient<IMediator, Mediator>()
            .AddMediatRHandlers();
    }

    [GenerateServiceRegistrations(AssignableTo = typeof(IRequestHandler<>), Lifetime = ServiceLifetime.Transient)]
    [GenerateServiceRegistrations(AssignableTo = typeof(IRequestHandler<,>), Lifetime = ServiceLifetime.Transient)]
    private static partial IServiceCollection AddMediatRHandlers(this IServiceCollection services);
```
It adds MediatR handlers, which would work for simple cases, although you might need to add other types like PipelineBehaviors or NotificationHandlers.

### Add all types from your project based on name filter:
```chsarp
    [GenerateServiceRegistrations(TypeNameFilter = "MyNamespace.*Service", Lifetime = ServiceLifetime.Scoped)]
    private static partial IServiceCollection AddServices(this IServiceCollection services);
```
