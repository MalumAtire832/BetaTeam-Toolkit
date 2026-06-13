using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Dds
{
    public class DdsAudioHeaderParser : BinaryParser<DdsAudioHeader>
    {
        public DdsAudioHeaderParser(Stream stream)
            : base(stream) { }

        public override DdsAudioHeader Parse()
        {
            var format = (DdsAudioFormat)Reader.ReadUInt16();
            var channels = Reader.ReadUInt16();
            var sampleRate = Reader.ReadUInt32();
            var byteRate = Reader.ReadUInt32();
            var blockAlign = Reader.ReadUInt16();
            var bitsPerSample = Reader.ReadUInt16();

            var header = new DdsAudioHeader(format, channels, sampleRate, byteRate, blockAlign, bitsPerSample);

            if (!header.IsValid)
            {
                throw new InvalidDataException(
                    $"Unsupported DDS audio format: {(ushort)format}"
                );
            }

            return header;
        }
    }
}
