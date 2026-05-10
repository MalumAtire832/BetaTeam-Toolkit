using Malumware.BetaTeam.Lib.IO.Dds;

namespace Malumware.BetaTeam.Lib.Tests.IO.Dds
{
    public class DdsAudioReaderTests
    {
        [Fact]
        public void Read_ReturnsAudio_WithCorrectNameAndHeader()
        {
            // Arrange
            var sampleData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var bytes = BuildDdsBytes(format: 1, sampleData: sampleData);

            // Act
            var audio = DdsAudioReader.Read("THEME", bytes);

            // Assert
            Assert.Equal("THEME", audio.Name);
            Assert.Equal(DdsAudioFormat.Pcm, audio.Header.Format);
            Assert.Equal(2, audio.Header.Channels);
            Assert.Equal(44100u, audio.Header.SampleRate);
            Assert.Equal(176400u, audio.Header.ByteRate);
            Assert.Equal(4, audio.Header.BlockAlign);
            Assert.Equal(16, audio.Header.BitsPerSample);
            Assert.Equal(sampleData, audio.Data);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenFormatIsUnsupported()
        {
            // Arrange
            var bytes = BuildDdsBytes(format: 0, sampleData: []);

            // Act
            var act = () => DdsAudioReader.Read("BAD", bytes);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        private static byte[] BuildDdsBytes(ushort format, byte[] sampleData)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(format);       // format
            writer.Write((ushort)2);    // channels
            writer.Write(44100u);       // sample rate
            writer.Write(176400u);      // byte rate
            writer.Write((ushort)4);    // block align
            writer.Write((ushort)16);   // bits per sample
            writer.Write(sampleData);
            return ms.ToArray();
        }
    }
}
