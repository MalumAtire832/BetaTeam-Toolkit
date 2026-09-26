using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Dump;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Dump
{
    public class FinDumpWriterTests
    {
        private static FinDump DumpWith(params FinDumpNode[] fields)
        {
            var root = new FinDumpObject(null, "NiNode", 0x10, 0x1F, fields);
            return new FinDump("TEST", 23, [root], []);
        }

        private static string Text(FinDump dump, bool full = false)
        {
            var writer = new StringWriter();
            FinTextDumpWriter.Write(dump, writer, full);

            return writer.ToString();
        }

        private static JsonElement Json(FinDump dump)
        {
            var stream = new MemoryStream();
            FinJsonDumpWriter.Write(dump, stream);

            return JsonDocument.Parse(stream.ToArray()).RootElement;
        }

        private static JsonElement Fields(JsonElement json)
        {
            return json.GetProperty("roots")[0].GetProperty("fields");
        }

        [Fact]
        public void Write_WritesHeaderAndRoot()
        {
            // Act
            var text = Text(DumpWith(new FinDumpValue("Name", "Root")));

            // Assert
            Assert.StartsWith("TEST (FIN version 23)\nNiNode @0x00000010 [offset 0x1F]\n└─ Name: \"Root\"\n", text);
        }

        [Fact]
        public void Write_WritesSummary_ForArrayWhenNotFull()
        {
            // Act
            var text = Text(DumpWith(new FinDumpArray("Values", "Single", [1f, 2f, 3f])));

            // Assert
            Assert.Contains("Values: 3 × Single", text);
        }

        [Fact]
        public void Write_WritesEveryValue_ForArrayWhenFull()
        {
            // Act
            var text = Text(DumpWith(new FinDumpArray("Values", "Single", [1f, 2f, 3f])), full: true);

            // Assert
            Assert.Contains("[2] 3", text);
        }

        [Fact]
        public void Write_WritesSummary_ForDataListWhenNotFull()
        {
            // Arrange
            var key = new FinDumpObject(null, "FinLinearFloatKey", null, null, [new FinDumpValue("Time", 0f)]);

            // Act
            var text = Text(DumpWith(new FinDumpList("Keys", "FinFloatKey", [key, key], IsData: true)));

            // Assert
            Assert.Contains("Keys: 2 × FinFloatKey", text);
        }

        [Fact]
        public void Write_ExpandsBlockList_EvenWhenNotFull()
        {
            // Arrange
            var child = new FinDumpObject(null, "NiTriShape", 0x20, 0x40, []);

            // Act
            var text = Text(DumpWith(new FinDumpList("Children", "FinRef<NiAVObject>", [child], IsData: false)));

            // Assert
            Assert.Contains("[0] NiTriShape @0x00000020 [offset 0x40]", text);
        }

        [Fact]
        public void Write_WritesArrowLine_ForReference()
        {
            // Act
            var text = Text(DumpWith(new FinDumpReference("Child", "NiNode", 0x20)));

            // Assert
            Assert.Contains("Child: → NiNode @0x00000020", text);
        }

        [Fact]
        public void Write_WritesEmptyListsAndNulls()
        {
            // Act
            var text = Text(DumpWith(new FinDumpList("ExtraData", "NiExtraData", [], IsData: false), new FinDumpValue("Name", null)));

            // Assert
            Assert.Contains("ExtraData: []", text);
            Assert.Contains("Name: null", text);
        }

        [Fact]
        public void Write_FormatsStructs()
        {
            // Act
            var text = Text(DumpWith(
                new FinDumpValue("Translation", new Vector3(1, 2.5f, 3)),
                new FinDumpValue("Rotation", FinMatrix3.Identity),
                new FinDumpValue("Color", new FinColor3(0.5f, 0, 1))));

            // Assert
            Assert.Contains("Translation: (1, 2.5, 3)", text);
            Assert.Contains("Rotation: [(1, 0, 0), (0, 1, 0), (0, 0, 1)]", text);
            Assert.Contains("Color: (0.5, 0, 1)", text);
        }

        [Fact]
        public void Write_WritesPlainText_WithoutEscapeCodes()
        {
            // Act
            var text = Text(DumpWith(new FinDumpValue("Name", "Root")));

            // Assert
            // An ordinal check: culture-aware comparison treats the escape character as ignorable
            Assert.False(text.Contains('\u001b'));
        }

        [Fact]
        public void Write_OmitsUnreferencedSection_WhenEmpty()
        {
            // Act
            var text = Text(DumpWith());

            // Assert
            Assert.DoesNotContain("Unreferenced", text);
        }

        [Fact]
        public void Write_WritesUnreferencedSection_WhenBlocksAreUnreferenced()
        {
            // Arrange
            var dump = new FinDump("TEST", 23, [], [new FinDumpObject(null, "NiFlipTextures", 0x30, 0x80, [])]);

            // Act
            var text = Text(dump);

            // Assert
            Assert.Contains("Unreferenced\n└─ NiFlipTextures @0x00000030", text);
        }

        [Fact]
        public void Write_UsesInvariantCulture_WhenCurrentCultureUsesCommaDecimal()
        {
            // Arrange
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            try
            {
                // Act
                var text = Text(DumpWith(new FinDumpValue("Scale", 1.5f)));

                // Assert
                Assert.Contains("Scale: 1.5", text);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void WriteJson_WritesBlockHeader()
        {
            // Act
            var json = Json(DumpWith());

            // Assert
            Assert.Equal("TEST", json.GetProperty("name").GetString());
            Assert.Equal(23, json.GetProperty("version").GetInt32());
            var root = json.GetProperty("roots")[0];
            Assert.Equal("NiNode", root.GetProperty("class").GetString());
            Assert.Equal("0x00000010", root.GetProperty("linkId").GetString());
            Assert.Equal(0x1F, root.GetProperty("offset").GetInt64());
        }

        [Fact]
        public void WriteJson_WritesReferenceObject_ForReference()
        {
            // Act
            var child = Fields(Json(DumpWith(new FinDumpReference("Child", "NiNode", 0x20)))).GetProperty("Child");

            // Assert
            Assert.Equal("0x00000020", child.GetProperty("ref").GetString());
            Assert.Equal("NiNode", child.GetProperty("class").GetString());
        }

        [Fact]
        public void WriteJson_WritesVector3AndMatrixAsArrays()
        {
            // Act
            var fields = Fields(Json(DumpWith(
                new FinDumpValue("Translation", new Vector3(1, 2, 3)),
                new FinDumpValue("Rotation", FinMatrix3.Identity))));

            // Assert
            var translation = fields
                .GetProperty("Translation")
                .EnumerateArray()
                .Select(e => e.GetSingle());
            Assert.Equal([1f, 2f, 3f], translation);
            Assert.Equal(9, fields.GetProperty("Rotation").GetArrayLength());
        }

        [Fact]
        public void WriteJson_WritesOtherStructsAsObjects()
        {
            // Act
            var color = Fields(Json(DumpWith(new FinDumpValue("Color", new FinColor3(0.5f, 0, 1))))).GetProperty("Color");

            // Assert
            Assert.Equal(0.5f, color.GetProperty("R").GetSingle());
        }

        [Fact]
        public void WriteJson_WritesNaNAsString_WhenFloatIsNaN()
        {
            // Act
            var fields = Fields(Json(DumpWith(
                new FinDumpValue("X", float.NaN),
                new FinDumpArray("Values", "Single", [float.PositiveInfinity, float.NegativeInfinity]))));

            // Assert
            Assert.Equal("NaN", fields.GetProperty("X").GetString());
            Assert.Equal("Infinity", fields.GetProperty("Values")[0].GetString());
            Assert.Equal("-Infinity", fields.GetProperty("Values")[1].GetString());
        }

        [Fact]
        public void WriteJson_WritesArraysAndListsInFull()
        {
            // Arrange
            var key = new FinDumpObject(null, "FinLinearFloatKey", null, null, [new FinDumpValue("Time", 2f)]);

            // Act
            var fields = Fields(Json(DumpWith(
                new FinDumpArray("Values", "Single", [1f, 2f, 3f]),
                new FinDumpList("Keys", "FinFloatKey", [key], IsData: true))));

            // Assert
            Assert.Equal(3, fields.GetProperty("Values").GetArrayLength());
            var jsonKey = fields.GetProperty("Keys")[0];
            Assert.Equal("FinLinearFloatKey", jsonKey.GetProperty("class").GetString());
            Assert.Equal(2f, jsonKey.GetProperty("fields").GetProperty("Time").GetSingle());
        }

        [Fact]
        public void WriteJson_WritesUtf8WithoutEscapingNonAscii()
        {
            // Act
            var stream = new MemoryStream();
            FinJsonDumpWriter.Write(DumpWith(new FinDumpValue("Name", "Bräu")), stream);

            // Assert
            Assert.Contains("Bräu", Encoding.UTF8.GetString(stream.ToArray()));
        }
    }
}
