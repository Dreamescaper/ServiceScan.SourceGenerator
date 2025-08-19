namespace ServiceScan.SourceGenerator;

internal static class GenerateAttributeInfo
{
    public const string MetadataName = "ServiceScan.SourceGenerator.GenerateServiceRegistrationsAttribute";

    public const string Source = """
        #nullable enable

        using System;
        using System.Diagnostics;
        using Microsoft.Extensions.DependencyInjection;

        namespace ServiceScan.SourceGenerator;

        [Conditional("CODE_ANALYSIS")]
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        internal class GenerateServiceRegistrationsAttribute : Attribute
        {
            /// <summary>
            /// Sets the assembly containing the given type as the source of types to register.
            /// If not specified, the assembly containing the method with this attribute will be used.
            /// </summary>
            public Type? FromAssemblyOf { get; set; }
        
            /// <summary>
            /// Sets this value to filter scanned assemblies by assembly name.
            /// It allows applying an attribute to multiple assemblies.
            /// For example, this allows scanning all assemblies from your solution.
            /// This option is incompatible with <see cref="FromAssemblyOf"/>.
            /// You can use '*' wildcards. You can also use ',' to separate multiple filters.
            /// </summary>
            /// <remarks>Be careful to include a limited number of assemblies, as it can affect build and editor performance.</remarks>
            /// <example>My.Product.*</example>
            public string? AssemblyNameFilter { get; set; }
        
            /// <summary>
            /// Sets the type that the registered types must be assignable to.
            /// Types will be registered with this type as the service type,
            /// unless <see cref="AsImplementedInterfaces"/> or <see cref="AsSelf"/> is set.
            /// </summary>
            public Type? AssignableTo { get; set; }

            /// <summary>
            /// Sets the type that the registered types must *not* be assignable to.
            /// </summary>
            public Type? ExcludeAssignableTo { get; set; }

            /// <summary>
            /// Sets the lifetime of the registered services.
            /// <see cref="ServiceLifetime.Transient"/> is used if not specified.
            /// </summary>
            public ServiceLifetime Lifetime { get; set; }
        
            /// <summary>
            /// If set to true, types will be registered as their implemented interfaces instead of their actual type.
            /// </summary>
            public bool AsImplementedInterfaces { get; set; }
        
            /// <summary>
            /// If set to true, types will be registered with their actual type.
            /// It can be combined with <see cref="AsImplementedInterfaces"/>. In this case, implemented interfaces will be
            /// "forwarded" to the "self" implementation.
            /// </summary>
            public bool AsSelf { get; set; }
        
            /// <summary>
            /// Sets this value to filter the types to register by their full name. 
            /// You can use '*' wildcards.
            /// You can also use ',' to separate multiple filters.
            /// </summary>
            /// <example>Namespace.With.Services.*</example>
            /// <example>*Service,*Factory</example>
            public string? TypeNameFilter { get; set; }
        
            /// <summary>
            /// Filters types by the specified attribute type being present.
            /// </summary>
            public Type? AttributeFilter { get; set; }
        
            /// <summary>
            /// Sets this value to exclude types from being registered by their full name. 
            /// You can use '*' wildcards.
            /// You can also use ',' to separate multiple filters.
            /// </summary>
            /// <example>Namespace.With.Services.*</example>
            /// <example>*Service,*Factory</example>
            public string? ExcludeByTypeName { get; set; }
        
            /// <summary>
            /// Excludes matching types by the specified attribute type being present.
            /// </summary>
            public Type? ExcludeByAttribute { get; set; }
        
            /// <summary>
            /// Sets this property to add types as keyed services. 
            /// This property should point to one of the following:
            /// - The name of a static method in the current type with a string return type.
            /// The method should be either generic or have a single parameter of type <see cref="Type"/>.
            /// - A constant field or static property in the implementation type.
            /// </summary>
            /// <example>nameof(GetKey)</example>
            public string? KeySelector { get; set; }
        
            /// <summary>
            /// Sets this property to invoke a custom method for each type found instead of regular registration logic.
            /// This property should point to one of the following:
            /// - Name of a generic method in the current type.
            /// - Static method name in found types.
            /// This property is incompatible with <see cref="Lifetime"/>, <see cref="AsImplementedInterfaces"/>, <see cref="AsSelf"/>,
            /// and <see cref="KeySelector"/> properties.
            /// </summary>
            public string? CustomHandler { get; set; }
        }
        """;
}

