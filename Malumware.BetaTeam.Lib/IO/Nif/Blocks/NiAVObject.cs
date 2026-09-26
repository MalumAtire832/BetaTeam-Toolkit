using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public abstract class NiAVObject : NiObjectNET
    {
        public const ushort FLAG_HIDDEN = 0x0001;

        public ushort Flags { get; set; }
        public Vector3 Translation { get; set; }
        public NifMatrix33 Rotation { get; set; } = NifMatrix33.Identity;
        public float Scale { get; set; } = 1;
        public Vector3 Velocity { get; set; }
        public List<NiProperty> Properties { get; } = [];
        public NifBoundingVolume? BoundingVolume { get; set; }

        internal override IEnumerable<NiObject?> GetLinks()
        {
            return base
                .GetLinks()
                .Concat(Properties);
        }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Flags);
            writer.WriteVector3(Translation);
            writer.WriteMatrix33(Rotation);
            writer.WriteSingle(Scale);
            writer.WriteVector3(Velocity);
            writer.WriteRefList(Properties);
            writer.WriteBool(BoundingVolume is not null);
            BoundingVolume?.Write(writer);
        }
    }
}
