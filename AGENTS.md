# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

A C# (.NET 8) toolkit for reading, converting and documenting the file formats of LEGO Alpha Team (PC, 2000,
Digital Domain). The game runs on a modified NetImmerse engine (the predecessor of Gamebryo), and most of its formats
are custom to Digital Domain.

| Project                        | Purpose                                                      |
|--------------------------------|--------------------------------------------------------------|
| `Malumware.Common`             | Shared helpers (e.g. `BinaryReaderExtensions`)               |
| `Malumware.BetaTeam.Lib`       | Format parsers, readers and converters, under `IO/<Format>/` |
| `Malumware.BetaTeam.Lib.Tests` | xUnit tests, mirroring the `Lib` folder structure            |
| `Malumware.BetaTeam.Cli`       | `betateam` CLI built on Spectre.Console.Cli                  |

Format documentation for readers lives in `docs/` (index: `docs/README.md`).

## Build and test

```bash
dotnet build
dotnet test
dotnet run --project Malumware.BetaTeam.Cli -- <command>
```

## Game files

The game files are proprietary and must never be committed. Only the owner's legitimate copy is used.

- `Research/` is gitignored and is the local workspace for everything derived from the game.
- Tests that need the real files use `[GameDataFact]` instead of `[Fact]`. They find the game through the
  `ALPHATEAM_GAME_DIR` environment variable or `Research/game`, and skip automatically when it's missing.
- Never copy game data, decompiled code or disassembly into tracked files, including test fixtures. Unit tests build
  their input buffers in code.

## Code conventions

Match the existing code:

- Block-scoped namespaces (`namespace Foo.Bar { ... }`), never file-scoped.
- Always put braces around `if`/`for`/`foreach`/`while` bodies, even single statements.
- Interface members get an explicit `public` modifier.
- Constants are `UPPER_SNAKE_CASE` (`SIZE`, `MARKER`, `MAX_NAME_LENGTH`).
- No XML doc comments on self-explanatory members. Use short `//` comments to explain *why*, e.g. a quirk of the
  original format.
- Parsers derive from `AbstractParser<T>` (it owns a `BinaryReader`), implement `Parse()`, and throw
  `InvalidDataException` on malformed input.
- Fixed-size headers are records with a `SIZE` constant and an `IsValid` property.
- Reading a format is split into `*Header`, `*HeaderParser`, `*Reader` and, where relevant, `*Converter` classes. See
  `IO/Pac` and `IO/Dds` for reference.
- Tests follow `Method_ExpectedResult_WhenCondition` naming, with `// Arrange`, `// Act`, `// Assert` sections.
- Paths taken from archive data must go through `PacArchiveEntry.GetDestinationPath` (or an equivalent check) so
  crafted files can't write outside the output directory.

## Reverse-engineering workflow

`scripts/setup-research.sh <game-directory> [ghidra-install-directory]` creates the `Research/` workspace: it links
the game, unpacks the archives, collects the binaries, runs the initial Ghidra analysis and registers the MCP server.
It can safely run again. The layout it creates:

```
Research/
├── game/        symlink to the installed game (DLLs, EXE, .pac archives)
├── extracted/   output of `betateam unpack`, one folder per archive
├── binaries/    symlinks to every DLL/EXE, including the behaviour DLLs from Bhvr.pac
├── ghidra/      analysed Ghidra project `alphateam` (all 86 binaries)
├── notes/       findings with code evidence (addresses, function names); decomp/ has decompiler dumps
└── patterns/    ImHex pattern files
```

A `pyghidra` MCP server ([pyghidra-mcp](https://github.com/clearbluejar/pyghidra-mcp), run through `uvx`) is registered
at local scope for this repo and opens that project. Use it to decompile functions, follow cross-references and search
strings.

If the server fails to connect:

- Only one process can open the Ghidra project at a time. A second Claude session, or a leftover `pyghidra-mcp`
  process, makes new connections fail. Check with `ps -ef | grep pyghidra-mcp`.
- A new project imports every binary before the server answers, which takes longer than Claude Code's startup
  timeout. The setup script avoids this by analysing everything up front.
- Local-scope MCP config is shared between a repository and its git worktrees, so running the setup script in a
  worktree replaces the main checkout's registration.

Useful starting points:

| Binary                   | Contains                                                             |
|--------------------------|----------------------------------------------------------------------|
| `DDPackedDirFileLib.dll` | PAC archive loading (`DDPackedDirFile`, `DDDirectory::Restore`)      |
| `NiMain.dll`             | Core NetImmerse: `NiStream`, object registration, `LoadBinary`s      |
| `NiAnimation.dll`        | Animation controllers and keyframes                                  |
| `NiA3dSound.dll`         | Audio (reads the 18-byte DDS header)                                 |
| `LoadComp.dll`, `LegoLogicComp.dll` | Game-specific components, likely custom classes          |
| `Research/binaries/*.DLL` from `Bhvr.pac` | Per-object behaviour code                           |

Rules for format work:

1. Verify every claim twice: against the game's code (what it reads) and against every shipped file (what it
   contains). A layout that only fits the data isn't confirmed, and neither is one that only fits the code.
2. Treat "the current parser works" as weak evidence. The PAC reader worked on every shipped archive while misreading
   the header and the directory structure.
3. Write evidence (addresses, function names, disassembly) to `Research/notes/`. Write the reader-facing
   explanation to `docs/formats/`.
4. `docs/` describes meaning, reasoning and historical context. It doesn't cover engine internals or addresses.
   Mark anything inferred rather than verified as such.
5. Add a `[GameDataFact]` test that checks a format invariant across all shipped files (e.g. zero leftover bytes,
   whole sample frames).

## Format status

| Format     | Status                                                                                       |
|------------|----------------------------------------------------------------------------------------------|
| PAC        | Verified and documented (`docs/formats/pac.md`)                                              |
| DDS audio  | Verified and documented (`docs/formats/dds-audio.md`)                                        |
| Locale TXT | Verified and documented (`docs/formats/string-tables.md`)                                    |
| TGA        | Standard Truevision TGA                                                                      |
| FIN models | Under investigation. Files start with `Dweezil 23\n`, followed by length-prefixed NetImmerse class names (`NiNode`, `NiTriShape`, ...) and their data. The engine reports "NetImmerse File Format, Version 7.0, With Modifications by Digital Domain". Goal: a C# parser and export to NIF files that NifSkope can open. |
| DDF, PUZ   | Not yet investigated                                                                         |

## Git and pull requests

- Branch from `master`. Split work into focused commits that each build and pass tests on their own.
- Commit messages: a short imperative subject, then a body explaining what changed and why.
- Open PRs with `gh` against `master`. Include a summary, the reasoning for each change, how it was verified, and any
  breaking API changes.
