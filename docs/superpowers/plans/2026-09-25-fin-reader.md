# FIN Reader and Dump Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read every shipped FIN model completely and verifiably, and inspect it with `betateam fin dump`.

**Architecture:** A stream parser reads the `Dweezil 23` header, then blocks back to back (class name → registry →
block class whose `Load` mirrors the engine's `LoadBinary`). References are read as `FinRef<T>` and resolved by a link
pass. A reflection-based dump builder turns the resolved graph into a dump model that a text writer and a JSON writer
render. The CLI command is a thin wrapper.

**Tech Stack:** .NET 8, xUnit, Spectre.Console.Cli, System.Text.Json (`Utf8JsonWriter`), System.Numerics.

**Spec:** `docs/superpowers/specs/2026-09-25-fin-reader-design.md`

## Global Constraints

- Block-scoped namespaces (`namespace Foo.Bar { ... }`), never file-scoped.
- Braces around every `if`/`for`/`foreach`/`while` body.
- Interface members get an explicit `public` modifier.
- Constants are `UPPER_SNAKE_CASE`.
- No XML doc comments on self-explanatory members; short `//` comments explain *why* (format quirks).
- Parsers derive from `AbstractParser<T>` and throw `InvalidDataException` on malformed input.
- Tests: `Method_ExpectedResult_WhenCondition`, with `// Arrange`, `// Act`, `// Assert`.
- Unit tests build their input in code. Never copy game data, decompiled code or disassembly into tracked files.
- Game data tests use `[GameDataFact]` and read FIN files from `Fin.pac` through `PacArchiveReader`.
- Evidence (addresses, decompiled code) goes to `Research/notes/fin.md` (untracked). Meaning goes to
  `docs/formats/fin.md`, with inferred parts marked as such, and no engine addresses.
- Every commit builds and passes `dotnet test`. Commit messages: short imperative subject, body with what and why,
  ending with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HK7t5V32pGYunYnRQ4UoFg
  ```

## Known facts (from research before planning)

Recorded with addresses in `Research/notes/fin.md`. Summary:

- Header: exactly `Dweezil 23\n` (`SetNewHeader("Dweezil ", 23)`). Anything after the digits, or another version,
  is rejected by the engine. No copyright lines follow.
- Stream: repeated `u32 length + bytes` class names. `Top Level Object` is followed by the real class name.
  `End Of File` ends the stream. The engine ignores trailing bytes; we reject them.
- `NiObject`: `u32` link ID, CString name (`u32` length, `0` = null), `u32` extra data count, then per extra data a
  CString class name followed by that extra data's own fields (inline, not linked).
- `NiAVObject` (after `NiObject`): `u8` unknown, 3 × `f32` translation, 9 × `f32` rotation (3 rows of 3, file order),
  `f32` scale, 3 × `f32` velocity (inferred name), `u32` property count + `u32` link IDs, `u32` unknown, `u32`
  has-bound (non-zero → bounding volume follows).
- `NiNode` (after `NiAVObject`): `u32` unknown, `u32` unknown, `u8` unknown, `u32` child count + link IDs, `u32`
  effect count + link IDs.
- Verified on one block of one file only. The game data test extends this to all files.
- Class names found by scanning for length-prefixed names (a hypothesis until parsed): `NiNode`, `NiTriShape`,
  `NiTextureModeProperty`, `NiMaterialProperty`, `NiImage`, `NiAlphaProperty`, `NiTextureProperty`,
  `NiBillboardNode`, `Ni3dsAnimationNode`, `Ni3dsPropAnimExtraData`, `NiMultiTextureProperty`, `Ni3dsColorAnimator`,
  `Ni3dsAlphaAnimator`, `NiVertexColorProperty`, `NiFlipTextures`, `DDCorona`, `NiZBufferProperty`, `Ni3dsBone`,
  `DDActorSharedData`, `Ni3dsMorphShape`, `NiLight`, `DDUnit`, `Ni3dsSkin`, `NiLODNode`, `NiSpecularProperty`,
  `DDEnv`, `NiEnvMappedTriShape`, `NiShadeProperty`. Standard classes live in `NiMain.dll`, `Ni3ds*` in
  `NiAnimation.dll`, `DD*` in `LoadComp.dll`.

## Review Focus

- A truncated file (ends mid-block) must throw `InvalidDataException` naming the offset, not `EndOfStreamException`.
  Pinned in Task 3.
- A garbage length or count (misread earlier block, or a non-FIN file) must throw `InvalidDataException` quickly, not
  allocate gigabytes. Pinned in Task 2.
- Two blocks with the same link ID must throw `InvalidDataException`, not silently link to one of them. Pinned in
  Task 4.
- NaN or infinite floats (uninitialised data in old exporters) must not crash the JSON dump; they are written as
  strings. Pinned in Task 14.
- Blocks not reachable from any top-level object must still appear in the dump (under "Unreferenced"), so the dump
  stays complete. Pinned in Task 13.

## File Structure

```
Malumware.BetaTeam.Lib/IO/Fin/
├── FinHeader.cs                 header record (version, size)
├── FinHeaderParser.cs           parses "Dweezil <n>\n"
├── FinUnknownClassException.cs  InvalidDataException with ClassName + Offset
├── FinMatrix3.cs                raw 3x3 matrix in file order
├── FinRef.cs                    FinRef (non-generic base) + FinRef<T>
├── FinBlockReader.cs            primitive reads, strings, counts, refs, extra data
├── FinBlockRegistry.cs          class name -> block / extra data factory
├── FinStreamParser.cs           block loop, markers, leftover check, calls linker
├── FinLinker.cs                 resolves FinRefs
├── FinFile.cs                   name, header, objects, top-level objects
├── FinReader.cs                 file/bytes -> FinFile
├── Blocks/                      one class per engine class (NiObject, NiExtraData, NiAVObject, NiNode, ...)
└── Dump/
    ├── FinDumpModel.cs          FinDump + node records
    ├── FinDumpBuilder.cs        FinFile -> FinDump via reflection
    ├── FinTextDumpWriter.cs     tree as plain text
    └── FinJsonDumpWriter.cs     JSON
Malumware.BetaTeam.Lib.Tests/IO/Fin/
├── FinStreamBuilder.cs          test helper that writes FIN buffers
├── TestBlocks.cs                test-only block classes for parser tests
├── FinHeaderParserTests.cs, FinBlockReaderTests.cs, FinStreamParserTests.cs, FinLinkerTests.cs
├── Blocks/*Tests.cs             one per block group
├── Dump/*Tests.cs
└── FinGameDataTests.cs
Malumware.BetaTeam.Cli/Commands/Fin/FinDumpCommand.cs, FinDumpCommandSettings.cs
docs/formats/fin.md, docs/README.md, AGENTS.md
```

---

### Task 1: FIN header

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinHeader.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinHeaderParser.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinHeaderParserTests.cs`
- Create: `docs/formats/fin.md`

**Interfaces:**
- Produces: `FinHeader(int version, int size)` with `const string PREFIX`, `const int SUPPORTED_VERSION`,
  `const int MAX_LINE_LENGTH`, `int Version`, `int Size` (bytes including `\n`), `bool IsValid`.
  `FinHeaderParser(Stream) : AbstractParser<FinHeader>`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text;
using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinHeaderParserTests
    {
        [Fact]
        public void Parse_ReturnsHeader_WhenLineIsValid()
        {
            // Arrange
            using var parser = new FinHeaderParser(new MemoryStream(Encoding.ASCII.GetBytes("Dweezil 23\nrest")));

            // Act
            var header = parser.Parse();

            // Assert
            Assert.True(header.IsValid);
            Assert.Equal(23, header.Version);
            Assert.Equal(11, header.Size);
        }

        [Theory]
        [InlineData("NetImmerse File Format, Version 7.0\n")]
        [InlineData("Dweezil \n")]
        [InlineData("Dweezil 23 \n")]
        [InlineData("Dweezil 2x\n")]
        [InlineData("Dweezil 23")]
        public void Parse_ThrowsInvalidDataException_WhenLineIsMalformed(string text)
        {
            // Arrange
            using var parser = new FinHeaderParser(new MemoryStream(Encoding.ASCII.GetBytes(text)));

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenVersionIsNot23()
        {
            // Arrange
            using var parser = new FinHeaderParser(new MemoryStream(Encoding.ASCII.GetBytes("Dweezil 22\n")));

            // Act
            var act = () => parser.Parse();

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("22", exception.Message);
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenNoNewlineWithinMaxLength()
        {
            // Arrange
            var bytes = Encoding.ASCII.GetBytes("Dweezil " + new string('1', 200) + "\n");
            using var parser = new FinHeaderParser(new MemoryStream(bytes));

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~FinHeaderParserTests`
Expected: build error, `FinHeaderParser` not found.

- [ ] **Step 3: Implement**

`FinHeader.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public record FinHeader
    {
        // Digital Domain replaced the NetImmerse header line through NiStream::SetNewHeader
        public const string PREFIX = "Dweezil ";
        public const int SUPPORTED_VERSION = 23;

        // The engine reads the line into a 128-byte buffer
        public const int MAX_LINE_LENGTH = 128;

        public int Version { get; }
        public int Size { get; }

        // The engine accepts exactly one version and rejects older and later ones
        public bool IsValid => Version == SUPPORTED_VERSION;

        public FinHeader(int version, int size)
        {
            Version = version;
            Size = size;
        }
    }
}
```

`FinHeaderParser.cs`:

```csharp
using System.Text;
using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinHeaderParser : AbstractParser<FinHeader>
    {
        public FinHeaderParser(Stream stream)
            : base(stream) { }

        public override FinHeader Parse()
        {
            var line = ReadLine();
            if (!line.StartsWith(FinHeader.PREFIX, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Not a FIN file: the header doesn't start with \"Dweezil \"");
            }

            // Like the engine, only digits may follow the prefix
            var digits = line[FinHeader.PREFIX.Length..];
            if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
            {
                throw new InvalidDataException($"Not a FIN file: invalid version \"{digits}\"");
            }

            var header = new FinHeader(int.Parse(digits), line.Length + 1);
            if (!header.IsValid)
            {
                throw new InvalidDataException(
                    $"Unsupported FIN version {header.Version}, expected {FinHeader.SUPPORTED_VERSION}"
                );
            }

            return header;
        }

        private string ReadLine()
        {
            var bytes = new List<byte>();
            while (bytes.Count < FinHeader.MAX_LINE_LENGTH)
            {
                if (Reader.BaseStream.Position >= Reader.BaseStream.Length)
                {
                    throw new InvalidDataException("Not a FIN file: the header line has no line feed");
                }

                var b = Reader.ReadByte();
                if (b == (byte)'\n')
                {
                    return Encoding.ASCII.GetString(bytes.ToArray());
                }
                bytes.Add(b);
            }

            throw new InvalidDataException(
                $"Not a FIN file: no line feed within the first {FinHeader.MAX_LINE_LENGTH} bytes"
            );
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~FinHeaderParserTests`
Expected: all PASS.

- [ ] **Step 5: Start `docs/formats/fin.md`**

Follow the tone of `docs/formats/string-tables.md`. Sections: introduction (what FIN files are, `Fin.pac`, the
naming prefixes `E`/`U`/`OG`/`B`/`T`/`P`/`BA`/`CU` as seen in the file list, with anything about their meaning marked
as inferred), `## Header` (the line, the fixed version, that the standard NetImmerse copyright lines are absent and
why: Digital Domain replaced the header), `## Blocks` (a heading with "Documented in the following sections" for now).

- [ ] **Step 6: Commit**

```bash
git add Malumware.BetaTeam.Lib/IO/Fin Malumware.BetaTeam.Lib.Tests/IO/Fin docs/formats/fin.md
git commit -m "Add FIN header parser" -m "FIN files start with a Digital Domain header line (Dweezil 23) that replaces the NetImmerse one. The engine accepts only this exact version." -m "<trailers>"
```

(`<trailers>` = the two attribution lines from Global Constraints, in every commit of this plan.)

---

### Task 2: Block model and block reader

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinUnknownClassException.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinMatrix3.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinRef.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinBlockReader.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinBlockRegistry.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Blocks/NiObject.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Blocks/NiExtraData.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinStreamBuilder.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/TestBlocks.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinBlockReaderTests.cs`

**Interfaces:**
- Produces:
  - `FinUnknownClassException(string className, long offset) : InvalidDataException` with `ClassName`, `Offset`.
  - `readonly record struct FinMatrix3(float M11 … M33)` in file order, `static Identity`.
  - `abstract class FinRef { uint LinkId; NiObject? Target; bool IsNull; abstract Type TargetType; }` and
    `sealed class FinRef<T> : FinRef where T : NiObject { new T? Target; }`.
  - `FinBlockReader(BinaryReader reader, FinBlockRegistry registry)` with `Position`, `Length`, `ReadByte()`,
    `ReadUInt16()`, `ReadUInt32()`, `ReadInt32()`, `ReadSingle()`, `ReadVector2()`, `ReadVector3()`, `ReadMatrix3()`,
    `ReadSizedString(int maxLength)`, `ReadCString()`, `ReadCount(int elementSize)`, `ReadRef<T>()`,
    `ReadRefList<T>()`, `ReadArray<T>(int count, Func<FinBlockReader, T> read)`, internal `ReadExtraDataList()`,
    internal `Refs`.
  - `FinBlockRegistry` with `static Default`, `RegisterBlock<T>()`, `RegisterExtraData<T>()` (class name =
    `typeof(T).Name`), internal `CreateBlock(string)`, `CreateExtraData(string)`, `BlockClassNames`,
    `ExtraDataClassNames`.
  - `abstract class NiObject { string ClassName; long Offset; uint LinkId; string? Name;
    IReadOnlyList<NiExtraData> ExtraData; internal virtual void Load(FinBlockReader reader); }`.
  - `abstract class NiExtraData { string ClassName; long Offset; internal virtual void Load(FinBlockReader reader); }`.
  - Test helper `FinStreamBuilder` (fluent, see below).

- [ ] **Step 1: Write the test helper and test blocks**

`FinStreamBuilder.cs`:

```csharp
using System.Text;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    internal sealed class FinStreamBuilder
    {
        private readonly MemoryStream _stream = new();
        private readonly BinaryWriter _writer;

        public FinStreamBuilder()
        {
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
        }

        public FinStreamBuilder Header(int version = 23)
        {
            _writer.Write(Encoding.ASCII.GetBytes($"Dweezil {version}\n"));
            return this;
        }

        // BinaryWriter.Write(string) uses a 7-bit length prefix, FIN files use a u32
        public FinStreamBuilder SizedString(string value)
        {
            _writer.Write(value.Length);
            _writer.Write(Encoding.ASCII.GetBytes(value));
            return this;
        }

        public FinStreamBuilder CString(string? value)
        {
            if (value is null)
            {
                _writer.Write(0);
                return this;
            }
            return SizedString(value);
        }

        public FinStreamBuilder Byte(byte value)
        {
            _writer.Write(value);
            return this;
        }

        public FinStreamBuilder UInt16(ushort value)
        {
            _writer.Write(value);
            return this;
        }

        public FinStreamBuilder UInt32(uint value)
        {
            _writer.Write(value);
            return this;
        }

        public FinStreamBuilder Int32(int value)
        {
            _writer.Write(value);
            return this;
        }


        public FinStreamBuilder Floats(params float[] values)
        {
            foreach (var value in values)
            {
                _writer.Write(value);
            }
            return this;
        }

        public FinStreamBuilder Refs(params uint[] linkIds)
        {
            _writer.Write((uint)linkIds.Length);
            foreach (var linkId in linkIds)
            {
                _writer.Write(linkId);
            }
            return this;
        }

        public FinStreamBuilder TopLevel() => SizedString("Top Level Object");
        public FinStreamBuilder EndOfFile() => SizedString("End Of File");

        // The NiObject part every block starts with, without extra data
        public FinStreamBuilder NiObject(uint linkId, string? name = null)
        {
            return UInt32(linkId).CString(name).UInt32(0);
        }

        public byte[] ToArray()
        {
            _writer.Flush();
            return _stream.ToArray();
        }
    }
}
```

`TestBlocks.cs` (test-only classes, used by parser and linker tests):

```csharp
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    internal sealed class TestBlock : NiObject
    {
        public uint Value { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Value = reader.ReadUInt32();
        }
    }

    internal sealed class TestParentBlock : NiObject
    {
        public FinRef<TestBlock> Child { get; private set; } = null!;

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Child = reader.ReadRef<TestBlock>();
        }
    }

    internal sealed class TestExtraData : NiExtraData
    {
        public uint Value { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            Value = reader.ReadUInt32();
        }
    }

    internal static class TestRegistry
    {
        public static FinBlockRegistry Create()
        {
            return new FinBlockRegistry()
                .RegisterBlock<TestBlock>()
                .RegisterBlock<TestParentBlock>()
                .RegisterExtraData<TestExtraData>();
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`FinBlockReaderTests.cs`:

```csharp
using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinBlockReaderTests
    {
        private static FinBlockReader CreateReader(byte[] bytes)
        {
            return new FinBlockReader(new BinaryReader(new MemoryStream(bytes)), TestRegistry.Create());
        }

        [Fact]
        public void ReadCString_ReturnsNull_WhenLengthIsZero()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().CString(null).ToArray());

            // Act
            var value = reader.ReadCString();

            // Assert
            Assert.Null(value);
            Assert.Equal(4, reader.Position);
        }

        [Fact]
        public void ReadCString_ReturnsText_WhenLengthIsPositive()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().CString("Box01").ToArray());

            // Act
            var value = reader.ReadCString();

            // Assert
            Assert.Equal("Box01", value);
        }

        [Fact]
        public void ReadSizedString_ThrowsInvalidDataException_WhenLengthExceedsMaximum()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().UInt32(0x0A96AB80).ToArray());

            // Act
            var act = () => reader.ReadSizedString(64);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Fact]
        public void ReadCount_ThrowsInvalidDataException_WhenCountExceedsRemainingBytes()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().UInt32(1_000_000).UInt32(0).ToArray());

            // Act
            var act = () => reader.ReadCount(4);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Fact]
        public void ReadMatrix3_ReadsNineFloatsInFileOrder()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().Floats(1, 2, 3, 4, 5, 6, 7, 8, 9).ToArray());

            // Act
            var matrix = reader.ReadMatrix3();

            // Assert
            Assert.Equal(new FinMatrix3(1, 2, 3, 4, 5, 6, 7, 8, 9), matrix);
        }

        [Fact]
        public void ReadVector3_ReadsThreeFloats()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().Floats(1, 2, 3).ToArray());

            // Act
            var vector = reader.ReadVector3();

            // Assert
            Assert.Equal(new Vector3(1, 2, 3), vector);
        }

        [Fact]
        public void ReadRefList_ReturnsUnresolvedRefs_WithNullForZero()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().Refs(0x10, 0).ToArray());

            // Act
            var refs = reader.ReadRefList<TestBlock>();

            // Assert
            Assert.Equal(2, refs.Count);
            Assert.Equal(0x10u, refs[0].LinkId);
            Assert.True(refs[1].IsNull);
            Assert.Equal(2, reader.Refs.Count);
        }

        [Fact]
        public void ReadExtraDataList_ReadsRegisteredExtraData()
        {
            // Arrange
            var bytes = new FinStreamBuilder().UInt32(1).CString("TestExtraData").UInt32(42).ToArray();
            var reader = CreateReader(bytes);

            // Act
            var extraData = reader.ReadExtraDataList();

            // Assert
            var single = Assert.IsType<TestExtraData>(Assert.Single(extraData));
            Assert.Equal(42u, single.Value);
            Assert.Equal("TestExtraData", single.ClassName);
        }

        [Fact]
        public void ReadExtraDataList_ThrowsUnknownClass_WhenExtraDataIsNotRegistered()
        {
            // Arrange
            var bytes = new FinStreamBuilder().UInt32(1).CString("NiStringExtraData").UInt32(0).ToArray();
            var reader = CreateReader(bytes);

            // Act
            var act = () => reader.ReadExtraDataList();

            // Assert
            var exception = Assert.Throws<FinUnknownClassException>(act);
            Assert.Equal("NiStringExtraData", exception.ClassName);
            Assert.Equal(4, exception.Offset);
        }

        [Fact]
        public void ReadExtraDataList_UsesNiExtraData_WhenClassNameIsNull()
        {
            // Arrange
            var bytes = new FinStreamBuilder().UInt32(1).CString(null).ToArray();
            var reader = CreateReader(bytes);

            // Act
            var act = () => reader.ReadExtraDataList();

            // Assert
            var exception = Assert.Throws<FinUnknownClassException>(act);
            Assert.Equal("NiExtraData", exception.ClassName);
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~FinBlockReaderTests`
Expected: build errors, types not found.

- [ ] **Step 4: Implement**

`FinUnknownClassException.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Blocks carry no size, so an unknown class can't be skipped
    public class FinUnknownClassException : InvalidDataException
    {
        public string ClassName { get; }
        public long Offset { get; }

        public FinUnknownClassException(string className, long offset)
            : base($"Unknown class \"{className}\" at offset 0x{offset:X}")
        {
            ClassName = className;
            Offset = offset;
        }
    }
}
```

`FinMatrix3.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Nine floats as stored in the file: three groups of three. Whether a group is a row or a column in the
    // engine's maths is for exporters to decide; the reader keeps file order.
    public readonly record struct FinMatrix3(
        float M11, float M12, float M13,
        float M21, float M22, float M23,
        float M31, float M32, float M33)
    {
        public static FinMatrix3 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);
    }
}
```

`FinRef.cs`:

```csharp
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // A reference to another block, stored in the file as that block's link ID (its original memory address)
    public abstract class FinRef
    {
        public uint LinkId { get; }
        public NiObject? Target { get; private set; }
        public bool IsNull => LinkId == 0;
        public abstract Type TargetType { get; }

        protected FinRef(uint linkId)
        {
            LinkId = linkId;
        }

        internal void Resolve(NiObject target)
        {
            Target = target;
        }
    }

    public sealed class FinRef<T> : FinRef where T : NiObject
    {
        public new T? Target => (T?)base.Target;
        public override Type TargetType => typeof(T);

        public FinRef(uint linkId)
            : base(linkId) { }
    }
}
```

`Blocks/NiObject.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Unlike later NetImmerse versions, the name and extra data live on NiObject itself
    public abstract class NiObject
    {
        public string ClassName { get; internal set; } = "";
        public long Offset { get; internal set; }
        public uint LinkId { get; private set; }
        public string? Name { get; private set; }
        public IReadOnlyList<NiExtraData> ExtraData { get; private set; } = [];

        internal virtual void Load(FinBlockReader reader)
        {
            LinkId = reader.ReadUInt32();
            Name = reader.ReadCString();
            ExtraData = reader.ReadExtraDataList();
        }
    }
}
```

`Blocks/NiExtraData.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Extra data is stored inline in its owner, not as a separate block
    public abstract class NiExtraData
    {
        public string ClassName { get; internal set; } = "";
        public long Offset { get; internal set; }

        internal virtual void Load(FinBlockReader reader) { }
    }
}
```

`FinBlockRegistry.cs`:

```csharp
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public sealed class FinBlockRegistry
    {
        public static FinBlockRegistry Default { get; } = CreateDefault();

        private readonly Dictionary<string, Func<NiObject>> _blocks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<NiExtraData>> _extraData = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> BlockClassNames => _blocks.Keys;
        public IReadOnlyCollection<string> ExtraDataClassNames => _extraData.Keys;

        // Block classes are named after the engine classes, so the C# name is the name in the file
        public FinBlockRegistry RegisterBlock<T>() where T : NiObject, new()
        {
            _blocks[typeof(T).Name] = () => new T();
            return this;
        }

        public FinBlockRegistry RegisterExtraData<T>() where T : NiExtraData, new()
        {
            _extraData[typeof(T).Name] = () => new T();
            return this;
        }

        internal NiObject? CreateBlock(string className)
        {
            return _blocks.TryGetValue(className, out var create) ? create() : null;
        }

        internal NiExtraData? CreateExtraData(string className)
        {
            return _extraData.TryGetValue(className, out var create) ? create() : null;
        }

        private static FinBlockRegistry CreateDefault()
        {
            // Class group tasks add their registrations here
            return new FinBlockRegistry();
        }
    }
}
```

`FinBlockReader.cs`:

```csharp
using System.Numerics;
using System.Text;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public sealed class FinBlockReader
    {
        // Longest names and texture paths are well below this; a larger length means the stream is out of step
        public const int MAX_CSTRING_LENGTH = 1024;

        private const string BASE_EXTRA_DATA_CLASS = "NiExtraData";

        private readonly BinaryReader _reader;
        private readonly FinBlockRegistry _registry;
        private readonly List<FinRef> _refs = [];

        internal IReadOnlyList<FinRef> Refs => _refs;

        public long Position => _reader.BaseStream.Position;
        public long Length => _reader.BaseStream.Length;

        public FinBlockReader(BinaryReader reader, FinBlockRegistry registry)
        {
            _reader = reader;
            _registry = registry;
        }

        public byte ReadByte() => _reader.ReadByte();
        public ushort ReadUInt16() => _reader.ReadUInt16();
        public uint ReadUInt32() => _reader.ReadUInt32();
        public int ReadInt32() => _reader.ReadInt32();
        public float ReadSingle() => _reader.ReadSingle();

        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        public FinMatrix3 ReadMatrix3()
        {
            return new FinMatrix3(
                ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle());
        }

        // u32 length + bytes, no terminator, no null case (class names and markers)
        public string ReadSizedString(int maxLength)
        {
            var offset = Position;
            var length = _reader.ReadUInt32();
            if (length > maxLength || length > Length - Position)
            {
                throw new InvalidDataException($"Invalid string length {length} at offset 0x{offset:X}");
            }
            return Encoding.ASCII.GetString(_reader.ReadBytes((int)length));
        }

        // u32 length + bytes; a length of zero (or negative) means null
        public string? ReadCString()
        {
            var offset = Position;
            var length = _reader.ReadInt32();
            if (length <= 0)
            {
                return null;
            }
            if (length > MAX_CSTRING_LENGTH || length > Length - Position)
            {
                throw new InvalidDataException($"Invalid string length {length} at offset 0x{offset:X}");
            }
            return Encoding.Latin1.GetString(_reader.ReadBytes(length));
        }

        // Guards against allocating huge arrays when the stream is out of step
        public int ReadCount(int elementSize)
        {
            var offset = Position;
            var count = _reader.ReadUInt32();
            if (count > (Length - Position) / elementSize)
            {
                throw new InvalidDataException(
                    $"Count {count} at offset 0x{offset:X} exceeds the {Length - Position} remaining bytes"
                );
            }
            return (int)count;
        }

        public T[] ReadArray<T>(int count, Func<FinBlockReader, T> read)
        {
            var values = new T[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = read(this);
            }
            return values;
        }

        public FinRef<T> ReadRef<T>() where T : NiObject
        {
            var reference = new FinRef<T>(ReadUInt32());
            _refs.Add(reference);
            return reference;
        }

        public IReadOnlyList<FinRef<T>> ReadRefList<T>() where T : NiObject
        {
            var count = ReadCount(sizeof(uint));
            return ReadArray(count, r => r.ReadRef<T>());
        }

        internal IReadOnlyList<NiExtraData> ReadExtraDataList()
        {
            var count = ReadCount(sizeof(uint));
            var list = new List<NiExtraData>(count);
            for (var i = 0; i < count; i++)
            {
                var offset = Position;
                // A missing class name makes the engine fall back to the base NiExtraData loader
                var className = ReadCString() ?? BASE_EXTRA_DATA_CLASS;
                var extraData = _registry.CreateExtraData(className)
                    ?? throw new FinUnknownClassException(className, offset);
                extraData.ClassName = className;
                extraData.Offset = offset;
                extraData.Load(this);
                list.Add(extraData);
            }
            return list;
        }
    }
}
```

Expression-bodied members (`=> _reader.ReadByte()`) are fine: the braces rule is about `if`/`for`/`foreach`/`while`
bodies.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~FinBlockReaderTests`
Expected: all PASS.

- [ ] **Step 6: Commit**

Subject: `Add FIN block model and block reader`. Body: blocks mirror the engine classes; `NiObject` holds the link ID,
name and inline extra data in this engine version; the reader guards lengths and counts so a misread fails fast.

---

### Task 3: Stream parser, FinFile and FinReader

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinFile.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinStreamParser.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinReader.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinStreamParserTests.cs`

**Interfaces:**
- Consumes: Task 1 header types, Task 2 `FinBlockReader`, `FinBlockRegistry`, `NiObject`, `FinUnknownClassException`.
- Produces:
  - `FinFile(string name, FinHeader header, IReadOnlyList<NiObject> objects, IReadOnlyList<NiObject> topLevelObjects)`.
  - `FinStreamParser(Stream stream, string name, FinHeader header, FinBlockRegistry registry) :
    AbstractParser<FinFile>`. The stream must be positioned after the header (`Seek(header.Size, SeekOrigin.Begin)`).
  - `FinReader.Read(string filePath)` (instance) and `internal static FinReader.Read(string name, byte[] bytes,
    FinBlockRegistry? registry = null)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinStreamParserTests
    {
        private static FinFile Read(byte[] bytes)
        {
            return FinReader.Read("TEST", bytes, TestRegistry.Create());
        }

        [Fact]
        public void Read_ReturnsBlocksInFileOrder_WhenStreamIsValid()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10, "First").UInt32(1)
                .TopLevel().SizedString("TestBlock").NiObject(0x20, "Second").UInt32(2)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            Assert.Equal("TEST", file.Name);
            Assert.Equal(23, file.Header.Version);
            Assert.Equal(2, file.Objects.Count);
            var first = Assert.IsType<TestBlock>(file.Objects[0]);
            Assert.Equal("TestBlock", first.ClassName);
            Assert.Equal(0x10u, first.LinkId);
            Assert.Equal("First", first.Name);
            Assert.Equal(1u, first.Value);
            Assert.Equal(11, first.Offset);
            Assert.Same(file.Objects[1], Assert.Single(file.TopLevelObjects));
        }

        [Fact]
        public void Read_ThrowsUnknownClass_WithClassNameAndOffset_WhenClassIsNotRegistered()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .SizedString("NiMystery").NiObject(0x20)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<FinUnknownClassException>(act);
            Assert.Equal("NiMystery", exception.ClassName);
            Assert.Equal(11 + 4 + 9 + 12 + 4, exception.Offset);    // header, class name, NiObject, value
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenBytesFollowEndOfFile()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header().EndOfFile().Byte(0).ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("1 bytes", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WithOffset_WhenFileEndsInsideBlock()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10)
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.IsNotType<FinUnknownClassException>(exception);
            Assert.Contains("TestBlock", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenEndOfFileMarkerIsMissing()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~FinStreamParserTests`
Expected: build errors.

- [ ] **Step 3: Implement**

`FinFile.cs`:

```csharp
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinFile
    {
        public string Name { get; }
        public FinHeader Header { get; }
        public IReadOnlyList<NiObject> Objects { get; }
        public IReadOnlyList<NiObject> TopLevelObjects { get; }

        public FinFile(
            string name,
            FinHeader header,
            IReadOnlyList<NiObject> objects,
            IReadOnlyList<NiObject> topLevelObjects)
        {
            Name = name;
            Header = header;
            Objects = objects;
            TopLevelObjects = topLevelObjects;
        }
    }
}
```

`FinStreamParser.cs`:

```csharp
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinStreamParser : AbstractParser<FinFile>
    {
        public const string TOP_LEVEL_OBJECT = "Top Level Object";
        public const string END_OF_FILE = "End Of File";

        // The longest class name in the shipped files is about 25 characters
        public const int MAX_CLASS_NAME_LENGTH = 64;

        private readonly string _name;
        private readonly FinHeader _header;
        private readonly FinBlockRegistry _registry;

        public FinStreamParser(Stream stream, string name, FinHeader header, FinBlockRegistry registry)
            : base(stream)
        {
            _name = name;
            _header = header;
            _registry = registry;
        }

        public override FinFile Parse()
        {
            var reader = new FinBlockReader(Reader, _registry);
            var objects = new List<NiObject>();
            var topLevelObjects = new List<NiObject>();
            var current = "class name";
            var currentOffset = reader.Position;

            try
            {
                while (true)
                {
                    current = "class name";
                    currentOffset = reader.Position;
                    var className = reader.ReadSizedString(MAX_CLASS_NAME_LENGTH);
                    if (className == END_OF_FILE)
                    {
                        break;
                    }

                    // The marker is followed by the class name of the object it marks
                    var isTopLevel = className == TOP_LEVEL_OBJECT;
                    if (isTopLevel)
                    {
                        currentOffset = reader.Position;
                        className = reader.ReadSizedString(MAX_CLASS_NAME_LENGTH);
                    }

                    var block = _registry.CreateBlock(className)
                        ?? throw new FinUnknownClassException(className, currentOffset);
                    current = className;
                    block.ClassName = className;
                    block.Offset = currentOffset;
                    block.Load(reader);

                    objects.Add(block);
                    if (isTopLevel)
                    {
                        topLevelObjects.Add(block);
                    }
                }
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidDataException(
                    $"Unexpected end of file while reading {current} at offset 0x{currentOffset:X}", e
                );
            }

            // The engine stops at the marker and ignores anything after it
            var leftover = reader.Length - reader.Position;
            if (leftover != 0)
            {
                throw new InvalidDataException(
                    $"{leftover} bytes left after End Of File at offset 0x{reader.Position:X}"
                );
            }

            return new FinFile(_name, _header, objects, topLevelObjects);
        }
    }
}
```

`FinReader.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinReader
    {
        public FinFile Read(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var bytes = File.ReadAllBytes(filePath);
            return Read(name, bytes);
        }

        internal static FinFile Read(string name, byte[] bytes, FinBlockRegistry? registry = null)
        {
            using var headerParser = new FinHeaderParser(new MemoryStream(bytes));
            var header = headerParser.Parse();

            using var streamParser = new FinStreamParser(
                new MemoryStream(bytes), name, header, registry ?? FinBlockRegistry.Default
            );
            streamParser.Seek(header.Size, SeekOrigin.Begin);
            return streamParser.Parse();
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~FinStreamParserTests`
Expected: all PASS.

- [ ] **Step 5: Document the framing**

In `docs/formats/fin.md`, replace the `## Blocks` placeholder text with the stream layout: class name as `u32` length
+ ASCII bytes, `Top Level Object` marker and what it means, `End Of File`, that blocks have no size field (so readers
must know every class), that trailing bytes after `End Of File` are ignored by the game (and that shipped files have
none, once Task 12 has verified it; until then write "is expected to"). Add a `### Strings` subsection: sized strings
vs. CStrings (`0` = no string).

- [ ] **Step 6: Commit**

Subject: `Add FIN stream parser`. Body: the block loop, markers, and why an unknown class or leftover bytes stop
parsing.

---

### Task 4: Linker

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/FinLinker.cs`
- Modify: `Malumware.BetaTeam.Lib/IO/Fin/FinStreamParser.cs` (call the linker before returning)
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinLinkerTests.cs`

**Interfaces:**
- Consumes: `FinRef`, `NiObject`, `FinBlockReader.Refs`.
- Produces: `internal static FinLinker.Link(IReadOnlyList<NiObject> objects, IReadOnlyList<FinRef> refs)`.

- [ ] **Step 1: Write the failing tests** (through `FinReader.Read` with `TestRegistry`, like Task 3)

```csharp
using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinLinkerTests
    {
        private static FinFile Read(byte[] bytes)
        {
            return FinReader.Read("TEST", bytes, TestRegistry.Create());
        }

        [Fact]
        public void Read_ResolvesReference_WhenTargetExists()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(7)
                .TopLevel().SizedString("TestParentBlock").NiObject(0x20).UInt32(0x10)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            var parent = Assert.IsType<TestParentBlock>(file.TopLevelObjects[0]);
            Assert.Same(file.Objects[0], parent.Child.Target);
        }

        [Fact]
        public void Read_ResolvesForwardReference_WhenTargetComesLater()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestParentBlock").NiObject(0x20).UInt32(0x10)
                .SizedString("TestBlock").NiObject(0x10).UInt32(7)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            var parent = Assert.IsType<TestParentBlock>(file.TopLevelObjects[0]);
            Assert.Same(file.Objects[1], parent.Child.Target);
        }

        [Fact]
        public void Read_LeavesTargetNull_WhenLinkIdIsZero()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestParentBlock").NiObject(0x20).UInt32(0)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            var parent = Assert.IsType<TestParentBlock>(file.Objects[0]);
            Assert.True(parent.Child.IsNull);
            Assert.Null(parent.Child.Target);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenReferenceIsDangling()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestParentBlock").NiObject(0x20).UInt32(0x99)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("0x00000099", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenTargetHasWrongType()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestParentBlock").NiObject(0x10).UInt32(0)
                .SizedString("TestParentBlock").NiObject(0x20).UInt32(0x10)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("TestBlock", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenLinkIdIsDuplicated()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .SizedString("TestBlock").NiObject(0x10).UInt32(2)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("Duplicate", exception.Message);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~FinLinkerTests`
Expected: the resolve tests FAIL (`Target` is null), the throw tests FAIL (no exception).

- [ ] **Step 3: Implement**

```csharp
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Same job as the engine's LinkObject pass: link IDs are the objects' original addresses
    internal static class FinLinker
    {
        public static void Link(IReadOnlyList<NiObject> objects, IReadOnlyList<FinRef> refs)
        {
            var byLinkId = new Dictionary<uint, NiObject>(objects.Count);
            foreach (var block in objects)
            {
                if (!byLinkId.TryAdd(block.LinkId, block))
                {
                    throw new InvalidDataException(
                        $"Duplicate link ID 0x{block.LinkId:X8} on {block.ClassName} at offset 0x{block.Offset:X}"
                    );
                }
            }

            foreach (var reference in refs)
            {
                if (reference.IsNull)
                {
                    continue;
                }

                if (!byLinkId.TryGetValue(reference.LinkId, out var target))
                {
                    throw new InvalidDataException($"Reference to missing link ID 0x{reference.LinkId:X8}");
                }

                if (!reference.TargetType.IsInstanceOfType(target))
                {
                    throw new InvalidDataException(
                        $"Link ID 0x{reference.LinkId:X8} is a {target.ClassName}, expected {reference.TargetType.Name}"
                    );
                }

                reference.Resolve(target);
            }
        }
    }
}
```

In `FinStreamParser.Parse`, before `return`: `FinLinker.Link(objects, reader.Refs);`

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~IO.Fin`
Expected: all PASS.

- [ ] **Step 5: Document links**

`docs/formats/fin.md`: a `### Links` subsection. Every block starts with a link ID, which is the object's memory
address when the file was saved. References store that value; `0` means no object. Reading therefore takes two
passes. Forward references occur (don't claim this until Task 12 has confirmed it in the data; the unit test proves
the reader supports it).

- [ ] **Step 6: Commit**

Subject: `Resolve links between FIN blocks`.

---

### Task 5: Game data progress test

**Files:**
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinGameDataTests.cs`

**Interfaces:**
- Consumes: `FinReader.Read(name, bytes)`, `FinUnknownClassException`, `PacArchiveReader`, `GameDataPaths`.
- Produces: `PENDING_CLASSES`, which every class group task shrinks.

- [ ] **Step 1: Write the test**

```csharp
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;
using Xunit.Abstractions;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinGameDataTests
    {
        // Classes that don't have a reader yet. A file may only stop at one of these; any other failure,
        // including an unknown name that isn't listed, usually means an earlier block was misread.
        // Each class group removes its names. A name is added only after confirming the engine registers it.
        private static readonly HashSet<string> PENDING_CLASSES =
        [
            "NiNode", "NiTriShape", "NiEnvMappedTriShape",
            "NiLODNode", "NiBillboardNode", "NiLight",
            "NiMaterialProperty", "NiAlphaProperty", "NiTextureProperty", "NiTextureModeProperty",
            "NiMultiTextureProperty", "NiVertexColorProperty", "NiZBufferProperty", "NiSpecularProperty",
            "NiShadeProperty", "NiImage", "NiFlipTextures",
            "NiExtraData", "Ni3dsPropAnimExtraData", "Ni3dsAnimationNode", "Ni3dsBone", "Ni3dsSkin",
            "Ni3dsMorphShape", "Ni3dsColorAnimator", "Ni3dsAlphaAnimator",
            "DDUnit", "DDActorSharedData", "DDEnv", "DDCorona",
        ];

        private readonly ITestOutputHelper _output;

        public FinGameDataTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [GameDataFact]
        public void Read_ParsesOrStopsAtPendingClass_ForEveryShippedFile()
        {
            // Arrange
            var failures = new List<string>();
            var stoppedAt = new Dictionary<string, int>();
            var parsed = 0;
            var count = 0;

            // Act
            foreach (var (name, data) in FinFiles())
            {
                count++;
                try
                {
                    FinReader.Read(name, data);
                    parsed++;
                }
                catch (FinUnknownClassException e) when (PENDING_CLASSES.Contains(e.ClassName))
                {
                    stoppedAt[e.ClassName] = stoppedAt.GetValueOrDefault(e.ClassName) + 1;
                }
                catch (InvalidDataException e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            // Progress, visible with `dotnet test --logger "console;verbosity=detailed"`
            _output.WriteLine($"{parsed} of {count} files parse completely");
            foreach (var (className, files) in stoppedAt.OrderByDescending(pair => pair.Value))
            {
                _output.WriteLine($"  {files,4} stop at {className}");
            }

            // Assert
            Assert.NotEqual(0, count);
            Assert.Empty(failures);
        }

        internal static IEnumerable<(string Name, byte[] Data)> FinFiles()
        {
            var reader = new PacArchiveReader();
            foreach (var path in GameDataPaths.Archives())
            {
                if (!Path.GetFileName(path).Equals("Fin.pac", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var archive = reader.Read(path);
                foreach (var (entry, data) in archive.Entries.OrderBy(pair => pair.Key.FileName))
                {
                    if (entry.FileName.EndsWith(".FIN", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return (Path.GetFileNameWithoutExtension(entry.FileName), data);
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run it**

Run: `dotnet test --filter FullyQualifiedName~FinGameDataTests --logger "console;verbosity=detailed"`
Expected: PASS, output "0 of 147 files parse completely" with all files stopping at the first pending class. If a
file fails with anything else, the framing is wrong: stop and investigate before continuing.

- [ ] **Step 3: Commit**

Subject: `Add FIN game data test with pending class list`. Body: explain the pending list and that it shrinks with
every class group.

---

### Task 6: NiAVObject and NiNode

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Blocks/NiAVObject.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Blocks/NiNode.cs`
- Modify: `Malumware.BetaTeam.Lib/IO/Fin/FinBlockRegistry.cs` (`CreateDefault`)
- Modify: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinGameDataTests.cs` (remove `NiNode` from `PENDING_CLASSES`)
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/Blocks/NiNodeTests.cs`
- Modify: `docs/formats/fin.md`, `Research/notes/fin.md`

**Interfaces:**
- Produces: `NiAVObject : NiObject` with `Unknown2C` (byte), `Translation` (Vector3), `Rotation` (FinMatrix3), `Scale`
  (float), `Velocity` (Vector3), `Properties` (`IReadOnlyList<FinRef<NiObject>>`, narrowed to `NiProperty` in
  Task 9), `Unknown7C` (uint). `NiNode : NiAVObject` with `UnknownC0`, `UnknownC4` (uint), `UnknownC9` (byte),
  `Children` (`IReadOnlyList<FinRef<NiAVObject>>`), `Effects` (`IReadOnlyList<FinRef<NiObject>>`, narrowed to
  `NiLight` in Task 8 if the engine only accepts lights there).
- Unknown fields are named after their offset in the engine object. Rename one only when the code shows what it is.

- [ ] **Step 1: Research the unknown fields and the bounding volume**

In Ghidra (`NiMain.dll`): look for functions reading `NiAVObject` +0x2c/+0x7c and `NiNode` +0xc0/+0xc4/+0xc9 (e.g.
getters, `UpdateWorldData`, `Update`). Decompile `NiBoundingVolume::CreateFromStream`. Write findings to
`Research/notes/fin.md`. If a field's purpose is clear, use that name instead of `UnknownXX` in the steps below.

- [ ] **Step 2: Write the failing tests**

```csharp
using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class NiNodeTests
    {
        private static FinStreamBuilder WriteNiNode(
            FinStreamBuilder builder, uint linkId, string name, uint[] children, uint hasBound = 0)
        {
            return builder.SizedString("NiNode").NiObject(linkId, name)
                .Byte(0)                                // unknown
                .Floats(1, 2, 3)                        // translation
                .Floats(1, 0, 0, 0, 1, 0, 0, 0, 1)      // rotation
                .Floats(2)                              // scale
                .Floats(0, 0, 0)                        // velocity
                .Refs()                                 // properties
                .UInt32(3)                              // unknown
                .UInt32(hasBound)
                .UInt32(2).UInt32(0).Byte(1)            // unknowns
                .Refs(children)
                .Refs();                                // effects
        }

        [Fact]
        public void Read_ReturnsNodeFields_WhenNodeIsValid()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            var bytes = WriteNiNode(builder, 0x10, "Box01", []).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var node = Assert.IsType<NiNode>(Assert.Single(file.TopLevelObjects));
            Assert.Equal("Box01", node.Name);
            Assert.Equal(new Vector3(1, 2, 3), node.Translation);
            Assert.Equal(FinMatrix3.Identity, node.Rotation);
            Assert.Equal(2f, node.Scale);
            Assert.Equal(3u, node.Unknown7C);
            Assert.Equal(2u, node.UnknownC0);
            Assert.Equal(1, node.UnknownC9);
            Assert.Empty(node.Children);
        }

        [Fact]
        public void Read_LinksChildNode_WhenChildFollows()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            builder = WriteNiNode(builder, 0x10, "Root", [0x20]);
            var bytes = WriteNiNode(builder, 0x20, "Child", []).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var root = Assert.IsType<NiNode>(file.TopLevelObjects[0]);
            Assert.Same(file.Objects[1], Assert.Single(root.Children).Target);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenNodeHasBoundingVolume()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header();
            var bytes = WriteNiNode(builder, 0x10, "Box01", [], hasBound: 1).EndOfFile().ToArray();

            // Act
            var act = () => FinReader.Read("TEST", bytes);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
```

If Step 1 shows that shipped files do contain bounding volumes (the game data test in Step 5 tells you), replace the
last test with one that reads a bounding volume built according to `NiBoundingVolume::CreateFromStream`, and
implement it instead of throwing.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~NiNodeTests`
Expected: build errors.

- [ ] **Step 4: Implement**

`Blocks/NiAVObject.cs`:

```csharp
using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiAVObject : NiObject
    {
        public byte Unknown2C { get; private set; }
        public Vector3 Translation { get; private set; }
        public FinMatrix3 Rotation { get; private set; }
        public float Scale { get; private set; }

        // Inferred: NetImmerse 3.x stores a velocity here
        public Vector3 Velocity { get; private set; }

        public IReadOnlyList<FinRef<NiObject>> Properties { get; private set; } = [];
        public uint Unknown7C { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Unknown2C = reader.ReadByte();
            Translation = reader.ReadVector3();
            Rotation = reader.ReadMatrix3();
            Scale = reader.ReadSingle();
            Velocity = reader.ReadVector3();
            Properties = reader.ReadRefList<NiObject>();
            Unknown7C = reader.ReadUInt32();

            if (reader.ReadUInt32() != 0)
            {
                throw new InvalidDataException(
                    $"{ClassName} at offset 0x{Offset:X} has a bounding volume, which isn't supported"
                );
            }
        }
    }
}
```

`Blocks/NiNode.cs`:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiNode : NiAVObject
    {
        public uint UnknownC0 { get; private set; }
        public uint UnknownC4 { get; private set; }
        public byte UnknownC9 { get; private set; }

        // Empty slots are stored as link ID 0
        public IReadOnlyList<FinRef<NiAVObject>> Children { get; private set; } = [];
        public IReadOnlyList<FinRef<NiObject>> Effects { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            UnknownC0 = reader.ReadUInt32();
            UnknownC4 = reader.ReadUInt32();
            UnknownC9 = reader.ReadByte();
            Children = reader.ReadRefList<NiAVObject>();
            Effects = reader.ReadRefList<NiObject>();
        }
    }
}
```

`FinBlockRegistry.CreateDefault`:

```csharp
return new FinBlockRegistry()
    .RegisterBlock<NiNode>();
```

Remove `"NiNode"` from `PENDING_CLASSES`.

- [ ] **Step 5: Run all tests, including game data**

Run: `dotnet test --logger "console;verbosity=detailed"`
Expected: unit tests PASS. Game data test PASS, with files now stopping at later classes. Any `InvalidDataException`
that isn't a pending class means the `NiAVObject`/`NiNode` layout is wrong for some file: investigate with the code
and the file before continuing.

- [ ] **Step 6: Document**

`docs/formats/fin.md`: sections `### NiObject` (link ID, name, inline extra data, and that name/extra data sit on
`NiObject` in this version rather than `NiObjectNET` as in later NetImmerse), `### NiAVObject` (field table with
types; transform explained: translation, 3×3 rotation stored as three groups of three floats, uniform scale; the
note that vertices are in the object's local space and a world position combines all parent transforms, which is
why flattening without them puts parts at the origin), `### NiNode`. Unknown fields listed as unknown, with observed
values if helpful.

- [ ] **Step 7: Commit**

Subject: `Read NiAVObject and NiNode`.

---

### Tasks 7–11: Class groups

These tasks follow the same pattern as Task 6. The plan can't list the fields: finding them is the work. Each task
defines the procedure, the classes and the functions to start from. The shape of the code, tests and docs is as in
Task 6.

**Procedure per class (the same in every group task):**

1. **Code:** decompile `LoadBinary` and `LinkObject` for the class and every base class not yet implemented. Write
   the field list (order, type, engine offset, what it's used for if visible) and addresses to
   `Research/notes/fin.md`. Also check `CreateObject`/the registration for the exact class name string.
2. **Test:** in `Malumware.BetaTeam.Lib.Tests/IO/Fin/Blocks/<Group>Tests.cs`, add a `FinStreamBuilder` helper method
   that writes one block of the class field by field (one commented builder call per field, as in `NiNodeTests`),
   and tests: `Read_Returns<Class>Fields_When<Class>IsValid` asserting every field, plus one test per branch in the
   loader (e.g. `Read_ReadsNormals_WhenHasNormalsIsSet`), plus a link test for every reference field.
3. **Run** the tests, see them fail.
4. **Implement** `Blocks/<Class>.cs`: public properties with private setters in file order; `Load` calls
   `base.Load(reader)` first, then reads in the engine's order. Counts use `reader.ReadCount(elementSize)` with the
   minimum bytes per element. Unknown fields are named `Unknown<offset>`. References use `FinRef<T>` with the most
   specific type the engine's `LinkObject` accepts. Register the class in `FinBlockRegistry.CreateDefault` (or
   `RegisterExtraData` for extra data). Remove it from `PENDING_CLASSES`.
5. **Run all tests** including game data. A non-pending failure means a layout is wrong: compare the code and the
   bytes at the reported offset before changing anything. If the parser reaches a class name that isn't on the
   pending list, check the engine registers it; if so, add it to the list and to a later group.
6. **Document** the class in `docs/formats/fin.md`: purpose, field table, meaning per field (inferred parts marked),
   and version branches that matter for version 23 only.
7. **Commit** once per class group (or per class, if a group gets large). Every commit passes all tests.

### Task 7: Geometry

- Classes: `NiTriShape` (bases `NiGeometry`, `NiTriBasedGeom`), `NiEnvMappedTriShape`.
- Start: `NiTriShape::LoadBinary` 10067df0, `NiTriBasedGeom::LoadBinary` 1005dd20, `NiGeometry::LoadBinary` 1002ff90,
  `NiGeometry::LinkObject` 100300b0, `NiEnvMappedTriShape::LoadBinary` 1002bd00.
- No `NiTriShapeData` class showed up in the name scan. Geometry is probably inline in the shape: confirm.
- Vertices, normals, colours and UVs are arrays: store them as `Vector3[]`, `Vector2[]` and a colour record struct
  (`FinColorA(float R, float G, float B, float A)` in `IO/Fin/`, or `FinColor3` if the engine reads three floats).
  Triangles as a `record struct FinTriangle(ushort A, ushort B, ushort C)` if the engine reads `u16` indices.
- Extra check (data): every triangle index is below the vertex count. Add this as an assertion in a new
  `[GameDataFact]` in `FinGameDataTests` that walks every file that parses completely.

### Task 8: Other nodes

- Classes: `NiLODNode` (base `NiSwitchNode`), `NiBillboardNode`, `NiLight` (and its base, likely
  `NiDynamicEffect`).
- Start: `NiLODNode::LoadBinary` 10034e30, `NiSwitchNode::LoadBinary` 10057910, `NiBillboardNode::LoadBinary`
  100174a0, `NiLight::LoadBinary` 100326c0. Find the concrete light classes (the name scan only shows `NiLight`,
  which may be abstract in the engine; the class name in the file decides what gets registered).
- Narrow `NiNode.Effects` to the type `NiNode::LinkObject` accepts.

### Task 9: Properties and textures

- Classes: `NiProperty` (base), `NiMaterialProperty`, `NiAlphaProperty`, `NiTextureProperty`,
  `NiTextureModeProperty`, `NiMultiTextureProperty`, `NiVertexColorProperty`, `NiZBufferProperty`,
  `NiSpecularProperty`, `NiShadeProperty`, `NiImage`, `NiFlipTextures`.
- Start: addresses under "Other addresses" in `Research/notes/fin.md`.
- Narrow `NiAVObject.Properties` to `FinRef<NiProperty>`.
- For `NiImage`: record whether texture file names are stored (external TGA) or pixel data is embedded
  (`NiRawImageData`). This matters for the texture milestone.

### Task 10: Extra data and Ni3ds animation classes

- Classes: `NiExtraData` fallback (null class name), `Ni3dsPropAnimExtraData`, `Ni3dsAnimationNode`, `Ni3dsBone`,
  `Ni3dsSkin`, `Ni3dsMorphShape`, `Ni3dsColorAnimator`, `Ni3dsAlphaAnimator`, and any extra data class the parser
  reaches.
- Start: `NiExtraData::LoadBinary` 1002bf60, `NiExtraData::CreateFromStream`; the `Ni3ds*` loaders in
  `NiAnimation.dll`.
- Read these completely, but document only structure: meaning of animation data is milestone 3. Skin and morph data
  matter for geometry export later: note which fields hold bone weights and morph targets if it's visible.
- `NiExtraData` is abstract in the C# model from Task 2. If the engine's fallback for a null class name reads a
  plain `NiExtraData`, remove `abstract` and register it with `RegisterExtraData<NiExtraData>()`, so the fallback
  resolves and the dump shows the class name the engine uses.

### Task 11: Digital Domain classes

- Classes: `DDUnit`, `DDActorSharedData`, `DDEnv`, `DDCorona`.
- Start: search `LoadComp.dll` for `LoadBinary`/`LinkObject` in their namespaces (`search_symbols_by_name` with
  `DDUnit::`). `LegoLogicComp.dll` and the behaviour DLLs use them: follow cross-references to learn what each field
  does. This is where the "custom properties" questions get answered, so spend the time on meaning here: for every
  field, record where the game reads it and what it does with it.
- `docs/formats/fin.md` gets a section per class with a meaning for every field that has one, marked verified or
  inferred.

---

### Task 12: Full coverage

**Files:**
- Modify: `Malumware.BetaTeam.Lib.Tests/IO/Fin/FinGameDataTests.cs`
- Modify: `docs/formats/fin.md`

- [ ] **Step 1: Replace the pending mechanism with the final invariants**

Delete `PENDING_CLASSES`, the `FinUnknownClassException` catch and the progress output. The test becomes:

```csharp
[GameDataFact]
public void Read_ParsesCompletely_ForEveryShippedFile()
{
    // Arrange
    var failures = new List<string>();
    var count = 0;

    // Act
    foreach (var (name, data) in FinFiles())
    {
        count++;
        try
        {
            var file = FinReader.Read(name, data);
            if (file.TopLevelObjects.Count == 0)
            {
                failures.Add($"{name}: no top-level object");
            }
        }
        catch (InvalidDataException e)
        {
            failures.Add($"{name}: {e.Message}");
        }
    }

    // Assert
    Assert.NotEqual(0, count);
    Assert.Empty(failures);
}
```

Zero leftover bytes and resolved links are enforced by the reader itself (Tasks 3 and 4), so parsing without an
exception proves them. Keep any extra invariant tests added in group tasks (e.g. triangle indices).

- [ ] **Step 2: Run** `dotnet test`. Expected: PASS.
- [ ] **Step 3: Update docs** with the verified statements that were phrased as expectations in Tasks 3 and 4 (no
  trailing bytes; whether forward references occur, which you can check with a short throwaway script or a debugger;
  don't add a test just for this).
- [ ] **Step 4: Commit** `Verify every shipped FIN file parses completely`.

---

### Task 13: Dump model and builder

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Dump/FinDumpModel.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Dump/FinDumpBuilder.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/Dump/FinDumpBuilderTests.cs`

**Interfaces:**
- Consumes: `FinFile`, `NiObject`, `NiExtraData`, `FinRef`.
- Produces:

```csharp
namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    public record FinDump(string Name, int Version, IReadOnlyList<FinDumpObject> Roots,
        IReadOnlyList<FinDumpObject> Unreferenced);

    public abstract record FinDumpNode(string? Field);

    // A block (with LinkId) or inline extra data (LinkId null)
    public sealed record FinDumpObject(string? Field, string ClassName, uint? LinkId, long Offset,
        IReadOnlyList<FinDumpNode> Children) : FinDumpNode(Field);

    // A block that was already written in full earlier in the dump
    public sealed record FinDumpReference(string? Field, string ClassName, uint LinkId) : FinDumpNode(Field);

    public sealed record FinDumpValue(string? Field, object? Value) : FinDumpNode(Field);

    // A list of blocks or extra data
    public sealed record FinDumpList(string Field, IReadOnlyList<FinDumpNode> Items) : FinDumpNode(Field);

    // A list of plain values (vertices, indices), which writers may summarise
    public sealed record FinDumpArray(string Field, string ElementType, IReadOnlyList<object?> Values)
        : FinDumpNode(Field);
}
```

`FinDumpBuilder.Build(FinFile file) : FinDump`.

- [ ] **Step 1: Write the failing tests**

Use `TestBlock`/`TestParentBlock` and a new test-only `TestListBlock : NiObject` with
`IReadOnlyList<FinRef<NiObject>> Items` and `float[] Values` (add both to `TestBlocks.cs` and `TestRegistry`). Tests:

- `Build_WritesSharedBlockInFullOnce_WhenReferencedTwice`: two `TestListBlock` roots both listing link ID 0x10 → the
  first contains a `FinDumpObject` for 0x10, the second a `FinDumpReference` with `LinkId == 0x10`.
- `Build_Terminates_WhenReferencesFormCycle`: two `TestListBlock`s referencing each other, one top-level → the dump
  contains one full object each and one `FinDumpReference` back to the root.
- `Build_ListsUnreferencedBlocks_WhenNotReachableFromTopLevel`: a top-level `TestBlock` plus a second `TestBlock`
  nothing points to → `Unreferenced` holds the second.
- `Build_ReturnsArrayNode_ForValueArrays`: `Values` becomes a `FinDumpArray` with `ElementType == "Single"` and the
  values in order.
- `Build_OrdersFieldsFromBaseToDerived`: the first children of any block are `Name` and `ExtraData` (from `NiObject`),
  followed by the derived class's fields in declaration order. `ClassName`, `Offset` and `LinkId` are not children
  (they're on `FinDumpObject`).
- `Build_WritesNullReference_AsNullValue`: a `TestParentBlock` with link ID 0 → `FinDumpValue("Child", null)`.

Write each in full with Arrange/Act/Assert, building bytes with `FinStreamBuilder` and reading with
`FinReader.Read("TEST", bytes, TestRegistry.Create())`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~FinDumpBuilderTests`. Expected: build errors.

- [ ] **Step 3: Implement**

```csharp
using System.Collections;
using System.Reflection;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    public static class FinDumpBuilder
    {
        // Shown on FinDumpObject itself, not as fields
        private static readonly HashSet<string> HEADER_PROPERTIES =
            [nameof(NiObject.ClassName), nameof(NiObject.Offset), nameof(NiObject.LinkId)];

        public static FinDump Build(FinFile file)
        {
            var visited = new HashSet<NiObject>(ReferenceEqualityComparer.Instance);
            var roots = file.TopLevelObjects.Select(block => BuildBlock(null, block, visited)).ToList();

            var unreferenced = new List<FinDumpObject>();
            foreach (var block in file.Objects)
            {
                if (!visited.Contains(block))
                {
                    unreferenced.Add(BuildBlock(null, block, visited));
                }
            }

            return new FinDump(file.Name, file.Header.Version, roots, unreferenced);
        }

        private static FinDumpObject BuildBlock(string? field, NiObject block, HashSet<NiObject> visited)
        {
            // Mark before descending, so cycles end in a reference
            visited.Add(block);
            return new FinDumpObject(field, block.ClassName, block.LinkId, block.Offset, BuildFields(block, visited));
        }

        private static FinDumpNode BuildReference(string? field, FinRef reference, HashSet<NiObject> visited)
        {
            var target = reference.Target;
            if (target is null)
            {
                return new FinDumpValue(field, null);
            }
            if (visited.Contains(target))
            {
                return new FinDumpReference(field, target.ClassName, target.LinkId);
            }
            return BuildBlock(field, target, visited);
        }

        private static IReadOnlyList<FinDumpNode> BuildFields(object value, HashSet<NiObject> visited)
        {
            var nodes = new List<FinDumpNode>();
            foreach (var property in PropertiesBaseFirst(value.GetType()))
            {
                if (HEADER_PROPERTIES.Contains(property.Name))
                {
                    continue;
                }
                nodes.Add(BuildField(property.Name, property.GetValue(value), visited));
            }
            return nodes;
        }

        private static FinDumpNode BuildField(string field, object? value, HashSet<NiObject> visited)
        {
            switch (value)
            {
                case FinRef reference:
                    return BuildReference(field, reference, visited);
                case NiExtraData extraData:
                    return new FinDumpObject(field, extraData.ClassName, null, extraData.Offset,
                        BuildFields(extraData, visited));
                case string or null:
                    return new FinDumpValue(field, value);
                case IEnumerable items when IsBlockList(value.GetType()):
                    return new FinDumpList(field, items.Cast<object?>()
                        .Select(item => BuildField(field, item, visited) with { Field = null })
                        .ToList());
                case IEnumerable items:
                    return new FinDumpArray(field, ElementType(value.GetType()).Name,
                        items.Cast<object?>().ToList());
                default:
                    return new FinDumpValue(field, value);
            }
        }

        private static bool IsBlockList(Type type)
        {
            var element = ElementType(type);
            return typeof(FinRef).IsAssignableFrom(element) || typeof(NiExtraData).IsAssignableFrom(element);
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType()!;
            }
            var enumerable = type.GetInterfaces().Append(type)
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable.GetGenericArguments()[0];
        }

        // Reflection doesn't guarantee order, so walk the hierarchy and sort each level by declaration order
        private static IEnumerable<PropertyInfo> PropertiesBaseFirst(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            {
                hierarchy.Push(current);
            }

            foreach (var level in hierarchy)
            {
                var properties = level
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(property => property.GetIndexParameters().Length == 0)
                    .OrderBy(property => property.MetadataToken);
                foreach (var property in properties)
                {
                    yield return property;
                }
            }
        }
    }
}
```

`FinRef<T>` declares `new Target` and `TargetType`; they're on `FinRef`, not on blocks, so no special handling is
needed. The `with { Field = null }` works because all node types are records.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~FinDumpBuilderTests`. Expected: PASS.

- [ ] **Step 5: Commit** `Build a dump model of FIN files`.

---

### Task 14: Text and JSON writers

**Files:**
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Dump/FinDumpValueFormatter.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Dump/FinTextDumpWriter.cs`
- Create: `Malumware.BetaTeam.Lib/IO/Fin/Dump/FinJsonDumpWriter.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/Dump/FinTextDumpWriterTests.cs`
- Create: `Malumware.BetaTeam.Lib.Tests/IO/Fin/Dump/FinJsonDumpWriterTests.cs`

**Interfaces:**
- Consumes: the Task 13 model.
- Produces: `FinTextDumpWriter.Write(FinDump dump, TextWriter writer, bool full)`,
  `FinJsonDumpWriter.Write(FinDump dump, Stream stream)`.

Text format (plain text, no colour codes, `\n` line endings, invariant culture):

```
TEST (FIN version 23)
NiNode @0x00000010 [offset 0x10]
├─ Name: "Root"
├─ ExtraData: []
├─ Translation: (1, 2, 3)
├─ Rotation: [(1, 0, 0), (0, 1, 0), (0, 0, 1)]
└─ Children
   ├─ NiNode @0x00000020 [offset 0x5A]
   │  └─ ...
   └─ → NiNode @0x00000020
Unreferenced
└─ ...
```

Arrays: `Vertices: 312 × Vector3` unless `full`, then one child line per value with its index (`[0] (1, 2, 3)`).
Empty lists: `Field: []`. Null: `Field: null`. Strings quoted. Floats with `ToString("R", CultureInfo.InvariantCulture)`.
`Vector2`/`Vector3` as `(x, y)`/`(x, y, z)`. `FinMatrix3` as three groups. Other structs (colours, triangles) as
`(A, B, C)` from their public properties in declaration order. Bytes arrays as a summary or hex. The "Unreferenced"
section is left out when empty.

JSON format:

```json
{
  "name": "TEST",
  "version": 23,
  "roots": [ { "class": "NiNode", "linkId": "0x00000010", "offset": 16, "fields": { "Name": "Root", ... } } ],
  "unreferenced": []
}
```

A reference is `{ "ref": "0x00000020", "class": "NiNode" }`. Lists and arrays are JSON arrays (always in full).
`Vector3` is `[x, y, z]`, `FinMatrix3` is an array of 9 in file order, other structs are objects of their public
properties. NaN and infinities are written as the strings `"NaN"`, `"Infinity"`, `"-Infinity"`, because JSON has no
literal for them and `Utf8JsonWriter` throws on them.

- [ ] **Step 1: Write the failing tests**

Build `FinDump` values directly in the tests (no parsing needed). Text tests:

- `Write_WritesSummary_ForArrayWhenNotFull` → contains `Values: 3 × Single`.
- `Write_WritesEveryValue_ForArrayWhenFull` → contains `[2] 3`.
- `Write_WritesArrowLine_ForReference` → contains `→ NiNode @0x00000020`.
- `Write_WritesPlainText_WithoutEscapeCodes` → output contains no `\u001b`.
- `Write_OmitsUnreferencedSection_WhenEmpty`.
- `Write_UsesInvariantCulture_WhenCurrentCultureUsesCommaDecimal`: set `CultureInfo.CurrentCulture = new("nl-NL")`
  in a try/finally, dump the value `1.5f`, expect `1.5`.

JSON tests (parse the output back with `JsonDocument` and assert on properties):

- `Write_WritesReferenceObject_ForReference`.
- `Write_WritesVector3AsArray`.
- `Write_WritesNaNAsString_WhenFloatIsNaN` (Review Focus): a `FinDumpValue("X", float.NaN)` and a
  `FinDumpArray("Values", "Single", [float.PositiveInfinity])` → `"NaN"` and `"Infinity"`, no exception.
- `Write_WritesArraysInFull`.

Write each test in full with Arrange/Act/Assert.

- [ ] **Step 2: Run to verify they fail.** `dotnet test --filter FullyQualifiedName~Dump`
- [ ] **Step 3: Implement** the three classes. `FinDumpValueFormatter` holds the shared value formatting for text
  (`string Format(object? value)`) so writers stay small. The JSON writer uses `Utf8JsonWriter` with
  `new JsonWriterOptions { Indented = true }` and a `WriteValue(Utf8JsonWriter writer, object? value)` switch over
  `null`, `string`, `bool`, `byte`/`ushort`/`uint`/`int`/`long`, `float`/`double` (with the NaN/infinity strings),
  `Vector2`, `Vector3`, `FinMatrix3`, `byte[]` (hex string), enums (name as string) and a fallback that writes public
  properties of other structs as an object.
- [ ] **Step 4: Run to verify they pass.**
- [ ] **Step 5: Commit** `Write FIN dumps as text and JSON`.

---

### Task 15: `betateam fin dump`

**Files:**
- Create: `Malumware.BetaTeam.Cli/Commands/Fin/FinDumpCommandSettings.cs`
- Create: `Malumware.BetaTeam.Cli/Commands/Fin/FinDumpCommand.cs`
- Modify: `Malumware.BetaTeam.Cli/Program.cs`

**Interfaces:**
- Consumes: `FinReader.Read(string)`, `FinDumpBuilder.Build`, `FinTextDumpWriter.Write`, `FinJsonDumpWriter.Write`.

- [ ] **Step 1: Settings**

```csharp
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Fin
{
    public class FinDumpCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT>")]
        [Description("FIN file, or a directory of FIN files")]
        public required string InputPath { get; init; }

        [CommandOption("-o|--output")]
        [Description("Output file, or output directory when INPUT is a directory")]
        public string? OutputPath { get; init; }

        [CommandOption("--json")]
        [Description("Write JSON instead of a text tree")]
        public bool Json { get; init; }

        [CommandOption("--full")]
        [Description("Write every array value instead of a summary")]
        public bool Full { get; init; }

        [CommandOption("-m|--mask")]
        [DefaultValue("*.FIN")]
        public required string Mask { get; init; }

        public override ValidationResult Validate()
        {
            if (Directory.Exists(InputPath))
            {
                return OutputPath is null
                    ? ValidationResult.Error("--output is required when INPUT is a directory")
                    : ValidationResult.Success();
            }

            return File.Exists(InputPath)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Input not found: {InputPath}");
        }
    }
}
```

- [ ] **Step 2: Command**

```csharp
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Dump;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Fin
{
    public class FinDumpCommand : Command<FinDumpCommandSettings>
    {
        private static readonly EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };

        protected override int Execute(CommandContext context, FinDumpCommandSettings settings, CancellationToken cancellationToken)
        {
            return Directory.Exists(settings.InputPath)
                ? DumpDirectory(settings)
                : DumpFile(settings);
        }

        private static int DumpFile(FinDumpCommandSettings settings)
        {
            FinDump dump;
            try
            {
                dump = FinDumpBuilder.Build(new FinReader().Read(settings.InputPath));
            }
            catch (InvalidDataException e)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{Path.GetFileName(settings.InputPath)}[/]: {e.Message}");
                return 1;
            }

            if (settings.OutputPath is null)
            {
                // Plain Console, so Spectre doesn't treat brackets in the dump as markup
                Write(dump, settings, Console.OpenStandardOutput());
                return 0;
            }

            using var output = File.Create(settings.OutputPath);
            Write(dump, settings, output);
            return 0;
        }

        private static int DumpDirectory(FinDumpCommandSettings settings)
        {
            var files = Directory.EnumerateFiles(settings.InputPath, settings.Mask, ENUMERATION_OPTIONS).Order().ToList();
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No FIN files found.[/]");
                return 0;
            }

            Directory.CreateDirectory(settings.OutputPath!);
            var extension = settings.Json ? ".json" : ".txt";
            var failed = 0;

            foreach (var filePath in files)
            {
                try
                {
                    var dump = FinDumpBuilder.Build(new FinReader().Read(filePath));
                    var outputPath = Path.Combine(settings.OutputPath!, Path.GetFileNameWithoutExtension(filePath) + extension);
                    using var output = File.Create(outputPath);
                    Write(dump, settings, output);
                }
                catch (InvalidDataException e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{Path.GetFileName(filePath)}[/]: {e.Message}");
                    failed++;
                }
            }

            AnsiConsole.MarkupLine($"[green]Done.[/] Dumped {files.Count - failed} of {files.Count} files.");
            return failed == 0 ? 0 : 1;
        }

        private static void Write(FinDump dump, FinDumpCommandSettings settings, Stream output)
        {
            if (settings.Json)
            {
                FinJsonDumpWriter.Write(dump, output);
                return;
            }

            using var writer = new StreamWriter(output, leaveOpen: true) { NewLine = "\n" };
            FinTextDumpWriter.Write(dump, writer, settings.Full);
        }
    }
}
```

- [ ] **Step 3: Register** in `Program.cs`, after the `convert` branch:

```csharp
config.AddBranch("fin", fin =>
{
    fin.SetDescription("Inspect FIN model files.");

    fin.AddCommand<FinDumpCommand>("dump")
        .WithDescription("Dump the block tree of FIN files as text or JSON, to the terminal or to files.")
        .WithExample("fin", "dump", "/game/extracted/Fin/U0207.FIN")
        .WithExample("fin", "dump", "/game/extracted/Fin", "-o", "/game/dumps", "--json");
});
```

- [ ] **Step 4: Verify by running**

```bash
dotnet build
dotnet run --project Malumware.BetaTeam.Cli -- fin dump Research/extracted/Fin/OG9997.FIN | head -40
dotnet run --project Malumware.BetaTeam.Cli -- fin dump Research/extracted/Fin -o "$SCRATCH/dumps"
dotnet run --project Malumware.BetaTeam.Cli -- fin dump Research/extracted/Fin -o "$SCRATCH/json" --json
dotnet run --project Malumware.BetaTeam.Cli -- fin dump Research/extracted/Fin; echo "exit $?"
```

(`$SCRATCH` = the session scratchpad directory, never a tracked path.) Expected: a readable tree; 147 `.txt` and 147
`.json` files; `jq . < one.json` succeeds; the last command prints the validation error and exits non-zero.

- [ ] **Step 5: Commit** `Add fin dump command`.

---

### Task 16: Documentation wrap-up

**Files:**
- Modify: `docs/formats/fin.md`, `docs/README.md`, `AGENTS.md`

- [ ] **Step 1:** `docs/formats/fin.md`: add a short "Inspecting files" note pointing to `betateam fin dump`, and
  check every section against `Research/notes/fin.md`: every claim is verified in code and data, or marked inferred.
- [ ] **Step 2:** `docs/README.md`: FIN row → `[FIN models](formats/fin.md)` with status "Documented, verified
  (reading)".
- [ ] **Step 3:** `AGENTS.md` format status: FIN → "Reading verified and documented (`docs/formats/fin.md`). Export:
  glTF (#6), NIF (#5)." Add `IO/Fin` to the reference list for block-based formats if helpful.
- [ ] **Step 4:** `dotnet test`, then commit `Document the FIN format`.
