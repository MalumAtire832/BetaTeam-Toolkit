using System.Collections;
using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // Turns parsed blocks into glTF extras, which Blender shows as custom properties. Like the dump, it walks the
    // blocks by reflection, so new fields and block classes need no code here.
    internal sealed class FinGltfExtrasBuilder
    {
        // Bounding volumes nest the deepest, and the reader already limits them to 32 levels
        public const int MAX_DEPTH = 256;

        // Written as "class" and "linkId", or not at all: file offsets mean nothing outside the file
        private static readonly HashSet<string> HEADER_PROPERTIES =
            [nameof(NiObject.ClassName), nameof(NiObject.Offset), nameof(NiObject.LinkId)];

        // Fields the glTF node itself holds (transform, hierarchy, mesh), which would only repeat it
        private static readonly HashSet<(Type, string)> NODE_FIELDS =
        [
            (typeof(NiAVObject), nameof(NiAVObject.Translation)),
            (typeof(NiAVObject), nameof(NiAVObject.Rotation)),
            (typeof(NiAVObject), nameof(NiAVObject.Scale)),
            (typeof(NiNode), nameof(NiNode.Children)),
            (typeof(NiGeometry), nameof(NiGeometry.VertexCount)),
            (typeof(NiGeometry), nameof(NiGeometry.Vertices)),
            (typeof(NiGeometry), nameof(NiGeometry.Normals)),
            (typeof(NiTriBasedGeom), nameof(NiTriBasedGeom.TriangleCount)),
            (typeof(NiTriBasedGeom), nameof(NiTriBasedGeom.TextureSetCount)),
            (typeof(NiTriBasedGeom), nameof(NiTriBasedGeom.TextureSets)),
            (typeof(NiTriBasedGeom), nameof(NiTriBasedGeom.Colors)),
            // Derived from the triangles, which the mesh already has
            (typeof(NiTriBasedGeom), nameof(NiTriBasedGeom.TrianglePlanes)),
            (typeof(NiTriShape), nameof(NiTriShape.Triangles)),
        ];

        // Blocks being expanded right now, so a property that links back to itself ends in a reference
        private readonly HashSet<NiObject> _expanding = new(ReferenceEqualityComparer.Instance);

        public static string FormatLinkId(uint linkId)
        {
            return $"0x{linkId:X8}";
        }

        // Properties and images describe how the object is drawn, so they're written out in full with every object
        // that uses them. Any other block is written once (as a node, or in the scene root's extras) and referred to.
        public static bool IsInline(NiObject block)
        {
            return block is NiProperty or NiImage;
        }

        // The fields of a block that becomes a glTF node, without the ones the node already holds
        public JsonObject BuildNode(NiAVObject block)
        {
            return BuildBlock(block, true, 0);
        }

        public JsonObject BuildBlock(NiObject block)
        {
            return BuildBlock(block, false, 0);
        }

        private JsonObject BuildBlock(NiObject block, bool isNode, int depth)
        {
            var result = new JsonObject
            {
                ["class"] = block.ClassName,
                ["linkId"] = FormatLinkId(block.LinkId),
            };

            _expanding.Add(block);
            AddFields(result, block, isNode, depth);
            _expanding.Remove(block);

            return result;
        }

        private void AddFields(JsonObject target, object value, bool isNode, int depth)
        {
            if (depth >= MAX_DEPTH)
            {
                throw new InvalidDataException($"Extra data is nested more than {MAX_DEPTH} levels deep");
            }

            var skipHeader = value is NiObject or NiExtraData;
            foreach (var property in FinReflection.PropertiesBaseFirst(value.GetType()))
            {
                if (skipHeader && HEADER_PROPERTIES.Contains(property.Name))
                {
                    continue;
                }
                if (isNode && NODE_FIELDS.Contains((property.DeclaringType!, property.Name)))
                {
                    continue;
                }

                // Blender can't store a custom property without a value, so absent fields are left out
                var node = BuildValue(property.GetValue(value), property.PropertyType, depth + 1);
                if (node is not null)
                {
                    target[property.Name] = node;
                }
            }
        }

        private JsonNode? BuildValue(object? value, Type declaredType, int depth)
        {
            switch (value)
            {
                case null:
                    return null;
                case FinRef reference:
                    return BuildReference(reference, depth);
                case NiExtraData extraData:
                    var result = new JsonObject { ["class"] = extraData.ClassName };
                    AddFields(result, extraData, false, depth);

                    return result;
                case string text:
                    return JsonValue.Create(text);
                case bool flag:
                    return JsonValue.Create(flag);
                case Enum enumValue:
                    return JsonValue.Create(enumValue.ToString());
                case float number:
                    return BuildFloat(number);
                case double number:
                    return double.IsFinite(number) ? JsonValue.Create(number) : BuildNonFinite(number);
                case byte or sbyte or short or ushort or int or uint or long:
                    return JsonValue.Create(Convert.ToInt64(value));
                case Vector2 v:
                    return new JsonArray(BuildFloat(v.X), BuildFloat(v.Y));
                case Vector3 v:
                    return new JsonArray(BuildFloat(v.X), BuildFloat(v.Y), BuildFloat(v.Z));
                case FinMatrix3 m:
                    return new JsonArray(
                        BuildFloat(m.M11), BuildFloat(m.M12), BuildFloat(m.M13),
                        BuildFloat(m.M21), BuildFloat(m.M22), BuildFloat(m.M23),
                        BuildFloat(m.M31), BuildFloat(m.M32), BuildFloat(m.M33)
                    );
                case IEnumerable items:
                    var elementType = ElementType(value.GetType());
                    var array = new JsonArray();
                    foreach (var item in items)
                    {
                        array.Add(BuildValue(item, elementType, depth + 1));
                    }

                    return array;
                default:
                    return BuildObject(value, declaredType, depth);
            }
        }

        // Colours, ranges, keys, bounding volumes and other plain objects, as their properties
        private JsonObject BuildObject(object value, Type declaredType, int depth)
        {
            var result = new JsonObject();
            var type = value.GetType();
            // Only needed where the field's type doesn't already say what it is, as with bounding volumes
            if (type != declaredType && !type.IsValueType)
            {
                result["class"] = type.Name;
            }

            if (type.IsValueType)
            {
                foreach (var property in FinReflection.DeclaredProperties(type))
                {
                    var node = BuildValue(property.GetValue(value), property.PropertyType, depth + 1);
                    if (node is not null)
                    {
                        result[property.Name] = node;
                    }
                }

                return result;
            }

            AddFields(result, value, false, depth);

            return result;
        }

        private JsonObject? BuildReference(FinRef reference, int depth)
        {
            var target = reference.Target;
            if (target is null)
            {
                return null;
            }
            if (IsInline(target) && !_expanding.Contains(target))
            {
                return BuildBlock(target, false, depth);
            }

            return new JsonObject
            {
                ["ref"] = FormatLinkId(target.LinkId),
                ["class"] = target.ClassName,
            };
        }

        public static JsonValue BuildFloat(float number)
        {
            return float.IsFinite(number) ? JsonValue.Create(number) : BuildNonFinite(number);
        }

        // JSON has no literal for NaN or infinity; written as strings, the same way as in the JSON dump
        private static JsonValue BuildNonFinite(double number)
        {
            return JsonValue.Create(double.IsNaN(number) ? "NaN" : number > 0 ? "Infinity" : "-Infinity");
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType()!;
            }
            var enumerable = type
                .GetInterfaces()
                .Append(type)
                .FirstOrDefault(e => e.IsGenericType && e.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable?.GetGenericArguments()[0] ?? typeof(object);
        }
    }
}
