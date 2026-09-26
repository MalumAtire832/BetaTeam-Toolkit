namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Playback settings shared by every 3ds animation class
    public sealed record FinAnimationCore(
        uint AnimationType,
        byte Unknown08,
        byte Unknown09,
        byte Unknown0A,
        bool SceneGraphUpdate,
        uint CycleType,
        uint Unknown10,
        uint Unknown14,
        uint Unknown18,
        float BeginKeyTime,
        uint Unknown20)
    {
        internal static FinAnimationCore Read(FinBlockReader reader)
        {
            return new FinAnimationCore(
                reader.ReadUInt32(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte() != 0,
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadSingle(),
                reader.ReadUInt32());
        }
    }
}
