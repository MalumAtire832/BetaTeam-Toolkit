using System.IO.MemoryMappedFiles;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    public class TgaImageReader
    {
        public TgaImage Read(string filePath)
        {
            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read
            );
            return Read(() => CreateStream(mmf));
        }

        internal static TgaImage Read(Func<Stream> streamFactory)
        {
            using var headerParser = new TgaImageHeaderParser(streamFactory());
            var header = headerParser.Parse();

            using var dataParser = new TgaImageDataParser(streamFactory(), header);
            var data = dataParser.Parse();

            return new TgaImage(header, data);
        }

        private static MemoryMappedViewStream CreateStream(MemoryMappedFile mmf)
        {
            return mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        }
    }
}
