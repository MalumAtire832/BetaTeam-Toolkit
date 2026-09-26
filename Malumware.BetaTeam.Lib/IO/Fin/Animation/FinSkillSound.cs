namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // A sound a skill plays. The engine loads FileName from its "AudioDir" setting and passes the values to its sound
    // source: Gain to Sound_SetGain, DistanceModelScale to SetDistanceModelScale, Min/MaxDistance and DistanceFlag to
    // SetMinMaxDistance. Delay holds playback back until that time into the skill. The flag names are inferred.
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
