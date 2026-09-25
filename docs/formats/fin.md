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

After the header, the file is a list of blocks, one per object in the scene, written back to back. Each block starts
with the name of its class:

| Size       | Field      | Meaning                                  |
|------------|------------|------------------------------------------|
| `u32`      | length     | Length of the class name in bytes        |
| length     | class name | ASCII, no terminator (`NiNode`, `DDUnit`) |
| (varies)   | data       | The object's fields, as described below  |

Two names aren't classes but markers:

- `Top Level Object` marks the block that follows it as a root of the scene. The marker is followed by the real class
  name, then that block's data. A file can have more than one root.
- `End Of File` ends the list. The game stops reading there and ignores anything after it. Shipped files are
  expected to end exactly at the marker.

Blocks don't store their own size and there is no table of contents. The only way to find where a block ends is to
read all of its fields, so a reader has to know every class that appears in a file. A single unknown class, or a
single misread field, makes everything after it unreadable. This is how NetImmerse files worked before version 4,
when a block size and a type table were added.

### Strings

Class names and markers are *sized strings*: a `u32` length followed by that many bytes. Strings inside a block
(object names, file names) are *C strings* in the engine's terms. They use the same layout, but a length of `0`
means there is no string at all, which is different from an empty one.

### Links

Every block starts with a `u32` link ID: the address the object had in memory when the file was saved. Blocks refer
to each other by storing that value, for example a node listing its children. A link of `0` means "no object". The
IDs themselves mean nothing once loaded; they only have to be unique within a file.

Because a block can refer to one that comes later in the file, the game reads in two passes: first every block, then
it replaces each stored ID with the object it belongs to. A reader has to do the same.

## Classes

Each class reads the fields of its parent class first, then its own. The tables below list only a class's own
fields, in file order. Types: `u8`/`u32` unsigned integers, `f32` a 32-bit float, `bool` a `u8` that is `0` or `1`,
`vec3` three `f32` (x, y, z), `link` a `u32` link ID, `link[]` a `u32` count followed by that many links.

### NiObject

The base of every block.

| Type      | Field      | Meaning                                                                           |
|-----------|------------|-----------------------------------------------------------------------------------|
| `u32`     | link ID    | This object's ID, used by other blocks to refer to it (see [Links](#links))       |
| C string  | name       | The object's name, as set in 3ds Max (`Box01`, `Dummy Object`). Often absent      |
| `u32`     | extra data | Number of extra data entries that follow                                          |
| (varies)  | entries    | Each one: a C string with the extra data's class name, then that class's fields   |

In later NetImmerse versions the name and extra data moved to a class called `NiObjectNET`. Here they are part of
`NiObject` itself. Extra data is also stored inside its owner, not as blocks of its own, so it has no link ID. An
entry without a class name is read as plain `NiExtraData`.

### NiAVObject

Parent of everything that has a place in the scene: nodes, shapes, lights.

| Type      | Field               | Meaning                                                                        |
|-----------|---------------------|--------------------------------------------------------------------------------|
| `bool`    | app culled          | Hidden by the game ("application culled"), as opposed to culled because it's off-screen |
| `vec3`    | translation         | Position relative to the parent                                                |
| 9 × `f32` | rotation            | 3×3 rotation matrix relative to the parent, as three groups of three floats    |
| `f32`     | scale               | Uniform scale relative to the parent                                           |
| `vec3`    | velocity            | Local velocity                                                                 |
| `link[]`  | properties          | Render properties (material, texture, alpha, ...) that apply to this object and everything below it |
| `u32`     | collision propagate | How collision tests treat this object's children. Which value means what hasn't been confirmed |
| `u32`     | has bounding volume | Non-zero if a collision bounding volume follows                                |

The transform is local: an object's position, rotation and scale are relative to its parent node, and vertices are
relative to the shape that holds them. Where an object ends up in the world is the combination of every transform
from the root down to it. A converter that copies vertices without applying those transforms puts every part at the
origin in its own orientation.

Whether the three groups of the rotation matrix are rows or columns in the engine's maths is still to be confirmed.
Readers should keep the nine values in file order and decide when converting.

### NiNode

A node groups other objects. It has no geometry of its own.

| Type      | Field          | Meaning                                                                                |
|-----------|----------------|----------------------------------------------------------------------------------------|
| `u32`     | sorting mode   | Whether the children are sorted (for transparency) before drawing: `0` on, `1` off, `2` default |
| `u32`     | sorter         | The address of the sorting object when the file was saved. The game ignores it         |
| `bool`    | visual object  | Whether the node counts as something visible (inferred from the engine's name for it)   |
| `link[]`  | children       | Child objects. A `0` is an empty slot                                                  |
| `link[]`  | effects        | Dynamic effects (lights) that light this node's subtree                                |
