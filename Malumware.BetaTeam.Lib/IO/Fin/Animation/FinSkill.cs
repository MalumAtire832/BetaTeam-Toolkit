namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // A named clip on the actor's animation timeline (such as "neutral"), from StartTime to EndTime. The index lists
    // say which of the actor's animated parts the clip drives.
    public sealed record FinSkill(
        string? Name,
        float StartTime,
        float EndTime,
        FinSkillSound? Sound,
        ushort[] AnimationNodes,
        ushort[] Actions,
        ushort[] OtherAnimations)
    {
        internal static FinSkill Read(FinBlockReader reader)
        {
            var name = reader.ReadCString();
            var startTime = reader.ReadSingle();
            var endTime = reader.ReadSingle();
            var sound = reader.ReadByte() != 0 ? FinSkillSound.Read(reader) : null;
            return new FinSkill(name, startTime, endTime, sound, ReadIndices(reader), ReadIndices(reader), ReadIndices(reader));
        }

        private static ushort[] ReadIndices(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(sizeof(ushort));
            return reader.ReadArray(count, r => r.ReadUInt16());
        }
    }
}
