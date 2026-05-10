namespace Malumware.BetaTeam.Lib.IO.Tga
{
    /// <summary>Parsed TGA image comprising the file header and all associated data sections</summary>
    public class TgaImage
    {
        /// <summary>Name of the source file without directory or extension</summary>
        public string FileName { get; }

        /// <summary>Parsed 18-byte file header</summary>
        public TgaImageHeader Header { get; }

        /// <summary>Raw data sections following the header: image ID, colour map, and pixel data</summary>
        public TgaImageData Data { get; }

        public TgaImage(string fileName, TgaImageHeader header, TgaImageData data)
        {
            FileName = fileName;
            Header = header;
            Data = data;
        }
    }
}
