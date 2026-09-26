using System.Text;
using Malumware.BetaTeam.Lib.IO.Locale;

namespace Malumware.BetaTeam.Lib.Tests.IO.Locale
{
    public class StringTableParserTests
    {
        [Fact]
        public void Parse_ReturnsEntry_WhenTagIsFollowedByValue()
        {
            // Arrange
            var text = "==MenuExitButton==\r\nExit\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            var entry = Assert.Single(entries);
            Assert.Equal("MenuExitButton", entry.Key);
            Assert.Equal("Exit", entry.Text);
        }

        [Fact]
        public void Parse_KeepsBlankLines_WhenTheyPrecedeTheNextTag()
        {
            // Arrange
            var text = "==First==\r\nOne\r\n\r\nTwo\r\n\r\n==Second==\r\nThree\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            Assert.Equal("One\n\nTwo\n", entries[0].Text);
            Assert.Equal("Three", entries[1].Text);
        }

        [Fact]
        public void Parse_SkipsCommentLines_WhenInsideValue()
        {
            // Arrange
            var text = "==Key==\r\nOne\r\n# note\r\nTwo\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            Assert.Equal("One\nTwo", Assert.Single(entries).Text);
        }

        [Fact]
        public void Parse_IgnoresTextAfterClosingMarker_WhenTagLineHasTrailingText()
        {
            // Arrange
            var text = "==Key== trailing\r\nValue\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            Assert.Equal("Key", Assert.Single(entries).Key);
        }

        [Theory]
        [InlineData("====")]
        [InlineData("==NoClosingMarker")]
        public void Parse_EndsValueWithoutNewEntry_WhenLineStartsWithMarkerButIsNoTag(string line)
        {
            // Arrange
            var text = $"==Key==\r\nValue\r\n{line}\r\nOrphan\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            var entry = Assert.Single(entries);
            Assert.Equal("Value", entry.Text);
        }

        [Fact]
        public void Parse_KeepsFirstEntry_WhenKeyRepeatsWithDifferentCasing()
        {
            // Arrange
            var text = "==Key==\r\nFirst\r\n==KEY==\r\nSecond\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            var entry = Assert.Single(entries);
            Assert.Equal("First", entry.Text);
        }

        [Theory]
        [InlineData(@"a\nb", "a\nb")]
        [InlineData(@"a\tb", "a\tb")]
        [InlineData(@"\x1B", "\u001B")]
        [InlineData(@"\X4a", "J")]
        [InlineData(@"\x0D", "\r")]
        [InlineData(@"\xZ1", "\u0001")]
        [InlineData(@"\\", @"\")]
        [InlineData(@"\q", "q")]
        public void Parse_DecodesEscape_WhenValueContainsBackslash(string value, string expected)
        {
            // Arrange
            var text = $"==Key==\r\n{value}\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            Assert.Equal(expected, Assert.Single(entries).Text);
        }

        [Fact]
        public void Parse_JoinsLines_WhenLineEndsWithBackslash()
        {
            // Arrange
            var text = "==Key==\r\nOne \\\r\nline\r\n";

            // Act
            var entries = Parse(text);

            // Assert
            Assert.Equal("One line", Assert.Single(entries).Text);
        }

        [Fact]
        public void Parse_KeepsLastLine_WhenFileHasNoTrailingNewline()
        {
            // Arrange
            var text = "==Key==\r\nValue";

            // Act
            var entries = Parse(text);

            // Assert
            Assert.Equal("Value", Assert.Single(entries).Text);
        }

        [Fact]
        public void Parse_IgnoresTag_WhenItIsTheUnterminatedLastLine()
        {
            // Arrange
            var text = "==Key==\r\nValue\r\n==Last==";

            // Act
            var entries = Parse(text);

            // Assert
            var entry = Assert.Single(entries);
            Assert.Equal("Value", entry.Text);
        }

        [Fact]
        public void Parse_DecodesWindows1252_WhenValueContainsHighBytes()
        {
            // Arrange
            var bytes = Encoding.ASCII.GetBytes("==Key==\r\nhe\u0000s\r\n");
            bytes[Array.IndexOf(bytes, (byte)0)] = 0x92;

            // Act
            var entries = Parse(bytes);

            // Assert
            Assert.Equal("he’s", Assert.Single(entries).Text);
        }

        private static IReadOnlyList<StringTableEntry> Parse(string text)
        {
            return Parse(Encoding.ASCII.GetBytes(text));
        }

        private static IReadOnlyList<StringTableEntry> Parse(byte[] bytes)
        {
            using (var parser = new StringTableParser(new MemoryStream(bytes)))
            {
                return parser.Parse();
            }
        }
    }
}
