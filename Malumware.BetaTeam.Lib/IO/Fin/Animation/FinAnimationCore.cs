namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Playback settings shared by every 3ds animation class. The names follow the engine's getters (GetAnimType,
    // GetCycleType, GetFrequency, GetPhase, GetBeginKeyTime, GetEndKeyTime, ...). Times are in the exporter's units:
    // 3ds Max ticks, 4800 per second (inferred from values like 3200 and 12800).
    public sealed record FinAnimationCore(
        uint AnimationType,
        byte Unknown08,
        byte Unknown09,
        byte Unknown0A,
        bool SceneGraphUpdate,
        uint CycleType,
        float DefaultDisplayTime,
        float Frequency,
        float Phase,
        float BeginKeyTime,
        float EndKeyTime)
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
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }
    }
}
