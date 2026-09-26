using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public abstract class NiTriBasedGeom : NiGeometry
    {
        public ushort TriangleCount { get; private set; }
        public ushort TextureSetCount { get; private set; }

        // One array per texture set, each with a coordinate per vertex. The engine reads three floats per
        // coordinate, not two.
        public IReadOnlyList<Vector3[]>? TextureSets { get; private set; }

        public FinColor4[]? Colors { get; private set; }
        public FinPlane[]? TrianglePlanes { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            TriangleCount = reader.ReadUInt16();
            TextureSetCount = reader.ReadUInt16();
            TextureSets = reader.ReadUInt32() != 0
                ? reader.ReadArray(TextureSetCount, r => r.ReadArray(VertexCount, v => v.ReadVector3()))
                : null;
            Colors = ReadOptionalArray(reader, VertexCount, r => r.ReadColor4());
            TrianglePlanes = ReadOptionalArray(reader, TriangleCount, r => new FinPlane(r.ReadVector3(), r.ReadSingle()));
        }
    }
}
