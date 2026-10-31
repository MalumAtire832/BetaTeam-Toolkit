using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.IO.Tga;
using Malumware.BetaTeam.Lib.Tests.GameData;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaGameDataTests
    {
        private static readonly string[] TEXTURE_ARCHIVES = ["Map", "LMap"];
        // A TGA 2.0 footer: extension area offset, developer area offset, signature
        private const int FOOTER_SIZE = 26;
        private static readonly byte[] SIGNATURE = "TRUEVISION-XFILE.\0"u8.ToArray();

        [GameDataFact]
        public void Read_DecodesEveryShippedTexture_WithNoLeftoverBytes()
        {
            // Arrange
            var reader = new PacArchiveReader();
            var entries = GameDataPaths
                .Archives()
                .Where(e => TEXTURE_ARCHIVES.Contains(Path.GetFileNameWithoutExtension(e), StringComparer.OrdinalIgnoreCase))
                .Select(e => reader.Read(e))
                .SelectMany(e => e.Entries)
                .Where(e => e.Key.FileName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var leftovers = new List<string>();

            // Act
            foreach (var (entry, data) in entries)
            {
                TgaReader.Read(data, out var consumed);
                var end = ImageDataEnd(data);
                if (consumed != end)
                {
                    leftovers.Add($"{entry.FileName}: {end - consumed} bytes");
                }
            }

            // Assert
            Assert.Equal(1260, entries.Count);
            Assert.Empty(leftovers);
        }

        // Where the pixel data should end: at the extension area or the footer when the file has a TGA 2.0 footer
        // (most shipped files do; the engine reads neither), otherwise at the end of the file
        private static int ImageDataEnd(byte[] data)
        {
            if (data.Length < FOOTER_SIZE || !data.AsSpan(data.Length - SIGNATURE.Length).SequenceEqual(SIGNATURE))
            {
                return data.Length;
            }
            var extensionOffset = BitConverter.ToInt32(data, data.Length - FOOTER_SIZE);
            var developerOffset = BitConverter.ToInt32(data, data.Length - FOOTER_SIZE + 4);
            var areas = new[] { extensionOffset, developerOffset }
                .Where(e => e > 0)
                .ToList();

            return areas.Count > 0 ? areas.Min() : data.Length - FOOTER_SIZE;
        }
    }
}
