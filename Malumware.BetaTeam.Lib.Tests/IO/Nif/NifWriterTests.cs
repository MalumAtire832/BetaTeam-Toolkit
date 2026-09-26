using System.Numerics;
using System.Text;
using Malumware.BetaTeam.Lib.IO.Nif;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes;

namespace Malumware.BetaTeam.Lib.Tests.IO.Nif
{
    public class NifWriterTests
    {
        private static BinaryReader Open(NifFile file)
        {
            return new BinaryReader(new MemoryStream(NifWriter.Write(file)), Encoding.Latin1);
        }

        private static string ReadSizedString(BinaryReader reader)
        {
            return Encoding.Latin1.GetString(reader.ReadBytes(reader.ReadInt32()));
        }

        // Header string, version and block count
        private static uint ReadHeader(BinaryReader reader)
        {
            var line = Encoding.ASCII.GetString(reader.ReadBytes(NifWriter.HEADER_STRING.Length + 1));
            Assert.Equal(NifWriter.HEADER_STRING + "\n", line);
            Assert.Equal(NifWriter.VERSION, reader.ReadUInt32());

            return reader.ReadUInt32();
        }

        // NiObjectNET: name, extra data link, controller link
        private static (string Name, int ExtraData) ReadObjectNet(BinaryReader reader)
        {
            var name = ReadSizedString(reader);
            var extraData = reader.ReadInt32();
            Assert.Equal(-1, reader.ReadInt32());

            return (name, extraData);
        }

        [Fact]
        public void Write_WritesHeaderBlockAndFooter_WhenFileHasOneNode()
        {
            // Arrange
            var node = new NiNode
            {
                Name = "Root",
                Flags = NiAVObject.FLAG_HIDDEN,
                Translation = new Vector3(1, 2, 3),
                Rotation = new NifMatrix33(1, 2, 3, 4, 5, 6, 7, 8, 9),
                Scale = 2,
                Velocity = new Vector3(4, 5, 6),
            };

            // Act
            using (var reader = Open(new NifFile([node])))
            {
                // Assert
                Assert.Equal(1u, ReadHeader(reader));
                Assert.Equal("NiNode", ReadSizedString(reader));
                Assert.Equal(("Root", -1), ReadObjectNet(reader));
                Assert.Equal(NiAVObject.FLAG_HIDDEN, reader.ReadUInt16());
                Assert.Equal([1f, 2, 3], new[] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() });
                var rotation = Enumerable
                    .Range(0, 9)
                    .Select(_ => reader.ReadSingle())
                    .ToArray();
                Assert.Equal([1f, 2, 3, 4, 5, 6, 7, 8, 9], rotation);
                Assert.Equal(2f, reader.ReadSingle());
                Assert.Equal([4f, 5, 6], new[] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() });
                Assert.Equal(0u, reader.ReadUInt32());      // properties
                Assert.Equal(0u, reader.ReadUInt32());      // no bounding volume, as a four-byte bool
                Assert.Equal(0u, reader.ReadUInt32());      // children
                Assert.Equal(0u, reader.ReadUInt32());      // effects
                Assert.Equal(1u, reader.ReadUInt32());      // roots
                Assert.Equal(0, reader.ReadInt32());
                Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
            }
        }

        [Fact]
        public void CollectBlocks_ListsParentsFirstAndSharedBlocksOnce_WhenPropertyIsShared()
        {
            // Arrange
            var material = new NiMaterialProperty();
            var first = new NiTriShape { Data = new NiTriShapeData() };
            var second = new NiTriShape();
            first.Properties.Add(material);
            second.Properties.Add(material);
            var root = new NiNode();
            root.AddExtraData(new NiStringExtraData("a"));
            root.Children.AddRange([first, null, second]);

            // Act
            var blocks = new NifFile([root]).CollectBlocks();

            // Assert
            Assert.Equal(
                [root, root.ExtraData!, first, material, first.Data!, second],
                blocks
            );
        }

        [Fact]
        public void Write_WritesChildIndicesAndEmptySlots_WhenNodeHasChildren()
        {
            // Arrange
            var child = new NiNode { Name = "Child" };
            var root = new NiNode();
            root.Children.AddRange([null, child]);

            // Act
            using (var reader = Open(new NifFile([root])))
            {
                // Assert
                Assert.Equal(2u, ReadHeader(reader));
                Assert.Equal("NiNode", ReadSizedString(reader));
                ReadObjectNet(reader);
                reader.ReadBytes(2 + 12 + 36 + 4 + 12 + 4 + 4);    // flags to bounding volume
                Assert.Equal(2u, reader.ReadUInt32());
                Assert.Equal(-1, reader.ReadInt32());
                Assert.Equal(1, reader.ReadInt32());
            }
        }

        [Fact]
        public void Write_ChainsExtraData_WhenObjectHasSeveral()
        {
            // Arrange
            var property = new NiShadeProperty { Smooth = false };
            property.AddExtraData(new NiStringExtraData("First"));
            property.AddExtraData(new NiStringExtraData("Second"));

            // Act
            using (var reader = Open(new NifFile([property])))
            {
                // Assert
                Assert.Equal(3u, ReadHeader(reader));
                Assert.Equal("NiShadeProperty", ReadSizedString(reader));
                Assert.Equal(("", 1), ReadObjectNet(reader));
                Assert.Equal(0, reader.ReadUInt16());
                Assert.Equal("NiStringExtraData", ReadSizedString(reader));
                Assert.Equal(2, reader.ReadInt32());                    // next
                Assert.Equal(4u + 5, reader.ReadUInt32());              // bytes that follow
                Assert.Equal("First", ReadSizedString(reader));
                Assert.Equal("NiStringExtraData", ReadSizedString(reader));
                Assert.Equal(-1, reader.ReadInt32());
                Assert.Equal(4u + 6, reader.ReadUInt32());
                Assert.Equal("Second", ReadSizedString(reader));
            }
        }

        [Fact]
        public void Write_WritesGeometry_WhenEveryArrayIsPresent()
        {
            // Arrange
            var data = new NiTriShapeData
            {
                VertexCount = 3,
                Vertices = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY],
                Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ],
                BoundCenter = new Vector3(0.5f, 0.5f, 0),
                BoundRadius = 1,
                VertexColors = [new(1, 0, 0, 1), new(0, 1, 0, 1), new(0, 0, 1, 1)],
                Triangles = [new NifTriangle(0, 1, 2)],
            };
            data.UvSets.Add([Vector2.Zero, Vector2.UnitX, Vector2.UnitY]);

            // Act
            using (var reader = Open(new NifFile([data])))
            {
                // Assert
                ReadHeader(reader);
                Assert.Equal("NiTriShapeData", ReadSizedString(reader));
                Assert.Equal(3, reader.ReadUInt16());
                Assert.Equal(1u, reader.ReadUInt32());                  // has vertices
                reader.ReadBytes(3 * 12);
                Assert.Equal(1u, reader.ReadUInt32());                  // has normals
                reader.ReadBytes(3 * 12);
                reader.ReadBytes(16);                                   // bound
                Assert.Equal(1u, reader.ReadUInt32());                  // has vertex colours
                reader.ReadBytes(3 * 16);
                Assert.Equal(1, reader.ReadUInt16());                   // UV sets
                Assert.Equal(1u, reader.ReadUInt32());                  // has UV
                reader.ReadBytes(3 * 8);
                Assert.Equal(1, reader.ReadUInt16());                   // triangles
                Assert.Equal(3u, reader.ReadUInt32());                  // triangle points
                Assert.Equal([0, 1, 2], new[] { reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16() });
                Assert.Equal(0, reader.ReadUInt16());                   // match groups
                Assert.Equal(1u, reader.ReadUInt32());                  // roots
                Assert.Equal(0, reader.ReadInt32());
                Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
            }
        }

        [Fact]
        public void Write_Throws_WhenArrayLengthDiffersFromVertexCount()
        {
            // Arrange
            var data = new NiTriShapeData { VertexCount = 3, Vertices = [Vector3.Zero] };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => NifWriter.Write(new NifFile([data])));
        }

        [Fact]
        public void Write_WritesNestedVolumes_WhenBoundingVolumeIsUnion()
        {
            // Arrange
            var union = new NifUnionBV();
            union.Volumes.Add(new NifSphereBV { Center = Vector3.UnitX, Radius = 2 });
            union.Volumes.Add(new NifBoxBV { Extents = Vector3.One });
            var node = new NiNode { BoundingVolume = union };

            // Act
            using (var reader = Open(new NifFile([node])))
            {
                // Assert
                ReadHeader(reader);
                ReadSizedString(reader);
                ReadObjectNet(reader);
                reader.ReadBytes(2 + 12 + 36 + 4 + 12 + 4);
                Assert.Equal(1u, reader.ReadUInt32());                  // has bounding volume
                Assert.Equal(4u, reader.ReadUInt32());                  // union
                Assert.Equal(2u, reader.ReadUInt32());
                Assert.Equal(0u, reader.ReadUInt32());                  // sphere
                reader.ReadBytes(16);
                Assert.Equal(1u, reader.ReadUInt32());                  // box
                reader.ReadBytes(12 + 36 + 12);
                Assert.Equal(0u, reader.ReadUInt32());                  // children
            }
        }

        [Fact]
        public void Write_WritesSlotFlagsAndDescriptions_WhenTexturingPropertyHasBaseTexture()
        {
            // Arrange
            var source = new NiSourceTexture { FileName = "a.tga" };
            var property = new NiTexturingProperty();
            property.Textures[(int)NifTextureSlot.Base] = new NifTexDesc { Source = source, ClampMode = 0, FilterMode = 1 };

            // Act
            using (var reader = Open(new NifFile([property])))
            {
                // Assert
                Assert.Equal(2u, ReadHeader(reader));
                Assert.Equal("NiTexturingProperty", ReadSizedString(reader));
                ReadObjectNet(reader);
                Assert.Equal(0, reader.ReadUInt16());                   // flags
                Assert.Equal(NiTexturingProperty.APPLY_MODULATE, reader.ReadUInt32());
                Assert.Equal(7u, reader.ReadUInt32());                  // texture count
                Assert.Equal(1u, reader.ReadUInt32());                  // has base texture
                Assert.Equal(1, reader.ReadInt32());                    // source
                Assert.Equal(0u, reader.ReadUInt32());                  // clamp
                Assert.Equal(1u, reader.ReadUInt32());                  // filter
                Assert.Equal(0u, reader.ReadUInt32());                  // UV set
                reader.ReadBytes(6);                                    // PS2 L and K, unknown short
                for (var slot = 1; slot < NiTexturingProperty.TEXTURE_COUNT; slot++)
                {
                    Assert.Equal(0u, reader.ReadUInt32());
                }
                Assert.Equal("NiSourceTexture", ReadSizedString(reader));
                ReadObjectNet(reader);
                Assert.Equal(1, reader.ReadByte());                     // external
                Assert.Equal("a.tga", ReadSizedString(reader));
            }
        }
    }
}
