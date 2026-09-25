using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class NiTriShapeTests
    {
        private const uint PRESENT = 0x0A96AC58;    // The engine stores the array's address; non-zero means present

        // A triangle (0, 1, 2) with every optional array present, and one texture set
        private static FinStreamBuilder WriteNiTriShape(FinStreamBuilder builder, string className = "NiTriShape")
        {
            return builder.SizedString(className).NiObject(0x10, "Box")
                .Byte(0)                                    // app culled
                .Floats(0, 0, 0)                            // translation
                .Floats(1, 0, 0, 0, 1, 0, 0, 0, 1)          // rotation
                .Floats(1)                                  // scale
                .Floats(0, 0, 0)                            // velocity
                .Refs()                                     // properties
                .UInt32(0)                                  // collision propagate
                .UInt32(0)                                  // no bounding volume
                .UInt16(3)                                  // vertex count
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0)      // vertices
                .UInt32(PRESENT).Floats(0, 0, 1, 0, 0, 1, 0, 0, 1)      // normals
                .Floats(0.5f, 0.5f, 0, 0.75f)               // bound center and radius
                .UInt16(1)                                  // triangle count
                .UInt16(1)                                  // texture set count
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0)      // texture coordinates
                .UInt32(PRESENT).Floats(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1)     // colours
                .UInt32(PRESENT).Floats(0, 0, 1, 0)         // triangle planes
                .UInt16(0).UInt16(1).UInt16(2);             // triangles
        }

        // The same triangle without any optional array
        private static FinStreamBuilder WriteBareNiTriShape(FinStreamBuilder builder)
        {
            return builder.SizedString("NiTriShape").NiObject(0x10)
                .Byte(0).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1).Floats(0, 0, 0)
                .Refs().UInt32(0).UInt32(0)
                .UInt16(3)                                  // vertex count
                .UInt32(0)                                  // no vertices
                .UInt32(0)                                  // no normals
                .Floats(0, 0, 0, 0)                         // bound
                .UInt16(1)                                  // triangle count
                .UInt16(0)                                  // texture set count
                .UInt32(0)                                  // no texture coordinates
                .UInt32(0)                                  // no colours
                .UInt32(0)                                  // no triangle planes
                .UInt16(0).UInt16(1).UInt16(2);             // triangles
        }

        [Fact]
        public void Read_ReturnsGeometry_WhenEveryArrayIsPresent()
        {
            // Arrange
            var bytes = WriteNiTriShape(new FinStreamBuilder().Header()).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var shape = Assert.IsType<NiTriShape>(Assert.Single(file.Objects));
            Assert.Equal(3, shape.VertexCount);
            Assert.Equal([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)], shape.Vertices);
            Assert.Equal(new Vector3(0, 0, 1), shape.Normals![2]);
            Assert.Equal(new Vector3(0.5f, 0.5f, 0), shape.BoundCenter);
            Assert.Equal(0.75f, shape.BoundRadius);
            Assert.Equal(1, shape.TriangleCount);
            var textureSet = Assert.Single(shape.TextureSets!);
            Assert.Equal(new Vector3(1, 0, 0), textureSet[1]);
            Assert.Equal(new FinColorA(0, 1, 0, 1), shape.Colors![1]);
            Assert.Equal(new FinPlane(new Vector3(0, 0, 1), 0), Assert.Single(shape.TrianglePlanes!));
            Assert.Equal(new FinTriangle(0, 1, 2), Assert.Single(shape.Triangles));
        }

        [Fact]
        public void Read_LeavesArraysNull_WhenArraysAreAbsent()
        {
            // Arrange
            var bytes = WriteBareNiTriShape(new FinStreamBuilder().Header()).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var shape = Assert.IsType<NiTriShape>(Assert.Single(file.Objects));
            Assert.Null(shape.Vertices);
            Assert.Null(shape.Normals);
            Assert.Null(shape.TextureSets);
            Assert.Null(shape.Colors);
            Assert.Null(shape.TrianglePlanes);
            Assert.Equal(new FinTriangle(0, 1, 2), Assert.Single(shape.Triangles));
        }

        [Fact]
        public void Read_ReadsTextureSetsInOrder_WhenShapeHasTwoSets()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("NiTriShape").NiObject(0x10)
                .Byte(0).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1).Floats(0, 0, 0)
                .Refs().UInt32(0).UInt32(0)
                .UInt16(1)                                  // vertex count
                .UInt32(0).UInt32(0).Floats(0, 0, 0, 0)
                .UInt16(0)                                  // triangle count
                .UInt16(2)                                  // texture set count
                .UInt32(PRESENT).Floats(1, 2, 3, 4, 5, 6)   // one vertex in each set
                .UInt32(0).UInt32(0)
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var shape = Assert.IsType<NiTriShape>(Assert.Single(file.Objects));
            Assert.Equal(2, shape.TextureSets!.Count);
            Assert.Equal(new Vector3(1, 2, 3), shape.TextureSets[0][0]);
            Assert.Equal(new Vector3(4, 5, 6), shape.TextureSets[1][0]);
        }

        [Fact]
        public void Read_ReturnsEnvMappedShape_WhenClassIsNiEnvMappedTriShape()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header();
            var bytes = WriteNiTriShape(builder, "NiEnvMappedTriShape").EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var shape = Assert.IsType<NiEnvMappedTriShape>(Assert.Single(file.Objects));
            Assert.Equal(new FinTriangle(0, 1, 2), Assert.Single(shape.Triangles));
        }
    }
}
