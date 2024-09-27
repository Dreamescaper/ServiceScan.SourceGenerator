namespace ServiceScan.SourceGenerator;

internal static class GenerateAttributeSource
{
    public static string Source => """
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
            /// Set the assembly containing the given type as the source of types to register.
            /// If not specified, the assembly containing the method with this attribute will be used.
            /// </summary>
            public Type? FromAssemblyOf { get; set; }

            /// <summary>
            /// Set the type that the registered types must be assignable to.
            /// Types will be registered with this type as the service type,
            /// unless <see cref="AsImplementedInterfaces"/> or <see cref="AsSelf"/> is set.
            /// </summary>
            public Type? AssignableTo { get; set; }

            /// <summary>
            /// Set the lifetime of the registered services.
            /// <see cref="ServiceLifetime.Transient"/> is used if not specified.
            /// </summary>
            public ServiceLifetime Lifetime { get; set; }
        
            /// <summary>
            /// If set to true, types will be registered as implemented interfaces instead of their actual type.
            /// </summary>
            public bool AsImplementedInterfaces { get; set; }
        
            /// <summary>
            /// If set to true, types will be registered with their actual type.
            /// It can be combined with <see cref="AsImplementedInterfaces"/>, in that case implemeted interfaces will be
            /// "forwarded" to "self" implementation.
            /// </summary>
            public bool AsSelf { get; set; }
        
            /// <summary>
            /// Set this value to filter the types to register by their full name. 
            /// You can use '*' wildcards.
            /// You can also use ',' to separate multiple filters.
            /// </summary>
            /// <example>Namespace.With.Services.*</example>
            /// <example>*Service,*Factory</example>
            public string? TypeNameFilter { get; set; }
        
            /// <summary>
            /// Set this value to a static method returning string.
            /// Returned value will be used as a key for the registration.
            /// Method should either be generic, or have a single parameter of type <see cref="Type"/>.
            /// </summary>
            /// <example>nameof(GetKey)</example>
            public string? KeySelector { get; set; }
        }
        """;
}