using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Which image a flip-book shows at a time in ticks (NiMain.dll NiFlipTextures::Act). The rate only matters when
    // the engine sets up the cycle time, so it isn't used here.
    public static class FinFlipBookFrames
    {
        private const uint LOOP = 0;
        private const uint PING_PONG = 1;
        private const uint CLAMP = 2;

        public static int FrameAt(NiFlipTextures flip, int frameCount, float ticks)
        {
            var cycle = flip.CycleTime;
            var elapsed = ticks - flip.StartTime;
            int frame;
            switch (flip.OutOfBound)
            {
                case LOOP:
                {
                    var offset = elapsed >= 0 ? elapsed % cycle : cycle - (-elapsed % cycle);
                    frame = Truncate(frameCount * offset / cycle);
                    break;
                }
                case PING_PONG:
                {
                    var distance = MathF.Abs(elapsed);
                    var cycles = Truncate(distance / cycle);
                    var offset = distance % cycle;
                    frame = cycles % 2 == 0
                        ? Truncate(frameCount * offset / cycle)
                        : Truncate((cycle - offset) * frameCount / cycle);
                    break;
                }
                case CLAMP:
                    frame = ticks <= flip.StartTime ? 0
                        : ticks < flip.StartTime + cycle ? Truncate(frameCount * elapsed / cycle)
                        : frameCount - 1;
                    break;
                default:
                    throw new NotSupportedException($"Flip-book out-of-bound mode {flip.OutOfBound} isn't supported");
            }

            return Math.Min(frame, frameCount - 1);
        }

        // The frames a skill shows: one entry at its start and one wherever the frame changes, found tick by tick.
        // Times are seconds from the skill's start.
        public static IReadOnlyList<(float Seconds, int Frame)> Track(NiFlipTextures flip, int frameCount,
            float startTicks, float endTicks)
        {
            var result = new List<(float Seconds, int Frame)> { (0, FrameAt(flip, frameCount, startTicks)) };
            // Counted in whole ticks from the start, because a float tick stops growing by one beyond 2^24
            var length = (long)Math.Max(0, endTicks - startTicks);
            for (long offset = 1; offset <= length; offset++)
            {
                var frame = FrameAt(flip, frameCount, startTicks + offset);
                if (frame != result[^1].Frame)
                {
                    result.Add((offset / FinAnimationTime.TICKS_PER_SECOND, frame));
                }
            }

            return result;
        }

        // The engine's float-to-int conversion truncates towards zero
        private static int Truncate(float value)
        {
            return (int)MathF.Truncate(value);
        }
    }
}
