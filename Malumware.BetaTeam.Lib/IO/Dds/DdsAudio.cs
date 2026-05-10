namespace Malumware.BetaTeam.Lib.IO.Dds
{
    public class DdsAudio
    {
        public string Name { get; }
        public DdsAudioHeader Header { get; }
        public byte[] Data { get; }

        public DdsAudio(string name, DdsAudioHeader header, byte[] data)
        {
            Name = name;
            Header = header;
            Data = data;
        }
    }
}
