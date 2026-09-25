namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiTriShape : NiTriBasedGeom
    {
        public FinTriangle[] Triangles { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Triangles = reader.ReadArray(TriangleCount, r => new FinTriangle(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16()));
        }
    }
}
