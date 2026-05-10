using Malumware.BetaTeam.Lib.IO.Dds;

namespace Malumware.BetaTeam.Lib.Tests.IO.Dds
{
    public class DdsAudioHeaderParserTests
    {
        [Fact]
        public void Parse_ReturnsHeader_WhenBufferIsValid()
        {
            // Arrange
            var bytes = BuildHeaderBytes(
                format: 1,          // PCM
                channels: 2,
                sampleRate: 44100,
                byteRate: 176400,
                blockAlign: 4,
                bitsPerSample: 16
            );
            var stream = new MemoryStream(bytes);
            using var parser = new DdsAudioHeaderParser(stream);

            // Act
            var header = parser.Parse();

            // Assert
            Assert.True(header.IsValid);
            Assert.Equal(DdsAudioFormat.Pcm, header.Format);
            Assert.Equal(2, header.Channels);
            Assert.Equal(44100u, header.SampleRate);
            Assert.Equal(176400u, header.ByteRate);
            Assert.Equal(4, header.BlockAlign);
            Assert.Equal(16, header.BitsPerSample);
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenFormatIsUnsupported()
        {
            // Arrange
            var bytes = BuildHeaderBytes(
                format: 0,          // unknown
                channels: 1,
                sampleRate: 22050,
                byteRate: 44100,
                blockAlign: 2,
                bitsPerSample: 16
            );
            var stream = new MemoryStream(bytes);
            using var parser = new DdsAudioHeaderParser(stream);

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        private static byte[] BuildHeaderBytes(
            ushort format, ushort channels, uint sampleRate,
            uint byteRate, ushort blockAlign, ushort bitsPerSample)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(format);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            return ms.ToArray();
        }
    }
}
