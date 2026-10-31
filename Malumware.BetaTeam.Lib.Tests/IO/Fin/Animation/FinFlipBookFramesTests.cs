using Malumware.BetaTeam.Lib.IO.Fin.Animation;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using static Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation.AnimatedFin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation
{
    public class FinFlipBookFramesTests
    {
        private static NiFlipTextures FlipBook(uint outOfBound, float startTime = 0, float cycleTime = 400)
        {
            var builder = Start().FlipBook(0x10, 0x11, 0, outOfBound, startTime, cycleTime, 0x20, 0x21)
                .Image(0x20, "a.tga")
                .Image(0x21, "b.tga");

            return Read(builder).Objects.OfType<NiFlipTextures>().Single();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(150, 1)]
        [InlineData(399, 3)]
        [InlineData(400, 0)]
        [InlineData(-50, 3)]
        public void Loop_WrapsByCycle(float ticks, int frame)
        {
            Assert.Equal(frame, FinFlipBookFrames.FrameAt(FlipBook(0), 4, ticks));
        }

        [Theory]
        [InlineData(100, 1)]
        [InlineData(500, 3)]
        [InlineData(799, 0)]
        [InlineData(-100, 1)]
        public void PingPong_RunsBackOnOddCycles(float ticks, int frame)
        {
            Assert.Equal(frame, FinFlipBookFrames.FrameAt(FlipBook(1), 4, ticks));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, 0)]
        [InlineData(399, 3)]
        [InlineData(500, 3)]
        public void Clamp_HoldsFirstAndLast(float ticks, int frame)
        {
            Assert.Equal(frame, FinFlipBookFrames.FrameAt(FlipBook(2), 4, ticks));
        }

        [Fact]
        public void FrameAt_CountsFromTheStartTime()
        {
            Assert.Equal(1, FinFlipBookFrames.FrameAt(FlipBook(2, startTime: 1000), 4, 1150));
        }

        [Fact]
        public void FrameAt_UnknownOutOfBound_Throws()
        {
            Assert.Throws<NotSupportedException>(() => FinFlipBookFrames.FrameAt(FlipBook(3), 4, 0));
        }

        [Fact]
        public void Track_ListsOnlyChanges()
        {
            var track = FinFlipBookFrames.Track(FlipBook(0), 4, 0, 800);

            Assert.Equal(
                [(0f, 0), (100 / 4800f, 1), (200 / 4800f, 2), (300 / 4800f, 3), (400 / 4800f, 0), (500 / 4800f, 1),
                    (600 / 4800f, 2), (700 / 4800f, 3), (800 / 4800f, 0)],
                track);
        }

        [Fact]
        public void Track_TimesAreFromTheSkillStart()
        {
            var track = FinFlipBookFrames.Track(FlipBook(0), 4, 150, 250);

            Assert.Equal([(0f, 1), (50 / 4800f, 2)], track);
        }

        [Fact]
        public void Track_ZeroLength_HasOneEntry()
        {
            Assert.Equal([(0f, 2)], FinFlipBookFrames.Track(FlipBook(2), 4, 200, 200));
        }
    }
}
