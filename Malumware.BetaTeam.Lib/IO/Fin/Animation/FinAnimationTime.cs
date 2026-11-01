namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Times the way the engine works with them: skills are in seconds, keys and animation settings in 3ds Max ticks
    public static class FinAnimationTime
    {
        public const float TICKS_PER_SECOND = 4800;

        // The engine rounds a skill's time to whole ticks before evaluating keys (DDSkill::Update: time × 4800 + 0.5,
        // truncated)
        public static float ToTicks(float seconds)
        {
            return MathF.Truncate(seconds * TICKS_PER_SECOND + 0.5f);
        }

        // Ni3dsAnimationCore::ComputeScaledTime: frequency and phase first, then the cycle type, then the result is
        // kept inside [BeginKeyTime, EndKeyTime]. Cycle 0 loops, 1 runs back and forth, anything else only clamps.
        public static float ScaledTime(FinAnimationCore core, float ticks)
        {
            var begin = core.BeginKeyTime;
            var end = core.EndKeyTime;
            var range = end - begin;
            var time = ticks * core.Frequency + core.Phase;

            if (range == 0)
            {
                return begin;
            }
            if (core.CycleType == 0)
            {
                var offset = (time - begin) % range;
                time = offset >= 0 ? begin + offset : end + offset;
            }
            else if (core.CycleType == 1)
            {
                var cycles = MathF.Floor((time - begin) / range);
                var offset = (time - begin) - cycles * range;
                time = (long)cycles % 2 == 0 ? begin + offset : end - offset;
            }

            return Math.Clamp(time, begin, end);
        }
    }
}
