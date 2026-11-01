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

### Converting to glTF

`betateam convert fin gltf <input directory> <output directory>` writes each FIN file as a binary glTF (`.glb`) that
Blender can import:

- Every object in the tree becomes a glTF node with its own local transform, so parts end up where the file places
  them. A root node named after the file turns the scene from Z-up (3ds Max) to glTF's Y-up; the values below it
  are the file's own. Blender turns it back on import: world positions match the file, but the objects' local values
  in Blender are Blender's conversion of them (a rotation about Z shows as one about −Y).
- Shapes become meshes with their vertex positions, normals, colours and first two texture coordinates. Normals are
  scaled to unit length, which glTF requires; a shape with a zero normal gets none and Blender computes its own.
  Each shape gets a material built from the properties that apply to it (see [Materials](#materials)). A `DDCorona`
  has no triangles, so its vertices are written as points.
- A `NiLODNode` keeps only its most detailed level: the child whose range starts closest to the camera.
  `--include-all-lods` keeps every level. Each level's node carries its index into the ranges as `lodLevel`.
- Characters' flat `LOD_` stand-ins (see [Distant characters](#distant-characters)) are left out the same way, and
  `--include-all-lods` keeps them. They then overlap the skinned meshes they replace, and face whichever way the file
  stores them, since only the game turns them towards the camera.
- Objects whose *app culled* flag is set (see [NiAVObject](#niavobject)) are left out with everything below them,
  because the game loads them hidden. They're effects, glows and lights, such as `Effect group` planes, `DDCorona`
  glows, explosion geometry and lights named `... On`; presumably the game's code shows them when they're needed.
  `--include-hidden` keeps them, and their extras still show the flag. glTF has no hidden state that Blender's
  importer reads, so they then show as visible. The flat `LOD_` stand-ins aren't marked this way: the game hides
  them while it runs.
- Lights aren't part of the tree (see [NiLight](#nilight)), so they are placed under the root as empty nodes with
  their own transform. A directional, point or spot light also gets a child node `Light` holding a
  `KHR_lights_punctual` light: its diffuse colour (converted to linear like material colours) with the dimmer as
  intensity, pointing along the light's direction, and for a spotlight the spot angle as the outer cone. The fade with
  distance has no glTF equivalent, so no range is written; the original values stay in the light node's extras.
  Ambient lights have no glTF light.
- An object whose [collision propagate mode](#collision-propagate-modes) makes its bounding volume the final shape
  (`0`, `4`, `6`) gets a child node `Collision0`, `Collision1`, … after its own children for each box, sphere and
  capsule of the volume (every member of a union), placed and turned where the volume is. Each holds an
  `OMI_physics_body` collider naming its shape in the file's `OMI_physics_shape` list, so an importer such as Godot
  makes a static body of it. Volumes with no such shape stay in the extras only, counted on the object's node as
  `collisionShapesSkipped`: inverted volumes, half spaces, intersections, lozenges and degenerate shapes. The OMI
  extensions are used rather than `KHR_collision_shapes` because Godot 4.7 imports only these. Objects in the other
  modes get no collider; for them the engine tests the triangles below, which an importer can generate from the
  meshes.
- The generated `Light` and `Collision` nodes carry no extras, so every node with extras is one of the file's
  objects.
- Every other field of an object goes into its node's *extras*, which Blender shows under *Object Properties →
  Custom Properties*: names, flags, bounding volumes, extra data, and its properties written out in full. Blocks
  outside the tree, such as `DDActorSharedData` and texture animations, are in the root node's `blocks`, keyed by
  link ID; other extras refer to them as `{"ref": "0x...", "class": ...}`. The third texture coordinate component
  is kept as `TextureCoordinateW` on shapes where it isn't always `0`.
- glTF doesn't allow texture coordinates that aren't finite numbers, and some shipped files have them (see
  [NiTriShape](#nitrishape)). We chose to write `0` for each such u or v, and to record on the shape's node how many
  coordinates were affected, as `NonFiniteTextureCoordinates`. Nearly all of them are in the second texture set, which
  only lightmaps use; an affected triangle shows one texel of its texture.
- A skinned mesh (`Ni3dsSkin`) is written the way the game draws it, not with its stored vertices, which are in
  another frame (see [Skinned meshes](#skinned-meshes)). Its vertices are placed by their bones at rest, and the
  bones become the joints of a glTF skin. Blender imports them as an armature, and posing a bone moves the mesh.

- A morphing shape (`Ni3dsMorphShape`) is written with its first target as the mesh and every other target as a glTF
  morph target, the difference from the first, which is how the game blends them (see
  [Morphing shapes](#morphing-shapes)).
- Each skill becomes a glTF animation; see [Animations](#animations).

#### Animations

Every skill (see [DDActorSharedData](#ddactorshareddata)) becomes one glTF animation with the skill's name, cut from
the object's single timeline between the skill's start and end. Times in the glb are seconds from the skill's start.

- **What a skill moves** is what the game moves when it plays it (see [Playing a skill](#playing-a-skill)): the
  animated nodes the skill lists, every node with a key inside the skill or a pose at the skill's start or end that
  differs from its pose at time 0, the skill's morphing shapes and flip-books, and every colour and transparency
  animator.
- **Values** are worked out the way the game interpolates its keys (see [Keys](#keys)), every 1/60 s plus at every key
  inside the skill, and written as linear glTF keys. A player that interpolates linearly between them stays close to
  the game, and matches it at each sample.
- **Transforms:** a node with position keys gets a translation, one with rotation keys a rotation, one with scale keys a
  scale. A rotation always comes with a uniform scale, because a rotation key replaces the whole rest rotation,
  including a mirrored one, which glTF writes as a negative scale.
- **Visibility** uses `KHR_node_visibility`, animated through `KHR_animation_pointer`, with a step at the skill's
  start and at each visibility key. The node's own `KHR_node_visibility` says whether the game loads it shown.
- **Colours:** a diffuse colour or transparency animator drives the material's base colour, an emissive animator its
  emissive colour, both through `KHR_animation_pointer` and with the same conversion as the material's own colours.
  An unlit material only takes the transparency. The game drives every colour animator of an object whatever skill it
  plays, so every skill's animation carries them. Ambient and specular colour animators aren't written: glTF has no
  place for those colours.
- **Morphing:** the weights of the shape's morph targets.
- **Flip-books** have no glTF animation target. The root node's extras list them under `flipbooks`, one entry per
  skill and material: `{"skill", "material", "images", "times", "frames"}`, where `images` are glTF image indices,
  and from `times[i]` (seconds from the skill's start) on the material shows `images[frames[i]]`. A skill that only
  flips textures has no channel, so no glTF animation is written for it; neither is one for a skill with nothing to
  animate.
- Flip-books that no skill runs (those without `TexturePropExtraData`) run on their own in the game and aren't
  converted. A node can only be animated when its rest transform is a rotation and scale; one that isn't stops the
  conversion with an error. `--include-hidden` is needed for the hidden nodes some skills show.

#### Materials

A shape's material comes from every property that applies to it: its own and those of the nodes above it, a lower
one replacing a higher one of the same class, the way the engine keeps its drawing state. Shapes with the same
properties share one material.

- The texture is `images[index]` of the `NiTextureProperty`, on the first texture set. It isn't embedded: the glb
  names it by a URI relative to the model, `../textures/<name>.png` by default (`--texture-uri-prefix` changes the
  part before the name), with the name in lower snake case (`Studs Gry.tga` becomes `studs_gry.png`). Convert the
  textures with `betateam convert tga png` into that folder, for example models into `out/models` and both texture
  archives into `out/textures`. Textures are shared between models that way, as in the game.
- The sampler follows the `NiTextureModeProperty`'s filter and clamp modes (see below).
- The base colour is the `NiMaterialProperty`'s diffuse colour and alpha (alpha limited to `0`–`1`; one shipped
  material says `3.6`), and its emittance becomes the emissive colour. Vertex colours multiply the base colour, as
  glTF prescribes and as the engine's texture apply mode does.
- The game's colours (diffuse, emittance, vertex colours) are display values: its renderer multiplies them as they
  are, like the hardware of its time. glTF colours are linear, so they're converted with the sRGB curve; a renderer
  that multiplies in linear space then shows the same products the game did. Alpha stays as it is.
- A shape without normals can't be lit: the engine shows its vertex colours, which have the lighting baked in. Such a
  shape gets an unlit material (`KHR_materials_unlit`) with a white base colour; only the alpha is kept. Every
  shape with a lightmap is one of these. That the diffuse colour plays no part then is inferred, not confirmed.
- A shape under a `NiAlphaProperty` with blending on is blended (`alphaMode: BLEND`); otherwise it's opaque. Every
  shipped alpha property uses the ordinary source-alpha blend.
- A lightmap (the one image of a `NiMultiTextureProperty`) multiplies the shape's colour, mapped with the second
  texture set. glTF has no place for it, so the image goes into the glTF image list and the material's extras point
  to it: `"lightmap": {"image": <index>, "texCoord": 1}`.
- Every property is also written out in full in the material's extras, under `properties`, for what glTF has no
  field for (depth buffer, vertex colour mode, specular, ...).

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
| `u32`     | collision propagate | How collision tests treat this object and its children (engine: `GetCollisionPropagate`), see [Collision propagate modes](#collision-propagate-modes) |
| `u32`     | has bounding volume | Non-zero if a collision shape follows, see [Bounding volumes](#bounding-volumes) |

The transform is local: an object's position, rotation and scale are relative to its parent node, and vertices are
relative to the shape that holds them. Where an object ends up in the world is the combination of every transform
from the root down to it. A converter that copies vertices without applying those transforms puts every part at the
origin in its own orientation.

The three groups of the rotation matrix are its rows: the engine rotates a point as *R · v*, with the first group
giving the new x, the second the new y and the third the new z. This is how later NetImmerse and Gamebryo versions
store it too, and the game's skinning code confirms it: it applies the combined bone matrices to vertices this way.

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

Some shipped files have texture coordinates that are NaN or infinite, in all three components. They sit on vertices
that triangles use, nearly always in the second texture set of shapes with only one or two triangles. Most are the
NaN an x86 processor returns for `0 / 0`, so a texture mapping that divided by zero in the exporter probably produced
them. That is inferred, not verified. How the game draws these triangles hasn't been checked.

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
| `i32` | mode  | How the node turns (engine: `SetMode`), see below |

The node keeps its position and scale and replaces its rotation, so that its local +Z points at the viewer and +Y
stays up:

| Mode | Turn                                                                                         |
|------|----------------------------------------------------------------------------------------------|
| `0`  | Faces the camera, rolled to keep the node's up axis as upright as the view allows            |
| `1`  | Turns only about the parent's Y axis towards the camera; in shipped files that axis is up      |
| `2`  | Takes the camera's rotation as it is (inferred from the code's shape, not traced in detail)   |

### NiLight

A light. One class covers every kind of light; the light type field tells them apart. It doesn't hang in the node
tree as a child: nodes list the lights that shine on them in their effects. The field names follow the engine's
getters (`GetLocation`, `GetDimmer`, `GetAttenuationCurve`, ...).

| Type        | Field                | Meaning                                                               |
|-------------|----------------------|-----------------------------------------------------------------------|
| `vec3`      | location             | Position                                                              |
| `vec3`      | direction            | Direction, for directional lights and spotlights                      |
| `bool`      | light switch         | Whether the light is on                                               |
| `f32`       | spot angle           | Cone angle of a spotlight, in degrees                                 |
| `f32`       | spot exponent        | How quickly a spotlight fades towards the edge of its cone            |
| `f32`       | dimmer               | Brightness multiplier; the colours are multiplied by it               |
| 3 × `f32`   | ambient colour       | Red, green, blue                                                      |
| 3 × `f32`   | diffuse colour       | Red, green, blue                                                      |
| 3 × `f32`   | specular colour      | Red, green, blue                                                      |
| `f32`       | attenuation distance | Distance over which the light fades                                  |
| `f32`       | attenuation curve    | Shape of the fade                                                     |
| `bool`      | attenuation          | Whether the light fades with distance at all                         |
| `i32`       | light type           | Kind of light: `0` ambient, `1` directional, `2` point, `3` spot. Shipped files use `1` and `2` |
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
| 5    | half space    | A plane: `vec3` normal *n* and `f32` constant *c*. Solid where *n · p ≤ c*      |
| 6    | intersection  | `u32` count, then that many nested volumes. Inside all of them                  |

Half spaces and intersections are Digital Domain's additions. An inverted shape collides from the inside, for
example to keep something within an area. The types a file can use are registered by the collision library, so a
type outside 0–6 can't be read. Shipped files give 324 nodes a volume: mostly boxes and unions, then spheres,
capsules and a few intersections.

The box's axes are unit vectors and its extents half sizes along them. The capsule is the segment from the origin to
origin + direction, widened by the radius.

#### Collision propagate modes

Whether the engine collides with an object's bounding volume, with the triangles below it or with nothing depends on
its collision propagate mode:

| Mode          | Collision                                                                   |
|---------------|-----------------------------------------------------------------------------|
| `0`, `6`      | The object's bounding volume is the final shape; nothing below it is tested |
| `1`, `2`, `7` | Descends: the children are tested, and geometry collides with its triangles |
| `3`           | Nothing in the subtree collides                                             |
| `4`           | As `0`, but not while the object is hidden (app culled)                     |
| `5`           | As `1`, but not while the object is hidden (app culled)                     |

Hidden objects in the other modes still collide.

## Properties

Properties set how objects are drawn. A property applies to the object that lists it and to everything below that
object in the tree, unless something lower down overrides it. Every property starts with the `NiObject` fields and
then a `bool` *master* flag (engine: `GetMaster`), whose effect hasn't been confirmed. The exception is
`NiShadeProperty`, whose loader skips the property part and has no master flag.

The mode fields below are engine enums. The texture modes follow the order NetImmerse kept into Gamebryo, which the
game's renderer confirms; the vertex colour mode isn't mapped yet.

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

Every shipped alpha property blends, with source mode `6` and destination mode `7`: source alpha and one minus source
alpha in NetImmerse's order, the ordinary transparency blend.

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

| Value | Filter                                    | Clamp                                  |
|-------|-------------------------------------------|----------------------------------------|
| `0`   | Nearest texel                             | Clamped in both directions             |
| `1`   | Bilinear                                  | Clamped in u, repeating in v           |
| `2`   | Trilinear (bilinear, blending mipmaps)    | Repeating in u, clamped in v           |
| `3`   | Nearest texel, nearest mipmap             | Repeating in both directions           |
| `4`   | Nearest texel, blending mipmaps           |                                        |
| `5`   | Bilinear, nearest mipmap                  |                                        |

The renderer stores clamp modes as a u and a v address mode in that order, so which of the two is u is inferred from
the order. Without mipmaps, the renderer falls back to nearest (`3`, `4`) or bilinear (`2`, `5`). Every shipped apply mode is
`3`, which multiplies the texture with the lit or vertex colour, alpha included.

### NiMultiTextureProperty

Several textures drawn on top of each other in stages. Each list has one entry per stage.

| Type                  | Field         | Meaning                                                 |
|-----------------------|---------------|---------------------------------------------------------|
| `link[]`              | images        | `NiImage` per stage                                     |
| `u32` + count × `u32` | combine modes | How each stage combines with the one before             |
| `u32` + count × `u32` | clamp modes   | As in `NiTextureModeProperty`                           |
| `u32` + count × `u32` | filter modes  | As in `NiTextureModeProperty`                           |

The game uses it only for lightmaps: every shipped one has a single image, combine mode `0`, clamp `0` and filter `1`.
Combine mode `0` multiplies the image with the colour below it, at full strength (not doubled).

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

An animation that steps through the images of a `NiTextureProperty`. In engine terms it is an *action*, so no other
block links to it. A texture property that has a `TexturePropExtraData` (see [Extra data](#extra-data)) gets its
flip-book run by the skills that list the extra data's index among their actions (see
[Playing a skill](#playing-a-skill)); one without runs on its own from the moment the file is loaded (inferred).

| Type   | Field        | Meaning                                                              |
|--------|--------------|----------------------------------------------------------------------|
| `u32`  | out of bound | What happens after the last image: `0` starts again, `1` runs back and forth, `2` stays on the last image |
| `f32`  | rate         | Playback speed (engine: `SetRate`); the game uses it only to set up the cycle time |
| `f32`  | start time   | When the animation starts, in ticks (engine: `GetStartTime`)        |
| `f32`  | cycle time   | Ticks for one pass through the images. Together with the rate and the number of images this gives the time per image: cycle time × rate ÷ images (engine: `GetSecsPerFrame`). The name is this toolkit's |
| `link` | textures     | The `NiTextureProperty` whose index it changes                      |

At a time *t* in ticks the game shows image ⌊images × *p* ÷ cycle time⌋, never past the last one, where *p* is
how far *t* is past the start time: wrapped into one cycle when the animation starts again (before the start too),
counted back in every second cycle when it runs back and forth, or held at the first and last image when it stays.

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
layout and how the game plays them.

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
angle and axis are how 3ds Max describes the rotation; the quaternion is the same rotation in NetImmerse's form,
stored w first. A visibility key is a time and a `bool`. Colours are animated with position keys, with red, green and
blue in place of x, y and z.

How the game works out a value at a time *t*:

- At or before the first key it takes the first key's value, at or after the last key the last one's, and at a key's
  own time that key's. Otherwise it interpolates from the key before to the key after, with *s* going from 0 to 1
  between their times.
- Linear keys interpolate linearly; rotations use spherical interpolation (slerp) of the quaternions. The game's slerp
  doesn't flip a quaternion to take the shorter way round.
- Bézier and TCB position keys store the segment's cubic: the value is value + *s*(c₁ + *s*(c₂ + *s*·c₃)), with c₁
  the out tangent (Bézier) or the second precomputed vector (TCB) and c₂, c₃ the last two vectors.
- Bézier and TCB float keys are Hermite curves: from the key's value with its out tangent (Bézier) or second
  precomputed value (TCB) to the next key's value with its in tangent (Bézier) or first precomputed value (TCB).
- Bézier and TCB rotation keys use *squad*: slerp(2*s*(1−*s*), slerp(*s*, q₀, q₁), slerp(*s*, a, b)), where a is the
  key's control quaternion (Bézier: its one extra quaternion; TCB: the first) and b the next key's (Bézier: its one;
  TCB: the second).
- The rotation key's last `u32` picks the interpolation: `0` is the one above. The game also has modes that
  interpolate the angle and axis, with or without extra spins, but no shipped key uses them.
- An Euler key holds three float key lists, angles in radians about x, y and z, each evaluated as above (an empty one
  is 0). Its `u16` picks the order the three turns are applied in; `0`, the only one shipped, turns about x first,
  then y, then z. Only the first Euler key of a list counts.
- Visibility holds each key's value until the next key.
- A barycentric morph key holds, per morph target, a weight, an in tangent and an out tangent (the 3 × n values, all
  floats); each target's weight is a Hermite curve between keys.

### Animation settings

Every 3ds animation class stores the same playback settings: `u32` animation type, three unknown `u8`, a `bool`
*scene graph update*, `u32` cycle type (how the animation repeats), then five `f32`: default display time,
frequency (playback speed, usually `1`), phase, begin key time and end key time. The names are the engine's
(`GetAnimType`, `GetSceneGraphUpdate`, `GetCycleType`, `GetDefaultDisplayTime`, `GetFrequency`, `GetPhase`,
`GetBeginKeyTime`, `GetEndKeyTime`). Times are 3ds Max ticks, 4800 per second: the game turns a skill's seconds into
ticks by multiplying by 4800 and rounding.

Before evaluating keys the game turns a time *t* into *t* × frequency + phase, then applies the cycle type: `0`
wraps it into the range from begin to end key time, `1` runs back and forth through that range, and any other value
leaves it. The result is then kept within the range. A range of zero length always gives the begin key time.

### Animated classes

| Class                | Based on             | Fields after the base class                                                     |
|----------------------|----------------------|---------------------------------------------------------------------------------|
| `Ni3dsAnimationNode` | `NiNode`             | Settings, then rotation, position and scale (float) key lists, each storing its type only when it has keys, then `i32` count and visibility keys |
| `Ni3dsBone`          | `Ni3dsAnimationNode` | Nothing: a bone is an animated node that a skin refers to                      |
| `Ni3dsColorAnimator` | `NiObject`           | Settings, `link` target, `u32` colour (`0` ambient, `1` diffuse, `2` specular, `3` emissive), `i32` count, key type (always), colour keys. Each channel is kept within `0`–`1` |
| `Ni3dsAlphaAnimator` | `NiObject`           | Settings, `link` target, `i32` count, key type (always), float keys for the material's alpha (not limited) |
| `Ni3dsSkin`          | `NiTriShape`         | Unknown `u8`, `bool` has skin data; if set, per vertex a `u16` count of influences, each a `f32` weight, a `vec3` offset relative to the bone, and a `link` to the `Ni3dsBone` |
| `Ni3dsMorphShape`    | `NiTriShape`         | Settings, 2 unknown `u8`, `i32` target count, `i32` key count, a bounding sphere (`vec3`, `f32`), key type and float keys, then per target one `vec3` per vertex |

A skinned mesh's vertices move with the bones that influence them: each vertex has a list of bones, how strongly each
one pulls it, and where the vertex sits relative to that bone. A morph shape keeps several complete sets of vertex
positions and blends between them.

### Skinned meshes

The game never draws a skin's stored vertices. It builds every vertex from its bones instead: the vertex's offset,
moved by the bone's transform, and with several bones a weighted sum of those. The stored vertices are in another
frame, probably the pose the model had in 3ds Max, and a converter that uses them puts the parts in the wrong place.

A bone's transform here is the chain of local transforms from the *root bone* down to it. The root bone is the first
bone in the chain whose parent isn't a bone, and nothing above it counts. The result is placed by the skin's own
transform, so the skin and its root bone's parent should sit in the same place. In the shipped files they do: the
skin is under an "auto geom parent" node with no transform of its own, next to the root bone or a few untransformed
levels away.

In every shipped skin, each vertex follows exactly one bone. With one bone the game ignores the weight and moves the
vertex with the bone in full, so body parts move rigidly, like the parts of a minifigure.

### Morphing shapes

A `Ni3dsMorphShape` with barycentric keys draws a blend of its targets: the first target, plus each other target's
difference from the first times that target's weight. The stored vertices aren't drawn. That the blend is exactly this
sum is inferred from how the game prepares the targets; the function that adds them up hasn't been read.

### Distant characters

Characters carry a cheaper version of their skinned body parts: nodes named `LOD_` followed by the part (`LOD_LArm1`,
`LOD_Chest`), each holding a flat shape under a [NiBillboardNode](#nibillboardnode) that turns it towards the camera.
Beyond the distance set in the character's [DDActorSharedData](#ddactorshareddata), the game hides the skinned
meshes and shows these stand-ins instead; nearer, it does the reverse. It finds them by name, looking through the
actor's nodes but not inside a `LOD_` node. The game never shows both at once, so a viewer that shows everything
has them poke through each other.

In the shipped files, exactly the 13 objects with `LOD_` nodes set that distance: 350 for most, 450 for the rest.

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
| `f32`         | LOD distance²     | Squared distance beyond which a character shows its `LOD_` stand-ins instead of its skinned meshes; `0` never swaps (see [Distant characters](#distant-characters)) |
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

The indices refer to the actor's own lists of animated parts, which the game builds when it loads the object:

- **Animation nodes:** walking the actor's tree depth first, each `Ni3dsAnimationNode` (bones included) in turn gets
  the next of the shared data's tracks, and its own keys are replaced by them; a node listed in two places is reached
  twice and keeps the last track. The indices count the nodes in that order.
- **Actions:** each texture property with at least two images and a `TexturePropExtraData` puts its flip-book at the
  extra data's index.
- **Other animations:** morphing shapes; that they're counted in tree order is inferred.

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

#### Playing a skill

When it loads an object the game adds to every skill's list of animation nodes each node whose track has a key
between the skill's start and end, or whose pose at the skill's start or end differs from its pose at time 0. It also
drops the keys between skills from each track, keeping the first and last key of every gap, which doesn't change any
value inside a skill.

While a skill plays, the game evaluates its animation nodes, its actions and its other animations at the skill's time
in ticks, and every colour and transparency animator of the object as well, whichever skill it is.

### DDCorona

A glow around a light, such as a lamp or a beacon: a small flat shape that always faces the camera. It fades out with
distance and when something solid is between the camera and the glow; objects whose name starts with `nonsolid` don't
block it. It is stored like a `NiTriShape` without the triangle list (the game builds its triangles when drawing),
followed by one field:

| Type  | Field | Meaning                                                                |
|-------|-------|------------------------------------------------------------------------|
| `f32` | size  | Size of the glow (engine: the size argument of `MakeCorona`). `0` or less means the object's scale is used instead |
