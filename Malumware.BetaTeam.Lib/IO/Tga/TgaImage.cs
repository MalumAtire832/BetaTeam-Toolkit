namespace Malumware.BetaTeam.Lib.IO.Tga
{
    /// <summary>Parsed TGA image comprising the file header and all associated data sections</summary>
    public class TgaImage
    {
        /// <summary>Parsed 18-byte file header</summary>
        public TgaImageHeader Header { get; }

        /// <summary>Raw data sections following the header: image ID, colour map, and pixel data</summary>
        public TgaImageData Data { get; }

        public TgaImage(TgaImageHeader header, TgaImageData data)
        {
            Header = header;
            Data = data;
        }
    }
}
