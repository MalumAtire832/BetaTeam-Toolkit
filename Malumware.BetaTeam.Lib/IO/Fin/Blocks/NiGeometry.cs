using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Geometry is stored in the shape itself; this engine version has no separate geometry data block
    public abstract class NiGeometry : NiAVObject
    {
        public ushort VertexCount { get; private set; }
        public Vector3[]? Vertices { get; private set; }
        public Vector3[]? Normals { get; private set; }
        public Vector3 BoundCenter { get; private set; }
        public float BoundRadius { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            VertexCount = reader.ReadUInt16();
            Vertices = ReadOptionalArray(reader, VertexCount, r => r.ReadVector3());
            Normals = ReadOptionalArray(reader, VertexCount, r => r.ReadVector3());
            BoundCenter = reader.ReadVector3();
            BoundRadius = reader.ReadSingle();
        }

        // Each optional array is preceded by the address it had when saved; zero means the array is absent
        protected static T[]? ReadOptionalArray<T>(FinBlockReader reader, int count, Func<FinBlockReader, T> read)
        {
            return reader.ReadUInt32() != 0 ? reader.ReadArray(count, read) : null;
        }
    }
}
