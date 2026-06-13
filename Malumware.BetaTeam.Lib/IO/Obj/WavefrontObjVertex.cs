namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public struct WavefrontObjVertex
    {
        public readonly decimal X;
        public readonly decimal Y;
        public readonly decimal Z;
        public readonly decimal? W;

        public WavefrontObjVertex(decimal x, decimal y, decimal z, decimal? w = null)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }
}