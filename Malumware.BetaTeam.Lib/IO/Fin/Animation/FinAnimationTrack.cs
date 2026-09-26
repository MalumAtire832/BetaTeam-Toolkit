namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Keyframes for one animated node, kept in the shared data so every instance of the actor uses the same copy
    public sealed record FinAnimationTrack(
        FinKeyGroup<FinRotKey> RotationKeys,
        FinKeyGroup<FinPosKey> PositionKeys,
        FinKeyGroup<FinFloatKey> ScaleKeys,
        FinVisKey[] VisibilityKeys)
    {
        internal static FinAnimationTrack Read(FinBlockReader reader)
        {
            var rotation = FinKeyReader.ReadOptionalRotKeys(reader);
            var position = FinKeyReader.ReadOptionalPosKeys(reader);
            var scale = FinKeyReader.ReadOptionalFloatKeys(reader);
            return new FinAnimationTrack(rotation, position, scale, FinKeyReader.ReadVisKeys(reader));
        }
    }
}
