# LEGO Alpha Team — File Format Documentation

These documents describe the data formats used by LEGO Alpha Team (PC, 2000): what each file contains,
why it is laid out the way it is, and what quirks you will run into when reading it.

They are written for people building tools, not for people studying the engine. So they explain what each field
means and why it is there, and they skip where in the game's code it gets read.
Every claim was checked against the game's own loading code and against every file shipped with the game.
Where something is inferred and not confirmed, the text says so.

## Background

LEGO Alpha Team was developed by Digital Domain and published by LEGO Media in 2000. It runs on a modified version of
NetImmerse, the engine that was later renamed Gamebryo and went on to power games like Morrowind and Oblivion.
The game's engine DLLs identify themselves as "NetImmerse File Format, Version 7.0, With Modifications by
Digital Domain". Many of the formats below come from Digital Domain rather than from NetImmerse, which is why a lot of
names start with `DD`.

The game targets 32-bit Windows 95/98. That explains many of the design choices: formats are built around Win32
structures (`FILETIME`, `WAVEFORMATEX`, split 32-bit offsets) and are made to be memory-mapped or handed straight
to DirectX-era APIs.

## Game data layout

Almost all game data ships inside eight `.pac` archives next to the executable:

| Archive         | Contents                                                          |
|-----------------|-------------------------------------------------------------------|
| `Ambient.pac`   | Ambient/background audio (`.DDS`)                                 |
| `Audio.pac`     | Sound effects and voice lines (`.DDS`)                            |
| `Bhvr.pac`      | Per-object behaviour code, as Windows DLLs (`.DLL`)               |
| `Etc.pac`       | Fonts (`.DDF` + `.TGA` glyph pages), puzzles (`.PUZ`), text files |
| `Fin.pac`       | 3D models and scenes (`.FIN`)                                     |
| `LMap.pac`      | Textures, most likely lightmaps given the `LM` in names (`.TGA`)  |
| `Locale_en.pac` | English voice-over audio (`.DDS`) and string tables (`.TXT`)      |
| `Map.pac`       | Textures (`.TGA`)                                                 |

## Formats

| Format                                    | Status                                           |
|-------------------------------------------|--------------------------------------------------|
| [PAC archives](formats/pac.md)            | Documented, verified                             |
| [DDS audio](formats/dds-audio.md)         | Documented, verified                             |
| [String tables](formats/string-tables.md) | Documented, verified                             |
| TGA textures                              | Standard Truevision TGA                          |
| [FIN models](formats/fin.md)              | Documented, verified (reading); converts to glTF |
| DDF fonts, PUZ puzzles                    | Not yet investigated                             |

## Conventions

- All integers are little-endian (x86) unless stated otherwise.
- Offsets are in bytes from the start of the file (or of the structure, where noted), written in hexadecimal.
- `u16`/`u32`/`u64` are unsigned integers of 2, 4 and 8 bytes. `FILETIME` is the Windows 64-bit timestamp:
  100-nanosecond intervals since 1601-01-01 UTC.
