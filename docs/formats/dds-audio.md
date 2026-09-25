# DDS Audio

All of the game's audio (music, ambience, sound effects and voice-over) is stored in `.DDS` files. Despite the
extension, these have nothing to do with the DirectDraw Surface texture format that also uses `.dds` (introduced in
DirectX 7, around the time this game was made). They are raw, uncompressed PCM audio with a tiny header in front.

The name is probably short for "Digital Domain Sound", matching the `DD` prefix the original developers used elsewhere, but
nothing in the game confirms that.

## Layout

```
┌──────────────────────────┐ 0x00
│ WAVEFORMATEX (18 bytes)  │
├──────────────────────────┤ 0x12
│ PCM sample data          │  until end of file
└──────────────────────────┘
```

| Offset | Type | Name           | Description                                        |
|--------|------|----------------|----------------------------------------------------|
| 0x00   | u16  | FormatTag      | Always `1` (PCM)                                   |
| 0x02   | u16  | Channels       | 1 (mono) or 2 (stereo)                             |
| 0x04   | u32  | SampleRate     | Samples per second, e.g. 22050                     |
| 0x08   | u32  | ByteRate       | `SampleRate × BlockAlign`                          |
| 0x0C   | u16  | BlockAlign     | Bytes per sample frame: `Channels × Bits / 8`      |
| 0x0E   | u16  | BitsPerSample  | Always 16                                          |
| 0x10   | u16  | ExtraSize      | Always 0 (`cbSize`, see below)                     |

The sample data length isn't stored anywhere. It runs from offset `0x12` to the end of the file.
Samples are signed 16-bit little-endian, interleaved left/right for stereo.

## Why this header

The header is a Windows `WAVEFORMATEX` structure written straight to disk. That's the structure DirectSound (and
Aureal's A3D, the 3D audio API the game's sound module is built on) takes to create a sound buffer. The game reads
exactly 18 bytes, hands them to the audio API as-is and streams the rest of the file into the buffer. There's no
parsing and no conversion step, which kept loading fast and simple on late-90s hardware.

The last field, `ExtraSize` (`cbSize` in Windows terms), gives the number of extra format bytes that follow. Compressed
formats like ADPCM use it for codec parameters. For plain PCM it's always 0, but it's still part of the
structure and still stored in the file.

> [!NOTE]
> The first 16 bytes happen to match the `fmt ` chunk of a WAV file. It's tempting to treat the header as 16 bytes
> and the audio as starting at `0x10`, but that's wrong: the two `ExtraSize` bytes would be read as the first audio sample.
> For mono files that just adds one silent sample, but for stereo files it shifts every frame by half, so the
> left and right channels come out swapped. All 498 shipped files only divide evenly into whole sample frames with the
> 18-byte header.

## Variants in the shipped game

| Channels | Sample rate | Bits | Files |
|----------|-------------|------|-------|
| Mono     | 22050 Hz    | 16   | 425   |
| Stereo   | 22050 Hz    | 16   | 72    |
| Mono     | 44100 Hz    | 16   | 1     |

22050 Hz was a common compromise at the time. It halves the size of CD-quality audio, which mattered on a CD-ROM
full of voice-over and music, and it still sounds fine through the PC speakers of the day.

## Converting to WAV

Since the header already has everything a WAV `fmt ` chunk needs, converting is just re-wrapping:

1. Write a RIFF/WAVE header.
2. Write a 16-byte `fmt ` chunk using the first 16 bytes of the DDS header (drop `ExtraSize`, which PCM WAV files
   leave out).
3. Write a `data` chunk containing everything from offset `0x12` onwards.
