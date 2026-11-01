using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class GlbJsonChunkTests
    {
        private const uint PRESENT = 0x0A96AC58;

        // A glb with a binary chunk: one triangle under a node
        private static byte[] TriangleGlb()
        {
            var bytes = new FinStreamBuilder().Header().TopLevel()
                .SizedString("NiTriShape").NiObject(0x10, "Triangle").NiAVObjectFields()
                .UInt16(3)
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0)      // vertices
                .UInt32(0)                                              // no normals
                .Floats(0.5f, 0.5f, 0, 0.75f)                           // bound
                .UInt16(1).UInt16(0)                                    // triangles, texture sets
                .UInt32(0).UInt32(0).UInt32(0)                          // no texture coordinates, colours, planes
                .UInt16(0).UInt16(1).UInt16(2)
                .EndOfFile()
                .ToArray();

            return new FinToGltfConverter().ToGlb(FinReader.Read("TEST", bytes));
        }

        [Theory]
        [InlineData("a")]
        [InlineData("ab")]
        [InlineData("abc")]
        [InlineData("abcd")]
        public void Rewrite_KeepsBinaryChunk_AndPadsJson(string value)
        {
            // Arrange
            var glb = TriangleGlb();

            // Act
            var rewritten = GlbJsonChunk.Rewrite(glb, json => json["asset"]!["extras"] = new JsonObject { ["test"] = value });

            // Assert
            Assert.Equal(0, rewritten.Length % 4);
            Assert.Equal(rewritten.Length, BitConverter.ToInt32(rewritten, 8));
            Assert.Equal(0, BitConverter.ToInt32(rewritten, 12) % 4);
            var before = GlbTestReader.Read(glb);
            var after = GlbTestReader.Read(rewritten);
            Assert.Equal(before.LogicalBuffers.Count, after.LogicalBuffers.Count);
            for (var i = 0; i < before.LogicalBuffers.Count; i++)
            {
                Assert.Equal(before.LogicalBuffers[i].Content, after.LogicalBuffers[i].Content);
            }
            Assert.Equal(value, GlbTestReader.Json(rewritten)["asset"]!["extras"]!["test"]!.GetValue<string>());
        }

        [Fact]
        public void Rewrite_ThrowsInvalidData_WhenFirstChunkIsNotJson()
        {
            // Arrange
            var glb = TriangleGlb();
            glb[16] = (byte)'X';

            // Act
            var act = () => GlbJsonChunk.Rewrite(glb, _ => { });

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
