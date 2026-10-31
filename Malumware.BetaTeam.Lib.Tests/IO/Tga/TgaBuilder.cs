namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    // Writes TGA bytes for tests: an 18-byte header, then whatever pixel bytes or packets the test adds
    internal sealed class TgaBuilder
    {
        // Descriptor bit 5: rows are stored top to bottom
        public const byte TOP_TO_BOTTOM = 0x20;

        private readonly MemoryStream _stream = new();
        private readonly BinaryWriter _writer;

        public TgaBuilder()
        {
            _writer = new BinaryWriter(_stream);
        }

        public TgaBuilder Header(byte imageType, ushort width, ushort height, byte pixelDepth, byte descriptor = 0,
            byte idLength = 0, byte colorMapType = 0)
        {
            _writer.Write(idLength);
            _writer.Write(colorMapType);
            _writer.Write(imageType);
            _writer.Write((ushort)0); // first colour map entry
            _writer.Write((ushort)0); // colour map length
            _writer.Write((byte)0);   // colour map entry size
            _writer.Write((ushort)0); // x origin
            _writer.Write((ushort)0); // y origin
            _writer.Write(width);
            _writer.Write(height);
            _writer.Write(pixelDepth);
            _writer.Write(descriptor);
            _writer.Write(new byte[idLength]);
            return this;
        }

        public TgaBuilder Bytes(params byte[] values)
        {
            _writer.Write(values);
            return this;
        }

        // A run-length packet: count repeats of one pixel
        public TgaBuilder Run(int count, params byte[] pixel)
        {
            _writer.Write((byte)(0x80 | (count - 1)));
            _writer.Write(pixel);
            return this;
        }

        // A raw packet: the pixels' bytes as they are
        public TgaBuilder Raw(int count, params byte[] pixels)
        {
            _writer.Write((byte)(count - 1));
            _writer.Write(pixels);
            return this;
        }

        public byte[] ToArray()
        {
            _writer.Flush();
            return _stream.ToArray();
        }
    }
}
