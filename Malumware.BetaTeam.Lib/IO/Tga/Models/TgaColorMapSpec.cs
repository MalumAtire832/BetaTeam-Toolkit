namespace Malumware.BetaTeam.Lib.IO.Tga.Models
{
    /// <summary>Five-byte color map specification embedded in the TGA header</summary>
    public record TgaColorMapSpec
    {
        /// <summary>Index of the first color map entry referenced by the image data</summary>
        public ushort FirstEntryIndex { get; }

        /// <summary>Total number of color map entries included in the file</summary>
        public ushort ColorMapLength { get; }

        /// <summary>Number of bits per color map entry (15, 16, 24, or 32)</summary>
        public byte EntrySize { get; }

        public TgaColorMapSpec(ushort firstEntryIndex, ushort colorMapLength, byte entrySize)
        {
            FirstEntryIndex = firstEntryIndex;
            ColorMapLength = colorMapLength;
            EntrySize = entrySize;
        }
    }
}