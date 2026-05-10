using Malumware.BetaTeam.Lib.IO.Dds;

namespace Malumware.BetaTeam.Lib.Tests.IO.Dds
{
    public class DdsAudioHeaderTests
    {
        [Fact]
        public void IsValid_ReturnsTrue_WhenFormatIsPcm()
        {
            // Arrange
            var header = new DdsAudioHeader(DdsAudioFormat.Pcm, channels: 2, sampleRate: 44100, byteRate: 176400, blockAlign: 4, bitsPerSample: 16);

            // Act & Assert
            Assert.True(header.IsValid);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenFormatIsUnknown()
        {
            // Arrange
            var header = new DdsAudioHeader((DdsAudioFormat)0, channels: 2, sampleRate: 44100, byteRate: 176400, blockAlign: 4, bitsPerSample: 16);

            // Act & Assert
            Assert.False(header.IsValid);
        }
    }
}
