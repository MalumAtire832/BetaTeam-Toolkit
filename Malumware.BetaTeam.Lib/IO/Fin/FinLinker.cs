using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Same job as the engine's LinkObject pass: link IDs are the objects' original addresses
    internal static class FinLinker
    {
        public static void Link(IReadOnlyList<NiObject> objects, IReadOnlyList<FinRef> refs)
        {
            var byLinkId = new Dictionary<uint, NiObject>(objects.Count);
            foreach (var block in objects)
            {
                if (!byLinkId.TryAdd(block.LinkId, block))
                {
                    throw new InvalidDataException(
                        $"Duplicate link ID 0x{block.LinkId:X8} on {block.ClassName} at offset 0x{block.Offset:X}"
                    );
                }
            }

            foreach (var reference in refs)
            {
                if (reference.IsNull)
                {
                    continue;
                }

                if (!byLinkId.TryGetValue(reference.LinkId, out var target))
                {
                    throw new InvalidDataException($"Reference to missing link ID 0x{reference.LinkId:X8}");
                }

                if (!reference.TargetType.IsInstanceOfType(target))
                {
                    throw new InvalidDataException(
                        $"Link ID 0x{reference.LinkId:X8} is a {target.ClassName}, expected {reference.TargetType.Name}"
                    );
                }

                reference.Resolve(target);
            }
        }
    }
}
