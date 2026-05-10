namespace Malumware.BetaTeam.Lib.IO.Dds
{
    public class DdsAudioReader
    {
        public DdsAudio Read(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var bytes = File.ReadAllBytes(filePath);
            return Read(name, bytes);
        }

        internal static DdsAudio Read(string name, byte[] bytes)
        {
            using var headerStream = new MemoryStream(bytes, 0, DdsAudioHeader.SIZE);
            using var parser = new DdsAudioHeaderParser(headerStream);
            var header = parser.Parse();
            var data = bytes[DdsAudioHeader.SIZE..];
            return new DdsAudio(name, header, data);
        }
    }
}
