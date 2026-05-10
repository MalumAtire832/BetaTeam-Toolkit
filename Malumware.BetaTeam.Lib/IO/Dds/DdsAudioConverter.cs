using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Dds
{
    public class DdsAudioConverter
    {
        public byte[] ToWav(DdsAudio audio)
        {
            var header = audio.Header;
            using var ms = new MemoryStream(44 + audio.Data.Length);
            using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            writer.Write("RIFF"u8.ToArray());
            writer.Write((uint)(36 + audio.Data.Length));
            writer.Write("WAVE"u8.ToArray());

            writer.Write("fmt "u8.ToArray()); // trailing space is intentional — chunk ID must be 4 bytes
            writer.Write(16u); // PCM fmt chunk is always 16 bytes; non-PCM formats require extra params
            writer.Write((ushort)header.Format);
            writer.Write(header.Channels);
            writer.Write(header.SampleRate);
            writer.Write(header.ByteRate);
            writer.Write(header.BlockAlign);
            writer.Write(header.BitsPerSample);

            writer.Write("data"u8.ToArray());
            writer.Write((uint)audio.Data.Length);
            writer.Write(audio.Data);

            return ms.ToArray();
        }

        public void Convert(DdsAudio audio, string outputPath)
        {
            File.WriteAllBytes(outputPath, ToWav(audio));
        }
    }
}
