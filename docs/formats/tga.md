# TGA textures

Every texture in `Map.pac` (678 files) and `LMap.pac` (582 lightmaps) is a Truevision TGA image, the common format of
paint and 3D tools of the time. The game's fonts in `Etc.pac` use TGA too, for their glyph pages. TGA is a public,
standard format; this page only covers the subset the game uses and how the game's engine reads it, which differs from
the standard in two places that change what you see.

## What ships

| Image type | Meaning                     | Bits per pixel | Files |
|------------|-----------------------------|----------------|-------|
| 2          | True colour                 | 24             | 947   |
| 2          | True colour                 | 32             | 292   |
| 3          | Greyscale                   | 8              | 5     |
| 10         | True colour, run-length     | 32             | 16    |

Every image has a power-of-two width and height. None has a colour map. Most rows are stored bottom to top (the TGA
default); 583 files say top to bottom in the header's descriptor byte, mostly lightmaps.

677 files end with a TGA 2.0 footer (two offsets and the signature `TRUEVISION-XFILE.`), and ten of them (the
`PULSE_C` animation frames) also carry a 495-byte extension area with the tool's author fields. The engine reads
neither; the pixel data always ends where the footer says the extra areas begin.

## How the engine reads it

The engine has its own TGA loader rather than a library. It accepts image types 1, 2, 3, 9, 10 and 11 at 8, 15, 16,
24 and 32 bits per pixel, and decides the pixel format from the bit depth alone:

- **A 32-bit image always has alpha.** The fourth byte of each pixel becomes the alpha, even when the header's
  descriptor says there are no alpha bits. The standard says to ignore the fourth byte then. Six shipped files
  (`LBOLTS001`–`006`, lightning frames) depend on this: their headers claim no alpha, but their fourth bytes run
  from 0 to 255, with half of each frame fully transparent.
- **Every other depth is opaque.** A 24-bit image gets alpha 255, and so does an 8-bit greyscale image, even though
  the five shipped ones claim 8 alpha bits in their descriptors. Grey becomes equal red, green and blue: these are
  grey pictures, not alpha masks.
- Rows go bottom to top unless descriptor bit 5 is set. Bit 4 (right to left) is ignored.
- A run-length packet may continue from the end of one row into the next, as the standard allows.

The toolkit's `TgaReader` follows these rules for the types the game ships (2, 3, 10 and 11) and rejects the rest. Its
output is always RGBA with rows top to bottom; `TgaImage.HasAlpha` says whether the alpha came from the file.
