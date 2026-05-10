using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    public class TgaImageConverter
    {
        private static readonly byte[] PNG_SIGNATURE = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public byte[] ToPng(TgaImage image)
        {
            var spec = image.Header.ImageSpec;
            var imageType = image.Header.ImageType;
            int width = spec.Width;
            int height = spec.Height;

            byte pngColorType;
            int bytesPerPixelIn;
            int bytesPerPixelOut;

            switch (imageType)
            {
                case TgaImageType.UncompressedColorMapped:
                case TgaImageType.RunLengthEncodingColorMapped:
                    return ToPngColorMapped(image, width, height);
                case TgaImageType.UncompressedTrueColor or TgaImageType.RunLengthEncodingTrueColor when spec.PixelDepth == 24:
                    pngColorType = 2;
                    bytesPerPixelIn = 3;
                    bytesPerPixelOut = 3;
                    break;
                case TgaImageType.UncompressedTrueColor or TgaImageType.RunLengthEncodingTrueColor when spec.PixelDepth == 32:
                    pngColorType = 6;
                    bytesPerPixelIn = 4;
                    bytesPerPixelOut = 4;
                    break;
                case TgaImageType.UncompressedGrayscale or TgaImageType.RunLengthEncodingGrayscale when spec.PixelDepth == 8:
                    pngColorType = 0;
                    bytesPerPixelIn = 1;
                    bytesPerPixelOut = 1;
                    break;
                default:
                    throw new NotSupportedException(
                        $"TGA image type {imageType} with pixel depth {spec.PixelDepth} is not supported.");
            }

            var topToBottom = (spec.ImageDescriptor & 0x20) != 0;
            var pixelData = image.Data.PixelData;

            var rawRowStride = 1 + width * bytesPerPixelOut;
            var rawRows = new byte[height * rawRowStride];

            for (var r = 0; r < height; r++)
            {
                var srcRow = topToBottom ? r : height - 1 - r;
                var srcBase = srcRow * width * bytesPerPixelIn;
                var dstBase = r * rawRowStride;

                rawRows[dstBase] = 0x00; // filter byte: None

                for (var p = 0; p < width; p++)
                {
                    var src = srcBase + p * bytesPerPixelIn;
                    var dst = dstBase + 1 + p * bytesPerPixelOut;

                    if (bytesPerPixelIn == 1)
                    {
                        rawRows[dst] = pixelData[src];
                    }
                    else
                    {
                        // TGA is BGR(A), PNG expects RGB(A): swap byte 0 (B) and byte 2 (R)
                        rawRows[dst]     = pixelData[src + 2]; // R
                        rawRows[dst + 1] = pixelData[src + 1]; // G
                        rawRows[dst + 2] = pixelData[src];     // B
                        if (bytesPerPixelOut == 4)
                            rawRows[dst + 3] = pixelData[src + 3]; // A
                    }
                }
            }

            return AssemblePng(width, height, pngColorType, rawRows);
        }

        private byte[] ToPngColorMapped(TgaImage image, int width, int height)
        {
            var colorMapSpec = image.Header.ColorMapSpec;
            var colorMap = image.Data.ColorMapData
                ?? throw new InvalidDataException("Color-mapped TGA has no colour map data.");

            int entrySize = colorMapSpec.EntrySize;
            var bytesPerEntry = entrySize <= 16 ? 2 : entrySize / 8;
            var hasAlpha = entrySize == 32;
            var pngColorType = hasAlpha ? (byte)6 : (byte)2;
            var bytesPerPixelOut = hasAlpha ? 4 : 3;

            var topToBottom = (image.Header.ImageSpec.ImageDescriptor & 0x20) != 0;
            var pixelData = image.Data.PixelData;
            int firstEntry = colorMapSpec.FirstEntryIndex;

            var rawRowStride = 1 + width * bytesPerPixelOut;
            var rawRows = new byte[height * rawRowStride];

            for (var r = 0; r < height; r++)
            {
                var srcRow = topToBottom ? r : height - 1 - r;
                var dstBase = r * rawRowStride;
                rawRows[dstBase] = 0x00;

                for (var p = 0; p < width; p++)
                {
                    var index = pixelData[srcRow * width + p] - firstEntry;
                    var mapOffset = index * bytesPerEntry;
                    var dst = dstBase + 1 + p * bytesPerPixelOut;

                    if (entrySize <= 16)
                    {
                        // 15/16-bit: xBBBBBGGGGGRRRRR stored little-endian
                        var entry = BinaryPrimitives.ReadUInt16LittleEndian(colorMap.AsSpan(mapOffset));
                        rawRows[dst]     = (byte)(((entry >> 10) & 0x1F) * 255 / 31); // R
                        rawRows[dst + 1] = (byte)(((entry >> 5)  & 0x1F) * 255 / 31); // G
                        rawRows[dst + 2] = (byte)((entry         & 0x1F) * 255 / 31); // B
                    }
                    else
                    {
                        // 24/32-bit: BGR(A) → RGB(A)
                        rawRows[dst]     = colorMap[mapOffset + 2]; // R
                        rawRows[dst + 1] = colorMap[mapOffset + 1]; // G
                        rawRows[dst + 2] = colorMap[mapOffset];     // B
                        if (hasAlpha)
                            rawRows[dst + 3] = colorMap[mapOffset + 3]; // A
                    }
                }
            }

            return AssemblePng(width, height, pngColorType, rawRows);
        }

        private static byte[] AssemblePng(int width, int height, byte pngColorType, byte[] rawRows)
        {
            byte[] idatData;
            using (var compressedMs = new MemoryStream())
            {
                using (var zlib = new ZLibStream(compressedMs, CompressionLevel.Optimal, leaveOpen: true))
                    zlib.Write(rawRows);
                idatData = compressedMs.ToArray();
            }

            var ihdrData = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(0), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdrData.AsSpan(4), (uint)height);
            ihdrData[8]  = 8; // bit depth
            ihdrData[9]  = pngColorType;
            ihdrData[10] = 0; // compression
            ihdrData[11] = 0; // filter
            ihdrData[12] = 0; // interlace

            using var png = new MemoryStream();
            png.Write(PNG_SIGNATURE);
            WriteChunk(png, "IHDR", ihdrData);
            WriteChunk(png, "IDAT", idatData);
            WriteChunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }

        public void Convert(TgaImage image, string outputPath)
            => File.WriteAllBytes(outputPath, ToPng(image));

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);

            Span<byte> lenBuf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(lenBuf, (uint)data.Length);
            stream.Write(lenBuf);
            stream.Write(typeBytes);
            stream.Write(data);

            Span<byte> crcBuf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBuf, Crc32(typeBytes, data));
            stream.Write(crcBuf);
        }

        private static uint Crc32(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            var crc = 0xFFFFFFFF;
            foreach (var by in a) crc = (crc >> 8) ^ CRC32_TABLE[(crc ^ by) & 0xFF];
            foreach (var by in b) crc = (crc >> 8) ^ CRC32_TABLE[(crc ^ by) & 0xFF];
            return crc ^ 0xFFFFFFFF;
        }

        private static readonly uint[] CRC32_TABLE = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var c = i;
                for (var k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
    }
}
