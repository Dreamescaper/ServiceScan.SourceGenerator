# ServiceScan.SourceGenerator
[![NuGet Version](https://img.shields.io/nuget/v/ServiceScan.SourceGenerator)](https://www.nuget.org/packages/ServiceScan.SourceGenerator/)
[![Stand With Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/badges/StandWithUkraine.svg)](https://stand-with-ukraine.pp.ua)

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
    [GenerateServiceRegistrations(AssignableTo = typeof(IEndpoint), CustomHandler = nameof(IEndpoint.MapEndpoint))]
    public static partial IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder endpoints);
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

### Apply EF Core IEntityTypeConfiguration types

```csharp
public static partial class ModelBuilderExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IEntityTypeConfiguration<>), CustomHandler = nameof(ApplyConfiguration))]
    public static partial ModelBuilder ApplyEntityConfigurations(this ModelBuilder modelBuilder);

    private static void ApplyConfiguration<T, TEntity>(ModelBuilder modelBuilder)
        where T : IEntityTypeConfiguration<TEntity>, new()
        where TEntity : class
    {
        modelBuilder.ApplyConfiguration(new T());
    }
}
```



## Parameters

`GenerateServiceRegistrations` attribute has the following properties:
| Property | Description |
| --- | --- |
| **FromAssemblyOf** | Sets the assembly containing the given type as the source of types to register. If not specified, the assembly containing the method with this attribute will be used. |
| **AssemblyNameFilter** | Sets this value to filter scanned assemblies by assembly name. It allows applying an attribute to multiple assemblies. For example, this allows scanning all assemblies from your solution. This option is incompatible with `FromAssemblyOf`. You can use '*' wildcards. You can also use ',' to separate multiple filters. *Be careful to include a limited number of assemblies, as it can affect build and editor performance.* |
| **AssignableTo** | Sets the type that the registered types must be assignable to. Types will be registered with this type as the service type, unless `AsImplementedInterfaces` or `AsSelf` is set. |
| **ExcludeAssignableTo** | Sets the type that the registered types must *not* be assignable to. |
| **Lifetime** | Sets the lifetime of the registered services. `ServiceLifetime.Transient` is used if not specified. |
| **AsImplementedInterfaces** | If set to true, types will be registered as their implemented interfaces instead of their actual type. |
| **AsSelf** | If set to true, types will be registered with their actual type. It can be combined with `AsImplementedInterfaces`. In this case, implemented interfaces will be "forwarded" to the "self" implementation. |
| **TypeNameFilter** | Sets this value to filter the types to register by their full name. You can use '*' wildcards. You can also use ',' to separate multiple filters. |
| **AttributeFilter** | Filters types by the specified attribute type being present. |
| **ExcludeByTypeName** | Sets this value to exclude types from being registered by their full name. You can use '*' wildcards. You can also use ',' to separate multiple filters. |
| **ExcludeByAttribute** | Excludes matching types by the specified attribute type being present. |
| **KeySelector** | Sets this property to add types as keyed services. This property should point to one of the following: <br>- The name of a static method in the current type with a string return type. The method should be either generic or have a single parameter of type `Type`. <br>- A constant field or static property in the implementation type. |
| **CustomHandler** | Sets this property to invoke a custom method for each type found instead of regular registration logic. This property should point to one of the following: <br>- Name of a generic method in the current type. <br>- Static method name in found types. <br>This property is incompatible with `Lifetime`, `AsImplementedInterfaces`, `AsSelf`, and `KeySelector` properties. |