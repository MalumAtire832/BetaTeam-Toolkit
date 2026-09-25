# FIN Models

Every 3D object in the game lives in a `.FIN` file in `Fin.pac`: the environments the missions take place in, the
units the player builds with, and the other objects placed in levels. A FIN file is a NetImmerse scene graph saved
to disk, the same kind of data that later NetImmerse and Gamebryo games store in `.nif` files. The layout is that of
early NetImmerse, before the format gained block sizes and a block type table, with a few changes by Digital Domain.

The file names follow the IDs the rest of the game uses:

| Prefix | Files | Contents                                                                                     |
|--------|-------|----------------------------------------------------------------------------------------------|
| `E`    | 54    | Environments. Each mission's puzzle file names one, together with its ambient sound and lightmaps |
| `U`    | 42    | Units, matching IDs in the unit tables (inferred from the IDs)                               |
| `OG`   | 34    | Other objects placed in levels (inferred)                                                    |
| `B`    | 7     | Inferred from the IDs only                                                                   |
| `T`    | 7     | Inferred from the IDs only                                                                   |
| `BA`, `CU`, `P` | 1 each | Inferred from the IDs only                                                          |

Environments have no code of their own, but 62 of the other 93 files have a behaviour DLL with the same ID in
`Bhvr.pac`, which holds the code for that object.

## Header

A FIN file starts with one line of text:

```
Dweezil 23
```

followed by a line feed (`0x0A`) and nothing else. Standard NetImmerse files start with
`NetImmerse File Format, Version ...` and three copyright lines. Digital Domain replaced that line with their own
name and version number, and the game accepts exactly this one: a file with any other number is rejected as too old
or too new, and anything after the digits means it isn't a FIN file at all. The copyright lines are gone too, so the
first block starts right after the line feed.

Where the name "Dweezil" comes from isn't documented anywhere in the game.

## Blocks

Documented in the following sections.
