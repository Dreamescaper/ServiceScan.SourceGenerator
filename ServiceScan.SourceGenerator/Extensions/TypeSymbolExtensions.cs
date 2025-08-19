using System.Linq;
using Microsoft.CodeAnalysis;

namespace ServiceScan.SourceGenerator.Extensions;

internal static class TypeSymbolExtensions
{
    /// <summary>
    /// Retrieves a method symbol from the specified type by name, considering accessibility, static context, and
    /// inheritance.
    /// </summary>
    /// <remarks>This method searches the specified type and its base types for a method with the given name
    /// that matches the specified accessibility and static context. If no matching method is found, the method returns
    /// <see langword="null"/>.</remarks>
    public static IMethodSymbol? GetMethod(this ITypeSymbol type, string methodName, SemanticModel semanticModel, int position, bool? isStatic = null)
    {
        var currentType = type;

        while (currentType != null)
        {
            var method = currentType.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.Name == methodName
                    && (isStatic == null || m.IsStatic == isStatic)
                    && semanticModel.IsAccessible(position, m))
                .FirstOrDefault();

            if (method != null)
                return method;

            currentType = currentType.BaseType;
        }

        return null;
    }
}
