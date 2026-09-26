using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A mesh that blends between several versions of its vertex positions (morph targets) over time
    public class Ni3dsMorphShape : NiTriShape
    {
        public FinAnimationCore Core { get; private set; } = null!;
        public byte UnknownE8 { get; private set; }
        public byte UnknownE9 { get; private set; }
        public Vector3 MorphBoundCenter { get; private set; }
        public float MorphBoundRadius { get; private set; }
        public FinKeyGroup<FinFloatKey> Keys { get; private set; } = FinKeyGroup<FinFloatKey>.Empty;

        // One position per vertex for each target
        public IReadOnlyList<Vector3[]> Targets { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Core = FinAnimationCore.Read(reader);
            UnknownE8 = reader.ReadByte();
            UnknownE9 = reader.ReadByte();
            var targetCount = reader.ReadSignedCount(Math.Max(1, VertexCount * 12));
            var keyCount = reader.ReadSignedCount(1);
            MorphBoundCenter = reader.ReadVector3();
            MorphBoundRadius = reader.ReadSingle();
            Keys = FinKeyReader.ReadFloatKeys(reader, keyCount, reader.ReadUInt32());
            Targets = reader.ReadArray(targetCount, r => r.ReadArray(VertexCount, v => v.ReadVector3()));
        }
    }
}
