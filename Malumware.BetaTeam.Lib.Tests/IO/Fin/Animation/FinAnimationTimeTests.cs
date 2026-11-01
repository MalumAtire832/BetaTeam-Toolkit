using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation
{
    public class FinAnimationTimeTests
    {
        private const float TOLERANCE = 1e-3f;

        private static FinAnimationCore Core(uint cycle, float begin, float end, float frequency = 1, float phase = 0)
        {
            return new FinAnimationCore(0, 0, 0, 0, false, cycle, 0, frequency, phase, begin, end);
        }

        [Fact]
        public void ToTicks_RoundsHalfUp()
        {
            Assert.Equal(16000, FinAnimationTime.ToTicks(3.3333333f));
            Assert.Equal(0, FinAnimationTime.ToTicks(0.0001f));
            Assert.Equal(1, FinAnimationTime.ToTicks(0.0002f));
        }

        [Theory]
        [InlineData(50, 100)]
        [InlineData(150, 150)]
        [InlineData(250, 200)]
        public void ScaledTime_Clamp_ClampsToBeginAndEnd(float ticks, float expected)
        {
            Assert.Equal(expected, FinAnimationTime.ScaledTime(Core(2, 100, 200), ticks), TOLERANCE);
        }

        [Theory]
        [InlineData(250, 150)]
        [InlineData(50, 150)]
        public void ScaledTime_Loop_WrapsIntoRange(float ticks, float expected)
        {
            Assert.Equal(expected, FinAnimationTime.ScaledTime(Core(0, 100, 200), ticks), TOLERANCE);
        }

        [Theory]
        [InlineData(150, 50)]
        [InlineData(230, 30)]
        [InlineData(40, 40)]
        public void ScaledTime_PingPong_RunsBackOnOddCycles(float ticks, float expected)
        {
            Assert.Equal(expected, FinAnimationTime.ScaledTime(Core(1, 0, 100), ticks), TOLERANCE);
        }

        [Fact]
        public void ScaledTime_UsesFrequencyAndPhase()
        {
            Assert.Equal(210, FinAnimationTime.ScaledTime(Core(2, 0, 1000, 2, 10), 100), TOLERANCE);
        }

        [Fact]
        public void ScaledTime_EmptyRange_IsBegin()
        {
            Assert.Equal(40, FinAnimationTime.ScaledTime(Core(0, 40, 40), 1234), TOLERANCE);
        }
    }
}
