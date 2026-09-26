using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    // Writes the complete dump as JSON; arrays and lists are always written in full
    public static class FinJsonDumpWriter
    {
        public static void Write(FinDump dump, Stream stream)
        {
            var options = new JsonWriterOptions
            {
                Indented = true,
                // Keep names like "Bräu" readable instead of \u escapes
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            using (var writer = new Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteString("name", dump.Name);
                writer.WriteNumber("version", dump.Version);
                writer.WriteStartArray("roots");
                foreach (var root in dump.Roots)
                {
                    WriteObject(writer, root);
                }
                writer.WriteEndArray();
                writer.WriteStartArray("unreferenced");
                foreach (var block in dump.Unreferenced)
                {
                    WriteObject(writer, block);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
        }

        private static void WriteObject(Utf8JsonWriter writer, FinDumpObject obj)
        {
            writer.WriteStartObject();
            writer.WriteString("class", obj.ClassName);
            if (obj.LinkId is { } linkId)
            {
                writer.WriteString("linkId", $"0x{linkId:X8}");
            }
            if (obj.Offset is { } offset)
            {
                writer.WriteNumber("offset", offset);
            }
            writer.WriteStartObject("fields");
            foreach (var child in obj.Children)
            {
                writer.WritePropertyName(child.Field ?? "");
                WriteNode(writer, child);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteNode(Utf8JsonWriter writer, FinDumpNode node)
        {
            switch (node)
            {
                case FinDumpObject obj:
                    WriteObject(writer, obj);
                    break;
                case FinDumpReference reference:
                    writer.WriteStartObject();
                    writer.WriteString("ref", $"0x{reference.LinkId:X8}");
                    writer.WriteString("class", reference.ClassName);
                    writer.WriteEndObject();
                    break;
                case FinDumpValue value:
                    WriteValue(writer, value.Value);
                    break;
                case FinDumpList list:
                    writer.WriteStartArray();
                    foreach (var item in list.Items)
                    {
                        WriteNode(writer, item);
                    }
                    writer.WriteEndArray();
                    break;
                case FinDumpArray array:
                    writer.WriteStartArray();
                    foreach (var item in array.Values)
                    {
                        WriteValue(writer, item);
                    }
                    writer.WriteEndArray();
                    break;
            }
        }

        private static void WriteValue(Utf8JsonWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string text:
                    writer.WriteStringValue(text);
                    break;
                case bool flag:
                    writer.WriteBooleanValue(flag);
                    break;
                case Enum enumValue:
                    writer.WriteStringValue(enumValue.ToString());
                    break;
                case float number:
                    WriteFloat(writer, number);
                    break;
                case double number:
                    WriteFloat(writer, number);
                    break;
                case byte or sbyte or short or ushort or int or uint or long:
                    writer.WriteNumberValue(Convert.ToInt64(value));
                    break;
                case ulong number:
                    writer.WriteNumberValue(number);
                    break;
                case Vector2 v:
                    WriteFloats(writer, v.X, v.Y);
                    break;
                case Vector3 v:
                    WriteFloats(writer, v.X, v.Y, v.Z);
                    break;
                case FinMatrix3 m:
                    WriteFloats(writer, m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33);
                    break;
                default:
                    WriteStruct(writer, value);
                    break;
            }
        }

        // JSON has no literal for NaN or infinity, and Utf8JsonWriter throws on them
        private static void WriteFloat(Utf8JsonWriter writer, double number)
        {
            if (double.IsNaN(number))
            {
                writer.WriteStringValue("NaN");
            }
            else if (double.IsPositiveInfinity(number))
            {
                writer.WriteStringValue("Infinity");
            }
            else if (double.IsNegativeInfinity(number))
            {
                writer.WriteStringValue("-Infinity");
            }
            else
            {
                writer.WriteNumberValue(number);
            }
        }

        private static void WriteFloat(Utf8JsonWriter writer, float number)
        {
            if (float.IsFinite(number))
            {
                writer.WriteNumberValue(number);
                return;
            }
            WriteFloat(writer, (double)number);
        }

        private static void WriteFloats(Utf8JsonWriter writer, params float[] values)
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                WriteFloat(writer, value);
            }
            writer.WriteEndArray();
        }

        // Other structs (colours, triangles, ranges) as an object of their properties
        private static void WriteStruct(Utf8JsonWriter writer, object value)
        {
            writer.WriteStartObject();
            foreach (var property in FinReflection.DeclaredProperties(value.GetType()))
            {
                writer.WritePropertyName(property.Name);
                WriteValue(writer, property.GetValue(value));
            }
            writer.WriteEndObject();
        }
    }
}
