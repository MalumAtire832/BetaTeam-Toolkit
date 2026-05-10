namespace Malumware.BetaTeam.Lib.IO.Dds
{
    public record DdsAudioHeader
    {
        /// <summary>Total size in bytes of the DDS audio header.</summary>
        public const int SIZE = 16;

        public DdsAudioFormat Format { get; }
        public ushort Channels { get; }
        public uint SampleRate { get; }
        public uint ByteRate { get; }
        public ushort BlockAlign { get; }
        public ushort BitsPerSample { get; }

        public bool IsValid => Enum.IsDefined(Format);

        public DdsAudioHeader(
            DdsAudioFormat format,
            ushort channels,
            uint sampleRate,
            uint byteRate,
            ushort blockAlign,
            ushort bitsPerSample)
        {
            Format = format;
            Channels = channels;
            SampleRate = sampleRate;
            ByteRate = byteRate;
            BlockAlign = blockAlign;
            BitsPerSample = bitsPerSample;
        }
    }
}
