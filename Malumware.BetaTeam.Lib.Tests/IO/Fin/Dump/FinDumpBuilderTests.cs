using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Dump;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Dump
{
    public class FinDumpBuilderTests
    {
        private static FinDump Build(FinStreamBuilder builder)
        {
            var file = FinReader.Read("TEST", builder.EndOfFile().ToArray(), TestRegistry.Create());
            return FinDumpBuilder.Build(file);
        }

        private static FinDumpNode Child(FinDumpObject parent, string field)
        {
            return Assert.Single(parent.Children, c => c.Field == field);
        }

        [Fact]
        public void Build_WritesSharedBlockInFullOnce_WhenReferencedTwice()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestListBlock").NiObject(0x20).Refs(0x10).UInt32(0)
                .TopLevel().SizedString("TestListBlock").NiObject(0x30).Refs(0x10).UInt32(0)
                .SizedString("TestBlock").NiObject(0x10).UInt32(1);

            // Act
            var dump = Build(builder);

            // Assert
            var first = Assert.IsType<FinDumpList>(Child(dump.Roots[0], "Items"));
            var full = Assert.IsType<FinDumpObject>(Assert.Single(first.Items));
            Assert.Equal(0x10u, full.LinkId);
            var second = Assert.IsType<FinDumpList>(Child(dump.Roots[1], "Items"));
            var reference = Assert.IsType<FinDumpReference>(Assert.Single(second.Items));
            Assert.Equal(0x10u, reference.LinkId);
            Assert.Equal("TestBlock", reference.ClassName);
        }

        [Fact]
        public void Build_Terminates_WhenReferencesFormCycle()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestListBlock").NiObject(0x10).Refs(0x20).UInt32(0)
                .SizedString("TestListBlock").NiObject(0x20).Refs(0x10).UInt32(0);

            // Act
            var dump = Build(builder);

            // Assert
            var child = Assert.IsType<FinDumpObject>(Assert.Single(Assert.IsType<FinDumpList>(Child(dump.Roots[0], "Items")).Items));
            var back = Assert.IsType<FinDumpReference>(Assert.Single(Assert.IsType<FinDumpList>(Child(child, "Items")).Items));
            Assert.Equal(0x10u, back.LinkId);
            Assert.Empty(dump.Unreferenced);
        }

        [Fact]
        public void Build_ListsUnreferencedBlocks_WhenNotReachableFromTopLevel()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .SizedString("TestBlock").NiObject(0x20).UInt32(2);

            // Act
            var dump = Build(builder);

            // Assert
            var orphan = Assert.Single(dump.Unreferenced);
            Assert.Equal(0x20u, orphan.LinkId);
        }

        [Fact]
        public void Build_ReturnsArrayNode_ForValueArrays()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestListBlock").NiObject(0x10).Refs().UInt32(3).Floats(1, 2, 3);

            // Act
            var dump = Build(builder);

            // Assert
            var values = Assert.IsType<FinDumpArray>(Child(dump.Roots[0], "Values"));
            Assert.Equal("Single", values.ElementType);
            Assert.Equal([1f, 2f, 3f], values.Values);
        }

        [Fact]
        public void Build_OrdersFieldsFromBaseToDerived_WithoutHeaderProperties()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel().SizedString("TestBlock").NiObject(0x10, "A").UInt32(7);

            // Act
            var dump = Build(builder);

            // Assert
            var root = dump.Roots[0];
            Assert.Equal(["Name", "ExtraData", "Value"], root.Children.Select(c => c.Field));
            Assert.Equal("TestBlock", root.ClassName);
            Assert.Equal(0x10u, root.LinkId);
            Assert.Equal(11L + 4 + 16, root.Offset);   // header, Top Level Object marker
        }

        [Fact]
        public void Build_WritesNullReference_AsNullValue()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel().SizedString("TestParentBlock").NiObject(0x10).UInt32(0);

            // Act
            var dump = Build(builder);

            // Assert
            var child = Assert.IsType<FinDumpValue>(Child(dump.Roots[0], "Child"));
            Assert.Null(child.Value);
        }

        [Fact]
        public void Build_WritesPlainObjects_AsNestedObjects_WithReferencesNotExpanded()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestDataBlock").NiObject(0x10).UInt32(0x20).Floats(1.5f).UInt32(1).UInt32(0x20).Floats(2)
                .SizedString("TestBlock").NiObject(0x20).UInt32(9);

            // Act
            var dump = Build(builder);

            // Assert
            var data = Assert.IsType<FinDumpObject>(Child(dump.Roots[0], "Data"));
            Assert.Equal("TestData", data.ClassName);
            Assert.Null(data.LinkId);
            Assert.IsType<FinDumpReference>(Child(data, "Link"));
            var entries = Assert.IsType<FinDumpList>(Child(dump.Roots[0], "Entries"));
            Assert.True(entries.IsData);
            Assert.Equal("TestData", entries.ElementType);
            Assert.Equal(0x20u, Assert.Single(dump.Unreferenced).LinkId);
        }

        [Fact]
        public void Build_MarksBlockLists_AsNotData()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel().SizedString("TestListBlock").NiObject(0x10).Refs().UInt32(0);

            // Act
            var dump = Build(builder);

            // Assert
            Assert.False(Assert.IsType<FinDumpList>(Child(dump.Roots[0], "Items")).IsData);
        }
    }
}
