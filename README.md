# BetaTeam

CLI toolkit written in C# and .NET 8 for interacting with LEGO Alpha Team PC source files.

Licensed under the [GNU General Public License v3.0](LICENSE).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Source Files

> [!WARNING]
> This repository does not include any original game files. You will need to source them yourself from a legitimate copy of LEGO Alpha Team.
> 
> Once installed, the game files can be found at: `C:\Program Files (x86)\LEGO Media\Games\LEGO Alpha Team`
> 
> The game is old enough that installation on modern Windows can be tricky, you just need it to install, not run.
> A community guide for getting it running on Windows 10 is available at the [Rock Raiders United forums](https://rockraidersunited.com/topic/7573-lego-alpha-team-windows-10-setup-guide/).
> Installing on a Windows XP VM is the route i took, the game installed but dit not run.

## Solution Structure

| Project                        | Description                                                               |
|--------------------------------|---------------------------------------------------------------------------|
| `Malumware.Common`             | Shared utilities                                                          |
| `Malumware.BetaTeam.Lib`       | Core library — parsers, readers, converters                               |
| `Malumware.BetaTeam.Lib.Tests` | Unit tests for the library                                                |
| `Malumware.BetaTeam.Cli`       | CLI entry point built with [Spectre.Console](https://spectreconsole.net/) |


## File Formats

Detailed format descriptions live in [`docs/`](docs/README.md).

### PAC Archives
PAC files are uncompressed archives used by LEGO Alpha Team, designed to be memory-mapped by the game.
A 16-byte header (`PACK` magic, archive size, directory size) is followed by a recursive directory of
null-terminated file names, each carrying a 64-bit offset, file size, and Windows FILETIME timestamp.
See [docs/formats/pac.md](docs/formats/pac.md).

### DDS Audio
`.DDS` files are raw PCM audio preceded by an 18-byte Windows `WAVEFORMATEX` header (format tag, channel count,
sample rate, byte rate, block align, bits per sample, extra size).
The converter wraps the raw PCM data in a standard RIFF/WAVE envelope to produce a valid `.wav` file.
See [docs/formats/dds-audio.md](docs/formats/dds-audio.md).


## CLI

### `unpack` — Extract PAC archives

Extracts all `.pac` archives in a directory. Each archive is unpacked into its own sub-folder named after the archive
file.

```
betateam unpack <INPUT_DIRECTORY> <OUTPUT_DIRECTORY> [--mask <pattern>]
```

| Argument / Option  | Description                             | Default |
|--------------------|-----------------------------------------|---------|
| `INPUT_DIRECTORY`  | Directory containing `.pac` archives    | —       |
| `OUTPUT_DIRECTORY` | Directory to write extracted files into | —       |
| `-m`, `--mask`     | File glob pattern                       | `*.pac` |

Examples:

```bash
betateam unpack /game/packs /game/extracted
betateam unpack /game/packs /game/extracted --mask *.pac
betateam unpack /game/packs /game/extracted --mask Audio.pac
```

### `convert dds wav` — Convert DDS audio to WAV

Converts `.DDS` proprietary audio files (Digital Domain Sound) to standard WAV files.

```
betateam convert dds wav <INPUT_DIRECTORY> <OUTPUT_DIRECTORY> [--mask <pattern>]
```

| Argument / Option  | Description                          | Default |
|--------------------|--------------------------------------|---------|
| `INPUT_DIRECTORY`  | Directory containing `.DDS` files    | —       |
| `OUTPUT_DIRECTORY` | Directory to write `.wav` files into | —       |
| `-m`, `--mask`     | File glob pattern                    | `*.DDS` |

Examples:

```bash
betateam convert dds wav /game/extracted/audio /game/wav
betateam convert dds wav /game/extracted/audio /game/wav --mask *.DDS
betateam convert dds wav /game/extracted/audio /game/wav --mask BUILD1LOOP.DDS
```