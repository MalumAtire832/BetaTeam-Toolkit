namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // A sound a skill plays. The meaning of the three flags is inferred from how the game passes them on.
    public sealed record FinSkillSound(
        string? FileName,
        string? NodeName,
        bool Loop,
        bool SourceType,
        bool DistanceFlag,
        float Delay,
        float Gain,
        float DistanceModelScale,
        float MaxDistance,
        float MinDistance)
    {
        internal static FinSkillSound Read(FinBlockReader reader)
        {
            return new FinSkillSound(
                reader.ReadCString(),
                reader.ReadCString(),
                reader.ReadByte() != 0,
                reader.ReadByte() != 0,
                reader.ReadByte() != 0,
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }
    }
}
