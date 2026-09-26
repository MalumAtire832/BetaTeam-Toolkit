using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public abstract class NiGeometryData : NiObject
    {
        // The lower six bits of the data flags hold the number of UV sets
        public const int MAX_UV_SETS = 63;

        public ushort VertexCount { get; set; }

        // Each array is either null or has VertexCount entries
        public Vector3[]? Vertices { get; set; }
        public Vector3[]? Normals { get; set; }
        public Vector3 BoundCenter { get; set; }
        public float BoundRadius { get; set; }
        public NifColor4[]? VertexColors { get; set; }
        public List<Vector2[]> UvSets { get; } = [];

        internal override void Write(NifWriter writer)
        {
            if (UvSets.Count > MAX_UV_SETS)
            {
                throw new InvalidOperationException($"{UvSets.Count} UV sets, at most {MAX_UV_SETS} fit");
            }

            writer.WriteUInt16(VertexCount);
            WriteOptionalArray(writer, Vertices, writer.WriteVector3);
            WriteOptionalArray(writer, Normals, writer.WriteVector3);
            writer.WriteVector3(BoundCenter);
            writer.WriteSingle(BoundRadius);
            WriteOptionalArray(writer, VertexColors, writer.WriteColor4);
            writer.WriteUInt16((ushort)UvSets.Count);
            writer.WriteBool(UvSets.Count > 0);
            foreach (var uvSet in UvSets)
            {
                CheckLength(uvSet.Length);
                foreach (var uv in uvSet)
                {
                    writer.WriteVector2(uv);
                }
            }
        }

        private void WriteOptionalArray<T>(NifWriter writer, T[]? values, Action<T> write)
        {
            writer.WriteBool(values is not null);
            if (values is null)
            {
                return;
            }

            CheckLength(values.Length);
            foreach (var value in values)
            {
                write(value);
            }
        }

        // The file stores one count for every per-vertex array
        private void CheckLength(int length)
        {
            if (length != VertexCount)
            {
                throw new InvalidOperationException($"Per-vertex array has {length} entries, expected {VertexCount}");
            }
        }
    }
}
