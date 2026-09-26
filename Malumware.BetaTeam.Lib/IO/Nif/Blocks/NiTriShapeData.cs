namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiTriShapeData : NiTriBasedGeomData
    {
        public NifTriangle[] Triangles { get; set; } = [];

        internal override void Write(NifWriter writer)
        {
            if (Triangles.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException($"{Triangles.Length} triangles, at most {ushort.MaxValue} fit");
            }

            base.Write(writer);
            writer.WriteUInt16((ushort)Triangles.Length);
            writer.WriteUInt32((uint)Triangles.Length * 3);
            foreach (var triangle in Triangles)
            {
                writer.WriteUInt16(triangle.V1);
                writer.WriteUInt16(triangle.V2);
                writer.WriteUInt16(triangle.V3);
            }
            // Match groups (vertices that share a normal): none
            writer.WriteUInt16(0);
        }
    }
}
