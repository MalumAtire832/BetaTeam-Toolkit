namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public class WavefrontObjObject
    {
        public string? Name { get; }
        public string? Material { get; internal set; }

        private readonly List<int[]> _faces;
        public IReadOnlyList<int[]> Faces => _faces;

        public WavefrontObjObject()
        {
            Name = null;
            _faces = [];
        }

        public WavefrontObjObject(string name)
        {
            Name = name;
            _faces = [];
        }

        internal void AddFace(int[] face) => _faces.Add(face);
    }
}