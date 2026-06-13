namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public class WavefrontObjReader
    {
        public WavefrontObj Read(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return Read(stream);
        }

        internal static WavefrontObj Read(Stream stream)
        {
            using var parser = new WavefrontObjParser(stream);
            return parser.Parse();
        }
    }
}