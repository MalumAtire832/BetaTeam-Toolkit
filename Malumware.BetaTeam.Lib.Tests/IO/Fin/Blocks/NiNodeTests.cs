using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class NiNodeTests
    {
        private static FinStreamBuilder WriteNiNode(
            FinStreamBuilder builder, uint linkId, string name, uint[] children)
        {
            return builder.SizedString("NiNode").NiObject(linkId, name)
                .Byte(0)                                // app culled
                .Floats(1, 2, 3)                        // translation
                .Floats(1, 0, 0, 0, 1, 0, 0, 0, 1)      // rotation
                .Floats(2)                              // scale
                .Floats(4, 5, 6)                        // velocity
                .Refs()                                 // properties
                .UInt32(3)                              // collision propagate
                .UInt32(0)                              // no bounding volume
                .UInt32(2)                              // sorting mode
                .UInt32(0)                              // sorter
                .Byte(1)                                // visual object
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
            Assert.Equal(new Vector3(4, 5, 6), node.Velocity);
            Assert.False(node.AppCulled);
            Assert.Equal(3u, node.CollisionPropagate);
            Assert.Equal(NiSortingMode.Default, node.SortingMode);
            Assert.True(node.IsVisualObject);
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
    }
}
