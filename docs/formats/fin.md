# FIN Models

Every 3D object in the game lives in a `.FIN` file in `Fin.pac`: the environments the missions take place in, the
units the player builds with, and the other objects placed in levels. A FIN file is a NetImmerse scene graph saved
to disk, the same kind of data that later NetImmerse and Gamebryo games store in `.nif` files. The layout is that of
early NetImmerse, before the format gained block sizes and a block type table, with a few changes by Digital Domain.

The file names follow the IDs the rest of the game uses:

| Prefix | Files | Contents (from the objects' own descriptions)                                               |
|--------|-------|----------------------------------------------------------------------------------------------|
| `E`    | 54    | Environments. Each mission's puzzle file names one, together with its ambient sound and lightmaps |
| `U`    | 42    | Units the player builds with (`Diving Board`, `Catapult`, `Launcher`)                        |
| `OG`   | 34    | Objects placed in levels (`Lever Activator`, `One-Light Door Panel`, `Ogel Laser`)           |
| `B`    | 7     | Ogel, the game's villain, and his henchmen (`Evil Ogel`, `Guard`, `Sentry`, `Assembly Line Worker`) |
| `T`    | 7     | The Alpha Team members, by specialty (`Motion Specialist`, `Rope Specialist`), and `Tee Vee` |
| `BA`, `CU`, `P` | 1 each | `Goody`, `CORD`, and `Low Crate Stack`, the only prop                              |

Every object's behaviour is code in a DLL in `Bhvr.pac`. Most objects have a DLL with their own ID; the others,
including all environments, name the DLL they share (see [DDUnit and DDEnv](#ddunit-and-ddenv)).

### Where the names come from

Field names follow the game's own code wherever it names them, for example through a getter such as `GetShadowMult`.
Those names are given in the tables as *engine: `GetShadowMult`*. Some values come from the tools the models were
made with rather than from the game: 3ds Max object names and animation ticks, and memory addresses the exporter
saved along with the data. The text says so where it matters. Anything else that is reasoned out rather than read
from the code is marked *inferred*.

### Inspecting files

`betateam fin dump <file>` prints a file's block tree with every field, `--json` writes it as JSON and `--full`
expands every array. Given a directory and `-o`, it dumps every file into its own `.txt` or `.json`. The JSON nests as
deep as the scene tree, which can go past the default depth limit of some JSON libraries.

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
- `End Of File` ends the list. The game stops reading there and ignores anything after it. Every shipped file ends
  exactly at the marker.

Every shipped file has exactly one root: a `DDUnit` in the 93 unit and object files, a `DDEnv` in the 54 environments
(see [Digital Domain classes](#digital-domain-classes)).

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

Blocks often refer to one that comes later in the file (a node is written before its children), so the game reads
in two passes: first every block, then it replaces each stored ID with the object it belongs to. A reader has to do
the same.

## Classes

Each class reads the fields of its parent class first, then its own. The tables below list only a class's own
fields, in file order. Types: `u8`/`u16`/`u32` unsigned integers, `i32` a signed integer, `f32` a 32-bit float,
`bool` a `u8` that is `0` or `1`, `vec3` three `f32` (x, y, z), `link` a `u32` link ID, `link[]` a `u32` count
followed by that many links. Where the engine reads a count as signed, a negative count means none.

### NiObject

The base of every block.

| Type      | Field      | Meaning                                                                           |
|-----------|------------|-----------------------------------------------------------------------------------|
| `u32`     | link ID    | This object's ID, used by other blocks to refer to it (see [Links](#links))       |
| C string  | name       | The object's name, as set in 3ds Max (`Box01`). Often absent                     |
| `u32`     | extra data | Number of extra data entries that follow                                          |
| (varies)  | entries    | Each one: a C string with the extra data's class name, then that class's fields   |

In later NetImmerse versions the name and extra data moved to a class called `NiObjectNET`. Here they are part of
`NiObject` itself. Extra data is also stored inside its owner, not as blocks of its own, so it has no link ID. An
entry without a class name is read as plain `NiExtraData`.

### NiAVObject

Parent of everything that has a place in the scene: nodes, shapes, lights.

| Type      | Field               | Meaning                                                                        |
|-----------|---------------------|--------------------------------------------------------------------------------|
| `bool`    | app culled          | Hidden by the game, as opposed to culled because it's off-screen (engine: `GetAppCulled`) |
| `vec3`    | translation         | Position relative to the parent                                                |
| 9 × `f32` | rotation            | 3×3 rotation matrix relative to the parent, as three groups of three floats    |
| `f32`     | scale               | Uniform scale relative to the parent                                           |
| `vec3`    | velocity            | Local velocity (engine: `GetLocalVelocity`)                                    |
| `link[]`  | properties          | Render properties (material, texture, alpha, ...) that apply to this object and everything below it |
| `u32`     | collision propagate | How collision tests treat this object's children (engine: `GetCollisionPropagate`). Which value means what hasn't been confirmed |
| `u32`     | has bounding volume | Non-zero if a collision shape follows, see [Bounding volumes](#bounding-volumes) |

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
| `u32`     | sorting mode   | Whether the children are sorted (for transparency) before drawing: `0` on, `1` off, `2` default (engine: `SetSortingOn`/`Off`/`Default`) |
| `u32`     | sorter         | The address of the sorting object when the file was saved. The game ignores it         |
| `bool`    | visual object  | Whether the node counts as something visible (engine: `IsVisualObject`; the effect is inferred) |
| `link[]`  | children       | Child objects. A `0` is an empty slot                                                  |
| `link[]`  | effects        | Lights (`NiLight`) that shine on this node's subtree                                   |

### NiTriShape

A mesh of triangles. The class inherits from two abstract classes, `NiGeometry` and `NiTriBasedGeom`, whose fields
come first. Unlike later NetImmerse versions, where the mesh sits in a separate `NiTriShapeData` block that several
shapes can share, the geometry here is stored in the shape itself.

Several arrays are optional. Each is preceded by a `u32` that held the array's memory address when the file was
saved: `0` means the array is absent, anything else means it follows.

From `NiGeometry`:

| Type                  | Field        | Meaning                                                |
|-----------------------|--------------|--------------------------------------------------------|
| `u16`                 | vertex count | Number of vertices                                     |
| `u32` + count × `vec3` | vertices    | Vertex positions, in the shape's local space           |
| `u32` + count × `vec3` | normals     | One normal per vertex                                  |
| `vec3`                | bound center | Center of a sphere around all vertices                 |
| `f32`                 | bound radius | Radius of that sphere                                  |

From `NiTriBasedGeom`:

| Type                            | Field             | Meaning                                                          |
|---------------------------------|-------------------|------------------------------------------------------------------|
| `u16`                           | triangle count    | Number of triangles                                              |
| `u16`                           | texture set count | Number of texture coordinate sets (0 to 2 in shipped files)      |
| `u32` + sets × vertices × `vec3` | texture coordinates | One array per set, one coordinate per vertex                  |
| `u32` + vertices × 4 × `f32`    | colours           | Vertex colours as red, green, blue, alpha                        |
| `u32` + triangles × 4 × `f32`   | triangle planes   | A plane per triangle: normal (x, y, z) and constant. Not always normalised |

From `NiTriShape`:

| Type                  | Field     | Meaning                                                     |
|-----------------------|-----------|-------------------------------------------------------------|
| triangles × 3 × `u16` | triangles | Vertex indices of each triangle. No flag, always present    |

Texture coordinates have three components, not two as in other NetImmerse versions. The first two are the usual
u and v, and they go well outside 0..1 where textures repeat. What the third one is for isn't known yet: in the
shipped files it is mostly `0`, and otherwise a value close to `0`, `0.5` or `1`.

The engine reads all texture coordinates into one array. That the first set comes first, then the second, is
inferred from how other NetImmerse versions store them: the file layout is the same size either way.

### NiEnvMappedTriShape

Stored exactly like `NiTriShape`. Going by the name, the class makes the engine draw the shape with an environment
map (a reflection); the reading code doesn't show this.

### NiLODNode

A level-of-detail node: it shows one child at a time, picked by the camera's distance. It inherits from the abstract
`NiSwitchNode`, whose fields come first.

From `NiSwitchNode`:

| Type   | Field                    | Meaning                                                        |
|--------|--------------------------|----------------------------------------------------------------|
| `i32`  | active child             | Index of the child that is currently shown                    |
| `bool` | update only active child | Whether the engine updates only the shown child, not all of them |

From `NiLODNode`:

| Type                 | Field             | Meaning                                                                  |
|----------------------|-------------------|--------------------------------------------------------------------------|
| `i32`                | range count       | Number of ranges, one per child                                         |
| count × (`f32`, `f32`, `vec3`) | ranges  | Near distance, far distance and the point distances are measured from   |
| `bool`               | position in range | Engine: `GetPositionInRange`; its effect isn't confirmed                |

The ranges are in the same order as the node's children: child 0 is shown when the camera is between the first
range's near and far distance, and so on.

### NiBillboardNode

A node that turns to face the camera, so flat shapes below it always face the viewer.

| Type  | Field | Meaning                                                                        |
|-------|-------|--------------------------------------------------------------------------------|
| `i32` | mode  | How the node turns (engine: `SetMode`). Shipped files use `0`, `1` and `2`; which turn is which isn't mapped yet |

### NiLight

A light. One class covers every kind of light; the light type field tells them apart. It doesn't hang in the node
tree as a child: nodes list the lights that shine on them in their effects. The field names follow the engine's
getters (`GetLocation`, `GetDimmer`, `GetAttenuationCurve`, ...).

| Type        | Field                | Meaning                                                               |
|-------------|----------------------|-----------------------------------------------------------------------|
| `vec3`      | location             | Position                                                              |
| `vec3`      | direction            | Direction, for directional lights and spotlights                      |
| `bool`      | light switch         | Whether the light is on                                               |
| `f32`       | spot angle           | Cone angle of a spotlight                                             |
| `f32`       | spot exponent        | How quickly a spotlight fades towards the edge of its cone            |
| `f32`       | dimmer               | Brightness multiplier                                                 |
| 3 × `f32`   | ambient colour       | Red, green, blue                                                      |
| 3 × `f32`   | diffuse colour       | Red, green, blue                                                      |
| 3 × `f32`   | specular colour      | Red, green, blue                                                      |
| `f32`       | attenuation distance | Distance over which the light fades                                  |
| `f32`       | attenuation curve    | Shape of the fade                                                     |
| `bool`      | attenuation          | Whether the light fades with distance at all                         |
| `i32`       | light type           | Kind of light. Shipped files use `1` and `2`; not mapped to names yet |
| `i32` + count × `u32` | illuminated nodes | Link IDs of the nodes it lights. The game reads and ignores them: the nodes' effect lists are what count |

### Bounding volumes

Some objects carry a collision shape. It is stored inside the object, right after the has-bounding-volume flag, as a
`u32` type followed by the shape:

| Type | Shape         | Fields                                                                          |
|------|---------------|---------------------------------------------------------------------------------|
| 0    | sphere        | `vec3` center, `f32` radius, `bool` inverted (engine: `IsInverted`)             |
| 1    | box           | `vec3` center, 3 × `vec3` axes, `vec3` half-size along each axis, `bool` inverted |
| 2    | capsule       | `vec3` origin, `vec3` direction, `f32` radius, `bool` inverted                  |
| 3    | lozenge       | `vec3` origin, two `vec3` edges of a parallelogram, `f32` radius                |
| 4    | union         | `u32` count, then that many nested volumes. Inside any of them                  |
| 5    | half space    | A plane: `vec3` normal and `f32` constant. Everything on one side               |
| 6    | intersection  | `u32` count, then that many nested volumes. Inside all of them                  |

Half spaces and intersections are Digital Domain's additions. An inverted shape collides from the inside, for
example to keep something within an area. The types a file can use are registered by the collision library, so a
type outside 0–6 can't be read. Shipped files give 324 nodes a volume: mostly boxes and unions, then spheres,
capsules and a few intersections.

## Properties

Properties set how objects are drawn. A property applies to the object that lists it and to everything below that
object in the tree, unless something lower down overrides it. Every property starts with the `NiObject` fields and
then a `bool` *master* flag (engine: `GetMaster`), whose effect hasn't been confirmed. The exception is
`NiShadeProperty`, whose loader skips the property part and has no master flag.

Most mode fields below are engine enums whose numbers aren't mapped to names yet.

### NiMaterialProperty

| Type      | Field          | Meaning                                   |
|-----------|----------------|-------------------------------------------|
| 3 × `f32` | ambient colour | Colour under ambient light                |
| 3 × `f32` | diffuse colour | Main surface colour                       |
| 3 × `f32` | specular colour| Colour of highlights                      |
| 3 × `f32` | emittance      | Colour the surface gives off by itself    |
| `f32`     | shininess      | Size of highlights (engine: `GetShineness`) |
| `f32`     | alpha          | Opacity, `0` transparent to `1` opaque    |

### NiAlphaProperty

| Type   | Field             | Meaning                                                 |
|--------|-------------------|---------------------------------------------------------|
| `bool` | alpha blending    | Whether the object is blended with what's behind it     |
| `u32`  | source blend mode | Blend factor for the object's own colour                |
| `u32`  | destination blend mode | Blend factor for the colour behind it              |

### NiTextureProperty

| Type     | Field  | Meaning                                                         |
|----------|--------|-----------------------------------------------------------------|
| `i32`    | index  | Which image in the list is shown                                |
| `link[]` | images | `NiImage` blocks. More than one when the texture is animated    |

### NiTextureModeProperty

How the texture is applied. Each field is an engine enum.

| Type  | Field  | Meaning                                                   |
|-------|--------|-----------------------------------------------------------|
| `u32` | apply  | How the texture combines with the lit surface colour      |
| `u32` | filter | How the texture is sampled (nearest, bilinear, mipmaps)   |
| `u32` | clamp  | Whether the texture repeats or stops at its edges         |

### NiMultiTextureProperty

Several textures drawn on top of each other in stages. Each list has one entry per stage.

| Type                  | Field         | Meaning                                                 |
|-----------------------|---------------|---------------------------------------------------------|
| `link[]`              | images        | `NiImage` per stage                                     |
| `u32` + count × `u32` | combine modes | How each stage combines with the one before             |
| `u32` + count × `u32` | clamp modes   | As in `NiTextureModeProperty`                           |
| `u32` + count × `u32` | filter modes  | As in `NiTextureModeProperty`                           |

### Other properties

| Class                   | Fields after the master flag                                        |
|-------------------------|----------------------------------------------------------------------|
| `NiVertexColorProperty` | `u32` colour mode: how vertex colours are used                       |
| `NiZBufferProperty`     | `bool` depth test, `bool` depth write                                |
| `NiSpecularProperty`    | `bool` specular highlights on or off                                 |
| `NiShadeProperty`       | No master flag. `bool` smooth shading on or off                      |

## Textures

### NiImage

A texture image.

| Type          | Field                    | Meaning                                                           |
|---------------|--------------------------|-------------------------------------------------------------------|
| `bool`        | external                 | Whether the image is a separate file                              |
| C string      | file name                | Only when external: the image file, a `.tga` name without a path. The game looks it up in its texture folders |
| `link`        | raw data                 | Only when not external: a block with the pixels                   |
| `u32`         | preferred texture format | Pixel format the engine should convert the image to (engine: `GetPreferredTextureFormat`) |

The game loads each file name only once and shares it between all images that name it.

### NiFlipTextures

An animation that steps through the images of a `NiTextureProperty`. In engine
terms it is an *action*: the game starts running it as soon as the file is loaded, so no other block links to it.

| Type   | Field        | Meaning                                                              |
|--------|--------------|----------------------------------------------------------------------|
| `u32`  | out of bound | What happens after the last image (engine enum, not mapped yet)     |
| `f32`  | rate         | Playback speed (engine: `SetRate`)                                   |
| `f32`  | start time   | When the animation starts (engine: `GetStartTime`)                  |
| `f32`  | cycle time   | Together with the rate and the number of images this gives the time per image: cycle time × rate ÷ images (engine: `GetSecsPerFrame`). The name is this toolkit's |
| `link` | textures     | The `NiTextureProperty` whose index it changes                      |

## Extra data

Extra data entries sit inside the `NiObject` part of their owner (see [NiObject](#niobject)). Every entry starts with
a `u32` size. A plain `NiExtraData` (an entry without a class name) is followed by that many bytes of raw data; no
shipped file has one. The subclasses below write a size too, but the game ignores it and reads their fields
instead.

| Class                    | Fields after the size                                                                    |
|--------------------------|------------------------------------------------------------------------------------------|
| `TexturePropExtraData`   | `link` to a `NiFlipTextures`, `i32` index. Digital Domain's: ties an animated texture to its property |
| `Ni3dsPropAnimExtraData` | `link` to the 3ds animator that animates the owning property                            |

## Animation

Animated models use the 3ds Max animation classes (`Ni3ds...`) that came with NetImmerse's 3ds Max exporter: node
animation, colour and transparency animators, skinned meshes and morphing meshes. This section describes their
layout so files can be read; what each setting does is left for a later investigation.

### Keys

Animation is stored as keys: a time and a value, plus interpolation data. A list of keys starts with a count and the
type of all keys in it. Some lists only store the type when the count is above zero; the classes below say which.

| Type | Float key                          | Position key                                  | Rotation key              |
|------|------------------------------------|-----------------------------------------------|---------------------------|
| 1    | linear: time, value                | linear: time, `vec3` value                    | linear (base fields only) |
| 2    | Bézier: + in and out tangent       | Bézier: + 4 × `vec3` (tangents and two values the exporter precomputed) | Bézier: + quaternion, `f32` |
| 3    | TCB: + tension, continuity, bias, 2 values the exporter precomputed | TCB: + tension, continuity, bias, 4 × `vec3` | TCB: + tension, continuity, bias, 2 quaternions, 2 × `f32` |
| 4    | morph key: nothing is stored       |                                               | Euler: + `u16`, then three float key lists (x, y, z) |
| 5    | barycentric morph: TCB + `u32` n and 3 × n values |                                  |                           |
| 6    | cubic morph: TCB + 2 × `f32`       |                                               |                           |

Every rotation key starts with the time, an angle, a `vec3` axis, a quaternion (4 × `f32`), an `i32` number of extra
spins and a `u32` whose meaning is unknown (engine: `GetAngle`, `GetAxis`, `GetQuaternion`, `GetExtraSpins`). The
angle and axis are how 3ds Max describes the rotation; the quaternion is the same rotation in NetImmerse's form. A visibility key is a time and a `bool`. Colours are animated with
position keys, with red, green and blue in place of x, y and z.

### Animation settings

Every 3ds animation class stores the same playback settings: `u32` animation type, three unknown `u8`, a `bool`
*scene graph update*, `u32` cycle type (how the animation repeats), then five `f32`: default display time,
frequency (playback speed, usually `1`), phase, begin key time and end key time. The names are the engine's
(`GetAnimType`, `GetSceneGraphUpdate`, `GetCycleType`, `GetDefaultDisplayTime`, `GetFrequency`, `GetPhase`,
`GetBeginKeyTime`, `GetEndKeyTime`). Times appear to be 3ds Max ticks (4800 per second): end key times such as 960,
3200 and 12800 are common.

### Animated classes

| Class                | Based on             | Fields after the base class                                                     |
|----------------------|----------------------|---------------------------------------------------------------------------------|
| `Ni3dsAnimationNode` | `NiNode`             | Settings, then rotation, position and scale (float) key lists, each storing its type only when it has keys, then `i32` count and visibility keys |
| `Ni3dsBone`          | `Ni3dsAnimationNode` | Nothing: a bone is an animated node that a skin refers to                      |
| `Ni3dsColorAnimator` | `NiObject`           | Settings, `link` target, unknown `u32`, `i32` count, key type (always), colour keys |
| `Ni3dsAlphaAnimator` | `NiObject`           | Settings, `link` target, `i32` count, key type (always), float keys           |
| `Ni3dsSkin`          | `NiTriShape`         | Unknown `u8`, `bool` has skin data; if set, per vertex a `u16` count of influences, each a `f32` weight, a `vec3` offset relative to the bone, and a `link` to the `Ni3dsBone` |
| `Ni3dsMorphShape`    | `NiTriShape`         | Settings, 2 unknown `u8`, `i32` target count, `i32` key count, a bounding sphere (`vec3`, `f32`), key type and float keys, then per target one `vec3` per vertex |

A skinned mesh's vertices move with the bones that influence them: each vertex has a list of bones, how strongly each
one pulls it, and where the vertex sits relative to that bone. A morph shape keeps several complete sets of vertex
positions and blends between them.

## Digital Domain classes

These classes are Digital Domain's own. They turn a NetImmerse scene into a game object: something with a type, a
description, a shadow, animations it can play and sounds that go with them.

### DDUnit and DDEnv

Every object except the environments is a `DDUnit`: the units the player builds with as well as characters and
other objects. Environments are `DDEnv`. Both are *actors* and are stored the same way: a full `NiNode`, followed by
one field.

| Type   | Field       | Meaning                                                                     |
|--------|-------------|-----------------------------------------------------------------------------|
| `link` | shared data | The `DDActorSharedData` block that describes this kind of object            |

The actor is one placed instance; the shared data is what all instances of that kind of object have in common.

The code that makes an object behave lives in a DLL in `Bhvr.pac`. The game (engine: `LoadActorDLL`) loads the DLL
named by the shared data's behaviour field from its `BehaviorDir` folder, or the one named after the object itself
when the field is empty. That lets objects share code: all
54 environments use `tl0019`, the six `T0011`–`T0016` objects use `t0011`, and a few units reuse another unit's
behaviour (`U0209` runs `u0165`).

### DDActorSharedData

| Type          | Field             | Meaning                                                                         |
|---------------|-------------------|---------------------------------------------------------------------------------|
| (`NiObject`)  | name              | The object's type name, which is its ID (`U0001`, `OG9997`)                      |
| C string      | description       | A readable description (`Catapult`, `Guard`) (engine: `GetDescription`)          |
| C string      | behaviour         | The behaviour DLL with the object's code (see below)                             |
| `bool`        | make shadow       | Whether the object casts a shadow (engine: `GetMakeShadow`)                      |
| `bool`        | prop              | Whether the object is a prop (engine: `IsProp`). Only `P0121`, a crate stack, sets it; what changes for a prop isn't known |
| `f32`         | shadow multiplier | Strength or size of the shadow (engine: `GetShadowMult`)                         |
| `u32`         | (unknown)         |                                                                                  |
| `i32` + count | skills            | Named animation clips, see below                                                 |
| `i32` + count | tracks            | Keyframes for the animated nodes: rotation, position and scale key lists (type stored only when they have keys) and visibility keys, as in `Ni3dsAnimationNode` |
| `u32` + count × `vec3` | floor points | Points the object stands on (engine: `GetFloorPts`; how they're used is inferred) |

A *skill* is a named stretch of the object's animation timeline. When the game plays a skill, it runs the object's
animations from the start time to the end time, and it can play a sound along with it. Most objects have a `neutral`
skill; others are generic (`anim1`, `on`, `off`) or specific to a character or machine (`ladder_up`, `fidget_a`,
`exploding`, `victory`, `lever_pulla`). 265 skills in the shipped files have a sound.

| Type                  | Field            | Meaning                                                                   |
|-----------------------|------------------|---------------------------------------------------------------------------|
| C string              | name             | The skill's name                                                          |
| `f32`                 | start time       | Where the clip starts on the timeline                                     |
| `f32`                 | end time         | Where it ends. The clip's length is end − start (engine: `DDSkill::GetDuration`) |
| `bool`                | has sound        | Whether a sound block follows                                             |
| (sound)               | sound            | Only when present, see below                                              |
| `i32` + count × `u16` | animation nodes  | Which of the actor's animated nodes the skill moves, by index (engine: `GetAnimNodeArray`) |
| `i32` + count × `u16` | actions          | Which of its actions (such as texture animations) the skill runs (engine: `GetActionArray`) |
| `i32` + count × `u16` | other animations | Which of its other animated parts (such as morphing meshes) the skill runs (engine: `GetAnimEtcArray`) |

The indices refer to the actor's own lists of animated parts, which the game builds when it loads the object. How it
orders those lists hasn't been worked out yet, so the indices can't be matched to blocks in the file for now.

A skill's sound:

| Type     | Field                | Meaning                                                                       |
|----------|----------------------|-------------------------------------------------------------------------------|
| C string | file name            | The sound file, looked up in the game's `AudioDir` folder                     |
| C string | node name            | When set, the sound comes from that part of the object rather than the whole (inferred) |
| `bool`   | loop                 | Whether the sound repeats (inferred)                                          |
| `bool`   | source type          | Passed on when the game creates the sound source; its meaning isn't known    |
| `bool`   | distance flag        | Passed on with the distances below; its meaning isn't known                   |
| `f32`    | delay                | Seconds into the skill before the sound starts; `0` or less plays it at once  |
| `f32`    | gain                 | Volume (engine: `Sound_SetGain`)                                              |
| `f32`    | distance model scale | How quickly the sound gets quieter with distance (engine: `SetDistanceModelScale`) |
| `f32`    | max distance         | Distance beyond which the sound is no longer heard (engine: `SetMinMaxDistance`) |
| `f32`    | min distance         | Distance within which the sound plays at full volume (engine: `SetMinMaxDistance`) |

### DDCorona

A glow around a light, such as a lamp or a beacon: a small flat shape that always faces the camera. It fades out with
distance and when something solid is between the camera and the glow; objects whose name starts with `nonsolid` don't
block it. It is stored like a `NiTriShape` without the triangle list (the game builds its triangles when drawing),
followed by one field:

| Type  | Field | Meaning                                                                |
|-------|-------|------------------------------------------------------------------------|
| `f32` | size  | Size of the glow (engine: the size argument of `MakeCorona`). `0` or less means the object's scale is used instead |
