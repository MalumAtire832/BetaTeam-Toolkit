using Malumware.BetaTeam.Lib.IO.Locale;

namespace Malumware.BetaTeam.Lib.Tests.IO.Locale
{
    public class GettextWriterTests
    {
        private const string HEADER = "msgid \"\"\nmsgstr \"\"\n\"Content-Type: text/plain; charset=UTF-8\\n\"\n\"Content-Transfer-Encoding: 8bit\\n\"\n";

        private static StringTable Table(string name, string key, string text)
        {
            return new StringTable(name, [new StringTableEntry(key, text)]);
        }

        [Fact]
        public void ToPo_WritesHeaderWithLanguage()
        {
            // Arrange
            var tables = new List<StringTable> { Table("T", "k", "v") };

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            Assert.StartsWith(HEADER + "\"Language: en\\n\"\n", po);
        }

        [Fact]
        public void ToPo_WritesUpperCaseKeyWithTableComment()
        {
            // Arrange
            var tables = new List<StringTable> { Table("UNITS_EN", "cu0001 name", "Belt\n") };

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            Assert.EndsWith("\n#. UNITS_EN\nmsgid \"CU0001 NAME\"\nmsgstr \"Belt\"\n", po);
        }

        [Fact]
        public void ToPo_SplitsMultiLineValues_AndTrimsTrailingLineFeeds()
        {
            // Arrange
            var tables = new List<StringTable> { Table("T", "k", "One\n\nTwo\n\n") };

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            Assert.EndsWith("msgstr \"\"\n\"One\\n\"\n\"\\n\"\n\"Two\"\n", po);
        }

        [Fact]
        public void ToPo_EscapesQuotesBackslashesAndTabs()
        {
            // Arrange
            var tables = new List<StringTable> { Table("T", "k", "a\"b\\c\td") };

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            Assert.Contains("msgstr \"a\\\"b\\\\c\\td\"", po);
        }

        [Fact]
        public void ToPo_KeepsFirstDefinition_WhenKeyRepeatsInAnotherCase()
        {
            // Arrange
            var tables = new List<StringTable> { Table("A", "foo", "one"), Table("B", "FOO", "two") };

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            Assert.Equal(1, po.Split("msgid \"FOO\"").Length - 1);
            Assert.Contains("#. A\nmsgid \"FOO\"\nmsgstr \"one\"\n", po);
            Assert.DoesNotContain("#. B", po);
        }

        [Fact]
        public void ToPo_WritesEmptyMsgstr_WhenValueIsOnlyLineFeeds()
        {
            // Arrange
            var tables = new List<StringTable> { Table("T", "k", "\n") };

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            Assert.EndsWith("msgid \"K\"\nmsgstr \"\"\n", po);
        }

        [Fact]
        public void ToPo_Throws_WhenValueHasControlCharacter()
        {
            // Arrange
            var tables = new List<StringTable> { Table("X", "k", "\x1B") };

            // Act
            var exception = Assert.Throws<InvalidDataException>(() => GettextWriter.ToPo(tables, "en"));

            // Assert
            Assert.Contains("X", exception.Message);
            Assert.Contains("k", exception.Message);
        }

        [Fact]
        public void ToPot_WritesEmptyMsgstrAndNoLanguage()
        {
            // Arrange
            var tables = new List<StringTable> { Table("T", "a", "one"), Table("T", "b", "two\nthree") };

            // Act
            var pot = GettextWriter.ToPot(tables);

            // Assert
            Assert.DoesNotContain("Language:", pot);
            Assert.StartsWith(HEADER, pot);
            Assert.EndsWith("\n#. T\nmsgid \"A\"\nmsgstr \"\"\n\n#. T\nmsgid \"B\"\nmsgstr \"\"\n", pot);
        }
    }
}
