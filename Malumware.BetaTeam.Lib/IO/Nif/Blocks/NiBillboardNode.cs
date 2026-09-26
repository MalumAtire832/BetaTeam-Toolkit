namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    // Before 10.1.0.0 the billboard mode lives in bits 5 and 6 of the flags, not in a field of its own
    public class NiBillboardNode : NiNode
    {
        public const int MODE_SHIFT = 5;
        public const ushort MODE_MASK = 0x0060;
    }
}
