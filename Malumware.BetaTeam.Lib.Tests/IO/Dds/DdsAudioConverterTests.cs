using System.Text;
using Malumware.BetaTeam.Lib.IO.Dds;

namespace Malumware.BetaTeam.Lib.Tests.IO.Dds
{
    public class DdsAudioConverterTests
    {
        [Fact]
        public void ToWav_ReturnsRiffWave_WithCorrectStructure()
        {
            // Arrange
            var sampleData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var audio = BuildAudio(sampleData);
            var converter = new DdsAudioConverter();

            // Act
            var wav = converter.ToWav(audio);

            // Assert
            using var reader = new BinaryReader(new MemoryStream(wav), Encoding.ASCII);

            Assert.Equal("RIFF", new string(reader.ReadChars(4)));
            Assert.Equal((uint)(36 + sampleData.Length), reader.ReadUInt32());
            Assert.Equal("WAVE", new string(reader.ReadChars(4)));

            Assert.Equal("fmt ", new string(reader.ReadChars(4)));
            Assert.Equal(16u, reader.ReadUInt32());
            Assert.Equal((ushort)DdsAudioFormat.Pcm, reader.ReadUInt16());
            Assert.Equal((ushort)2, reader.ReadUInt16());   // channels
            Assert.Equal(44100u, reader.ReadUInt32());      // sample rate
            Assert.Equal(176400u, reader.ReadUInt32());     // byte rate
            Assert.Equal((ushort)4, reader.ReadUInt16());   // block align
            Assert.Equal((ushort)16, reader.ReadUInt16());  // bits per sample

            Assert.Equal("data", new string(reader.ReadChars(4)));
            Assert.Equal((uint)sampleData.Length, reader.ReadUInt32());
            Assert.Equal(sampleData, reader.ReadBytes(sampleData.Length));
        }

        [Fact]
        public void ToWav_TotalSize_Is44PlusSampleDataLength()
        {
            // Arrange
            var sampleData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
            var audio = BuildAudio(sampleData);
            var converter = new DdsAudioConverter();

            // Act
            var wav = converter.ToWav(audio);

            // Assert
            Assert.Equal(44 + sampleData.Length, wav.Length);
        }

        private static DdsAudio BuildAudio(byte[] sampleData)
        {
            var header = new DdsAudioHeader(
                DdsAudioFormat.Pcm,
                channels: 2,
                sampleRate: 44100,
                byteRate: 176400,
                blockAlign: 4,
                bitsPerSample: 16
            );
            return new DdsAudio("TEST", header, sampleData);
        }
    }
}
