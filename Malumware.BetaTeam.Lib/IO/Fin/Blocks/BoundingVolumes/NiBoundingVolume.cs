namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // A collision shape, stored inline in its NiAVObject as a type number followed by the shape
    public abstract class NiBoundingVolume
    {
        // Unions and intersections nest; shipped files stay shallow, so a deep stack means a malformed file
        public const int MAX_DEPTH = 32;

        internal abstract void Load(FinBlockReader reader, int depth);

        internal static NiBoundingVolume Read(FinBlockReader reader, int depth = 0)
        {
            var offset = reader.Position;
            if (depth > MAX_DEPTH)
            {
                throw new InvalidDataException(
                    $"Bounding volumes nested more than {MAX_DEPTH} deep at offset 0x{offset:X}"
                );
            }

            var type = reader.ReadUInt32();
            NiBoundingVolume volume = type switch
            {
                0 => new NiSphereBV(),
                1 => new NiBoxBV(),
                2 => new NiCapsuleBV(),
                3 => new NiLozengeBV(),
                4 => new NiUnionBV(),
                5 => new DDHalfSpaceBV(),
                6 => new DDIntersectionBV(),
                _ => throw new InvalidDataException($"Unknown bounding volume type {type} at offset 0x{offset:X}"),
            };
            volume.Load(reader, depth);
            return volume;
        }

        protected static IReadOnlyList<NiBoundingVolume> ReadVolumes(FinBlockReader reader, int depth)
        {
            // Each nested volume takes at least its type number
            var count = reader.ReadCount(sizeof(uint));
            return reader.ReadArray(count, r => Read(r, depth + 1));
        }
    }
}
