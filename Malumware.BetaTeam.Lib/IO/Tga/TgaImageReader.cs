using System.IO.MemoryMappedFiles;
using Malumware.BetaTeam.Lib.IO.Tga.Models;
using Malumware.BetaTeam.Lib.IO.Tga.Parsing;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    public class TgaImageReader
    {
        public TgaImage Read(string filePath)
        {
            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read
            );
            return Read(Path.GetFileNameWithoutExtension(filePath), () => CreateStream(mmf));
        }

        internal static TgaImage Read(string fileName, Func<Stream> streamFactory)
        {
            using var headerParser = new TgaImageHeaderParser(streamFactory());
            var header = headerParser.Parse();

            using var dataParser = new TgaImageDataParser(streamFactory(), header);
            var data = dataParser.Parse();

            return new TgaImage(fileName, header, data);
        }

        private static MemoryMappedViewStream CreateStream(MemoryMappedFile mmf)
        {
            return mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        }
    }
}
