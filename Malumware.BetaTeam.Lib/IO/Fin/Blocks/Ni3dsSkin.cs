using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A mesh deformed by bones: per vertex, the bones that move it and by how much
    public class Ni3dsSkin : NiTriShape
    {
        public byte Unknown12 { get; private set; }
        public IReadOnlyList<FinSkinInfluence[]>? SkinVertices { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Unknown12 = reader.ReadByte();
            if (reader.ReadByte() != 0)
            {
                SkinVertices = reader.ReadArray(VertexCount, ReadInfluences);
            }
        }

        private static FinSkinInfluence[] ReadInfluences(FinBlockReader reader)
        {
            var count = reader.ReadUInt16();
            return reader.ReadArray(count, r => new FinSkinInfluence(r.ReadSingle(), r.ReadVector3(), r.ReadRef<Ni3dsBone>()));
        }
    }
}
