using System.Collections;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    // Walks the parsed blocks by reflection, so new block classes need no dump code
    public static class FinDumpBuilder
    {
        // Shipped files nest at most 45 levels. The limit keeps a crafted file from overflowing the stack, and keeps
        // the JSON (about three levels per two dump levels) within Utf8JsonWriter's depth of 1000.
        public const int MAX_DEPTH = 256;

        // Shown on FinDumpObject itself, not as fields
        private static readonly HashSet<string> HEADER_PROPERTIES =
            [nameof(NiObject.ClassName), nameof(NiObject.Offset), nameof(NiObject.LinkId)];

        public static FinDump Build(FinFile file)
        {
            var visited = new HashSet<NiObject>(ReferenceEqualityComparer.Instance);
            var roots = file.TopLevelObjects
                .Select(block => BuildBlock(null, block, visited, 0))
                .ToList();

            var unreferenced = new List<FinDumpObject>();
            foreach (var block in file.Objects)
            {
                if (!visited.Contains(block))
                {
                    unreferenced.Add(BuildBlock(null, block, visited, 0));
                }
            }

            return new FinDump(file.Name, file.Header.Version, roots, unreferenced);
        }

        private static FinDumpObject BuildBlock(string? field, NiObject block, HashSet<NiObject> visited, int depth)
        {
            // Mark before descending, so cycles end in a reference
            visited.Add(block);
            return new FinDumpObject(field, block.ClassName, block.LinkId, block.Offset, BuildFields(block, visited, false, depth));
        }

        private static IReadOnlyList<FinDumpNode> BuildFields(object value, HashSet<NiObject> visited, bool inData, int depth)
        {
            if (depth >= MAX_DEPTH)
            {
                throw new InvalidDataException($"Blocks are nested more than {MAX_DEPTH} levels deep");
            }

            var skipHeader = value is NiObject or NiExtraData;
            var nodes = new List<FinDumpNode>();
            foreach (var property in FinReflection.PropertiesBaseFirst(value.GetType()))
            {
                if (skipHeader && HEADER_PROPERTIES.Contains(property.Name))
                {
                    continue;
                }
                nodes.Add(BuildField(property.Name, property.GetValue(value), visited, inData, depth + 1));
            }

            return nodes;
        }

        private static FinDumpNode BuildField(string? field, object? value, HashSet<NiObject> visited, bool inData, int depth)
        {
            switch (value)
            {
                case null:
                    return new FinDumpValue(field, null);
                case FinRef reference:
                    return BuildReference(field, reference, visited, !inData, depth);
                case NiExtraData extraData:
                    return new FinDumpObject(field, extraData.ClassName, null, extraData.Offset, BuildFields(extraData, visited, false, depth));
                case string:
                    return new FinDumpValue(field, value);
                case IEnumerable items:
                    return BuildList(field, value.GetType(), items, visited, inData, depth);
                default:
                    if (value.GetType().IsValueType)
                    {
                        return new FinDumpValue(field, value);
                    }

                    // Plain objects (keys, bounding volumes, skills) are data: links inside them aren't expanded
                    return new FinDumpObject(field, TypeName(value.GetType()), null, null, BuildFields(value, visited, true, depth));
            }
        }

        private static FinDumpNode BuildReference(string? field, FinRef reference, HashSet<NiObject> visited, bool expand, int depth)
        {
            var target = reference.Target;
            if (target is null)
            {
                return new FinDumpValue(field, null);
            }
            if (!expand || visited.Contains(target))
            {
                return new FinDumpReference(field, target.ClassName, target.LinkId);
            }

            return BuildBlock(field, target, visited, depth);
        }

        private static FinDumpNode BuildList(string? field, Type type, IEnumerable items, HashSet<NiObject> visited, bool inData, int depth)
        {
            var element = ElementType(type);
            if (element.IsValueType)
            {
                var values = items
                    .Cast<object?>()
                    .ToList();

                return new FinDumpArray(field, TypeName(element), values);
            }

            // Links and extra data make up the tree; anything else is data
            var isData = inData || !(typeof(FinRef).IsAssignableFrom(element) || typeof(NiExtraData).IsAssignableFrom(element));
            var nodes = items
                .Cast<object?>()
                .Select(item => BuildField(null, item, visited, isData, depth + 1))
                .ToList();

            return new FinDumpList(field, TypeName(element), nodes, isData);
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
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable.GetGenericArguments()[0];
        }

        // FinKeyGroup`1 reads better as FinKeyGroup<FinPosKey>
        private static string TypeName(Type type)
        {
            if (type.IsArray)
            {
                return TypeName(type.GetElementType()!) + "[]";
            }
            if (!type.IsGenericType)
            {
                return type.Name;
            }
            var name = type.Name[..type.Name.IndexOf('`')];
            var arguments = type
                .GetGenericArguments()
                .Select(TypeName);

            return $"{name}<{string.Join(", ", arguments)}>";
        }
    }
}
