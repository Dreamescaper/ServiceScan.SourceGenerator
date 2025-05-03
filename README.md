# ServiceScan.SourceGenerator
[![NuGet Version](https://img.shields.io/nuget/v/ServiceScan.SourceGenerator)](https://www.nuget.org/packages/ServiceScan.SourceGenerator/)

Source generator for services registrations inspired by [Scrutor](https://github.com/khellang/Scrutor/).
Code generation allows to have AOT-compatible code, without an additional hit on startup performance due to runtime assembly scanning.

## Installation 
Add the NuGet Package to your project:
```
dotnet add package ServiceScan.SourceGenerator
```

## Usage

`ServiceScan` generates a partial method implementation based on `GenerateServiceRegistrations` attribute. This attribute can be added to a partial method with `IServiceCollection` parameter. 
For example, based on the following partial method:
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

The only thing left is to invoke this method on your `IServiceCollection` instance
```csharp
services.AddServices();
```

## Examples

### Register all [FluentValidation](https://github.com/FluentValidation/FluentValidation) validators
Unlike using `FluentValidation.DependencyInjectionExtensions` package, `ServiceScan` is AOT-compatible, and doesn't affect startup performance:
```csharp
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
It adds MediatR requests handlers, although you might need to add other types like PipelineBehaviors or NotificationHandlers.

### Add all repository types from your project based on name filter as their implemented interfaces:
```csharp
[GenerateServiceRegistrations(
    TypeNameFilter = "*Repository",
    AsImplementedInterfaces = true,
    Lifetime = ServiceLifetime.Scoped)]
private static partial IServiceCollection AddRepositories(this IServiceCollection services);
```

### Add AspNetCore Minimal API endpoints
You can add custom type handler, if you need to do something non-trivial with that type. For example, you can automatically discover
and map Minimal API endpoints:
```csharp
public interface IEndpoint
{
    abstract static void MapEndpoint(IEndpointRouteBuilder endpoints);
}

public class HelloWorldEndpoint : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", () => "Hello World!");
    }
}

public static partial class ServiceCollectionExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IEndpoint), CustomHandler = nameof(MapEndpoint))]
    public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);

    private static void MapEndpoint<T>(IEndpointRouteBuilder endpoints) where T : IEndpoint
    {
        T.MapEndpoint(endpoints);
    }
}
```

### Register Options types
Another example of `CustomHandler` is to register Options types. We can define custom `OptionAttribute`, which allows to specify configuration section key.
And then read that value in our `CustomHandler`:
```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class OptionAttribute(string? section = null) : Attribute
{
    public string? Section { get; } = section;
}

[Option]
public record RootSection { }

[Option("SectionOption")]
public record SectionOption { }

public static partial class ServiceCollectionExtensions
{
    [GenerateServiceRegistrations(AttributeFilter = typeof(OptionAttribute), CustomHandler = nameof(AddOption))]
    public static partial IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration);

    private static void AddOption<T>(IServiceCollection services, IConfiguration configuration) where T : class
    {
        var sectionKey = typeof(T).GetCustomAttribute<OptionAttribute>()?.Section;
        var section = sectionKey is null ? configuration : configuration.GetSection(sectionKey);
        services.Configure<T>(section);
    }
}
```


## Parameters

`GenerateServiceRegistrations` attribute has the following properties:
| Property | Description |
| --- | --- |
| **FromAssemblyOf** | Set the assembly containing the given type as the source of types to register. If not specified, the assembly containing the method with this attribute will be used. |
| **AssignableTo** | Set the type that the registered types must be assignable to. Types will be registered with this type as the service type, unless `AsImplementedInterfaces` or `AsSelf` is set. |
| **Lifetime** | Set the lifetime of the registered services. `ServiceLifetime.Transient` is used if not specified. |
| **AsImplementedInterfaces** | If true, the registered types will be registered as implemented interfaces instead of their actual type. |
| **AsSelf** | If true, types will be registered with their actual type. It can be combined with `AsImplementedInterfaces`. In that case, implemented interfaces will be "forwarded" to an actual implementation type. |
| **TypeNameFilter** | Set this value to filter the types to register by their full name. You can use '*' wildcards. You can also use ',' to separate multiple filters. |
| **AttributeFilter** | Filter types by the specified attribute type present. |
| **ExcludeByTypeName** | Set this value to exclude types from being registered by their full name. You can use '*' wildcards. You can also use ',' to separate multiple filters. |
| **ExcludeByAttribute** | Exclude matching types by the specified attribute type present. |
| **ExcludeAssignableTo** | Set the type that the registered types must not be assignable to. |
| **KeySelector** | Set this property to add types as keyed services. This property should point to one of the following: <br>- Name of the static method in the current type with a string return type. The method should be either generic or have a single parameter of type `Type`. <br>- Const field or static property in the implementation type. |
| **CustomHandler** | Set this property to a static generic method name in the current class. This method will be invoked for each type found by the filter instead of the regular registration logic. This property is incompatible with `Lifetime`, `AsImplementedInterfaces`, `AsSelf`, and `KeySelector` properties. |