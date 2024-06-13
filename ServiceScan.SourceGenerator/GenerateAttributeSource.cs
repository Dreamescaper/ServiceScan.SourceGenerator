﻿namespace ServiceScan.SourceGenerator;

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
            /// If set to true, the registered types will be registered as implemented interfaces instead of their actual type.
            /// </summary>
            public bool AsImplementedInterfaces { get; set; }
        
            /// <summary>
            /// This option will also register the type itself.
            /// Combined with <see cref="AsImplementedInterfaces"/>, TODO.
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
        }
        """;
}