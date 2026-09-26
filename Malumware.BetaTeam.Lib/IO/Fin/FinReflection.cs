using System.Reflection;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Lists the public properties of parsed objects in a stable order, for code that walks them generically
    internal static class FinReflection
    {
        // Reflection doesn't guarantee order, so sort by declaration order
        public static IEnumerable<PropertyInfo> DeclaredProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(e => e.GetIndexParameters().Length == 0)
                .OrderBy(e => e.MetadataToken);
        }

        // Base class properties first, the order in which the engine reads them
        public static IEnumerable<PropertyInfo> PropertiesBaseFirst(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            {
                hierarchy.Push(current);
            }

            return hierarchy.SelectMany(DeclaredProperties);
        }
    }
}
