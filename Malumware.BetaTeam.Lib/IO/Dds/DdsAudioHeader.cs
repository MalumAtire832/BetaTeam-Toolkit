namespace Malumware.BetaTeam.Lib.IO.Dds
{
    public record DdsAudioHeader
    {
        /// <summary>Total size in bytes of the DDS audio header, a Windows <c>WAVEFORMATEX</c> structure.</summary>
        public const int SIZE = 18;

        public DdsAudioFormat Format { get; }
        public ushort Channels { get; }
        public uint SampleRate { get; }
        public uint ByteRate { get; }
        public ushort BlockAlign { get; }
        public ushort BitsPerSample { get; }

        /// <summary>Number of extra format bytes following the header (<c>cbSize</c>). Always 0 for PCM.</summary>
        public ushort ExtraSize { get; }

        public bool IsValid => Enum.IsDefined(Format);

        public DdsAudioHeader(
            DdsAudioFormat format,
            ushort channels,
            uint sampleRate,
            uint byteRate,
            ushort blockAlign,
            ushort bitsPerSample,
            ushort extraSize = 0)
        {
            Format = format;
            Channels = channels;
            SampleRate = sampleRate;
            ByteRate = byteRate;
            BlockAlign = blockAlign;
            BitsPerSample = bitsPerSample;
            ExtraSize = extraSize;
        }
    }
}
