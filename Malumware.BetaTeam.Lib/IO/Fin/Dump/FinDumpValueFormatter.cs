using System.Globalization;
using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    // Formats single values for the text dump, independent of the current culture
    public static class FinDumpValueFormatter
    {
        public static string Format(object? value)
        {
            return value switch
            {
                null => "null",
                string text => Quote(text),
                bool flag => flag ? "true" : "false",
                Enum enumValue => enumValue.ToString(),
                Vector2 v => $"({Format(v.X)}, {Format(v.Y)})",
                Vector3 v => $"({Format(v.X)}, {Format(v.Y)}, {Format(v.Z)})",
                FinMatrix3 m => $"[({Format(m.M11)}, {Format(m.M12)}, {Format(m.M13)}), " +
                                $"({Format(m.M21)}, {Format(m.M22)}, {Format(m.M23)}), " +
                                $"({Format(m.M31)}, {Format(m.M32)}, {Format(m.M33)})]",
                IFormattable number when value.GetType().IsPrimitive => number.ToString(null, CultureInfo.InvariantCulture),
                _ when value.GetType().IsValueType => FormatStruct(value),
                _ => value.ToString() ?? "",
            };
        }

        // Record structs (colours, triangles, ranges) as their property values in declaration order
        private static string FormatStruct(object value)
        {
            var values = FinReflection.DeclaredProperties(value.GetType())
                .Select(property => Format(property.GetValue(value)));
            return $"({string.Join(", ", values)})";
        }

        private static string Quote(string text)
        {
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        }
    }
}
