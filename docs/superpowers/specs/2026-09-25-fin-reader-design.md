# FIN reader and dump: design

## Goal

Read every FIN model shipped with LEGO Alpha Team completely and verifiably, and make the contents inspectable
through a `betateam fin dump` command. This is the foundation for later exporters.

Out of scope for this task:

- Conversion to glTF for Blender (#6)
- Conversion to NIF for NifSkope (#5)
- Meaning of animation controllers (a later milestone; controllers are only parsed as far as the stream requires)

## Background

FIN files start with `Dweezil 23\n`. After the header come blocks back to back, in the pre-4.0 NetImmerse stream
layout. Each block is a length-prefixed class name, a 4-byte pointer that serves as the object's link ID, and the
block data. `Top Level Object` markers mark root objects and the file ends with an `End Of File` string. Blocks carry
no size field, so an unknown class can't be skipped.

`LoadComp.dll` registers the header with `NiStream::SetNewHeader("Dweezil ", 23)`. `NiStream::LoadHeader` then
requires exactly that prefix and version, and skips the copyright lines that standard NetImmerse files have.

The shipped files contain the standard NetImmerse classes (`NiNode`, `NiTriShape`, `NiLODNode`, `NiBillboardNode`,
`NiLight`, `NiEnvMappedTriShape`, and several property and texture classes), Digital Domain classes from
`LoadComp.dll` (`DDUnit`, `DDActorSharedData`, `DDEnv`, `DDCorona`) and 3ds Max animation classes from
`NiAnimation.dll` (`Ni3dsAnimationNode`, `Ni3dsBone`, `Ni3dsSkin`, `Ni3dsMorphShape`, `Ni3dsColorAnimator`,
`Ni3dsAlphaAnimator`, `Ni3dsPropAnimExtraData`). This list comes from a scan for length-prefixed names and gets
confirmed by the parser.

## Architecture

All reader code lives in `Malumware.BetaTeam.Lib/IO/Fin/` and follows the `IO/Pac` and `IO/Dds` pattern.

| Type                | Responsibility                                                                             |
|---------------------|--------------------------------------------------------------------------------------------|
| `FinHeader`         | Record with the header line and version, plus `IsValid`                                    |
| `FinHeaderParser`   | Reads the `Dweezil <version>\n` line                                                        |
| `FinStreamParser`   | Block loop: class name, marker handling, registry lookup, leftover byte check               |
| `FinBlockRegistry`  | Maps class name to block reader                                                             |
| `FinLinker`         | Resolves pointer references to blocks after parsing                                         |
| `FinReader`         | Opens a file and returns a `FinFile`                                                        |
| `FinFile`           | Header, blocks in file order, top-level objects                                             |
| `Blocks/*`          | One class per engine class, named after it, with a `Load` method mirroring `LoadBinary`     |

### Parsing rules

- `Top Level Object` marks the next object as a root. `End Of File` ends the stream.
- Any other class name is looked up in the registry. An unknown class throws `InvalidDataException` with the class
  name and file offset. Continuing would produce garbage.
- Bytes after `End Of File` throw `InvalidDataException`.
- Block classes mirror the engine's inheritance: `NiObject` (link ID, name, inline extra data) → `NiAVObject`
  (transform, properties) → `NiNode`/`NiTriShape`, and so on. Each `Load` calls its base first, as the engine's
  `LoadBinary` chain does, so shared fields are read in one place.
- Values are stored exactly as in the file (rotation matrix in file order, scale as a single float). Conversions
  belong in exporters, so the dump stays faithful.

### Linking

References to other blocks are read as `FinRef<T>` placeholders holding a pointer value. After parsing, `FinLinker`
builds a pointer → block dictionary and resolves every reference. A pointer of 0 is null. A pointer with no matching
block, or a block of the wrong type, throws `InvalidDataException`.

## Reverse-engineering workflow

For each class:

1. **Code:** decompile its `LoadBinary` and `LinkObject` (`NiMain.dll`, `NiAnimation.dll`, or `LoadComp.dll`/
   `LegoLogicComp.dll` for the DD classes) to get the fields, types and version branches.
2. **Data:** check the layout against every shipped FIN. Each block ends exactly where the next class name begins,
   and every file has zero leftover bytes.
3. **Record:** addresses and decompiled code go in `Research/notes/fin.md`. What each field means goes in
   `docs/formats/fin.md`, with inferred parts marked as such.

NifSkope's `nif.xml` may be used as a hint for field names, never as evidence.

Order:

1. Stream framing and header (including where `Dweezil 23` is checked and what the version affects)
2. `NiObjectNET`, `NiAVObject`, `NiNode`
3. `NiTriShape` and its geometry data, `NiEnvMappedTriShape`
4. `NiLODNode`, `NiBillboardNode`, `NiLight`
5. Properties, textures and extra data
6. `Ni3dsAnimationNode`, `Ni3dsBone`, `Ni3dsSkin`, `Ni3dsMorphShape`, `Ni3dsColorAnimator`, `Ni3dsAlphaAnimator`,
   `Ni3dsPropAnimExtraData`, as far as the stream requires
7. `DDUnit`, `DDActorSharedData`, `DDEnv`, `DDCorona`

## Dump command

```
betateam fin dump <INPUT> [-o|--output <PATH>] [--json] [--full] [-m|--mask *.FIN]
```

- `<INPUT>` is a FIN file or a directory.
- Without `--output`, a single file is dumped to the terminal. For a directory input, `--output` is required and must
  be a directory. Each FIN then gets its own `.txt` or `.json` file.
- Tree format (default): the top-level objects are the roots, with children, properties, extra data and controllers
  below. Each block shows its class, pointer ID and fields. Written to a file, the tree is plain text without colour
  codes.
- A block referenced more than once is written in full the first time. Later references appear as
  `→ <Class> @<pointer>`, so there are no duplicates and no endless loops.
- Arrays (vertices, triangles, etc.) are summarised as `<count> × <type>` unless `--full` is given.
- `--json` writes the complete graph as JSON. Shared blocks are handled the same way as in the tree.
- The dump walks the block records by reflection, so new block types need no dump code.
- In batch mode, a file that fails is reported with class name and offset, and the rest continue. The exit code is
  non-zero if any file failed.

The command is registered under a new `fin` branch in `Program.cs`, which future FIN commands (`convert fin gltf`,
etc.) can join.

## Testing

Unit tests (input buffers built in code, no game data):

- `FinHeaderParser`: valid header, wrong prefix, missing newline
- `FinStreamParser`: top-level marker, end marker, unknown class (message contains class name and offset), leftover
  bytes
- `FinLinker`: resolved reference, null reference, dangling reference, wrong type
- Each block reader, with a minimal buffer
- Dump: shared reference output, array summarising, plain text output without colour codes

`[GameDataFact]` tests over every shipped FIN:

- Every file parses with zero leftover bytes
- Every link resolves
- Every file has at least one top-level object
- Every class that appears has a registered reader

While class groups are still being added, the game data test keeps an explicit list of pending class names. A file
may only fail with an unknown class error for a name on that list. Any other failure fails the test, including an
unknown name that isn't on the list, which usually means an earlier block was misread and the parser landed on
garbage. Each class group commit removes its names from the list, and the last one removes the list.

## Documentation

- `docs/formats/fin.md`: framing, class hierarchy, fields per class, what's known about each DD class
- Entry in `docs/README.md`
- Update the FIN row in the `AGENTS.md` format status table
- Evidence in `Research/notes/fin.md` (not tracked)

## Commits

Framing and header first, then one or more commits per class group in the order above, and the dump command last.
Every commit builds and its tests pass. The game data test's pending list shrinks with each step (see Testing).
