namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public class WavefrontObj
    {
        public IReadOnlyList<string> MaterialLibraries { get; }
        public IReadOnlyList<WavefrontObjVertex> Vertices { get; }
        public IReadOnlyList<WavefrontObjObject> Objects { get; }

        public WavefrontObj(List<WavefrontObjVertex> vertices, List<WavefrontObjObject> objects, List<string> materialLibraries)
        {
            Vertices = vertices;
            Objects = objects;
            MaterialLibraries = materialLibraries;
        }
    }
}