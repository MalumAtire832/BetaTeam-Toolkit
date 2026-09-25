using System.Text;
using Malumware.BetaTeam.Lib.IO.Locale;

namespace Malumware.BetaTeam.Lib.Tests.IO.Locale
{
    public class StringTableReaderTests
    {
        [Fact]
        public void Read_ReturnsTable_WithNameAndEntriesInFileOrder()
        {
            // Arrange
            var bytes = Encoding.ASCII.GetBytes("==B==\r\nTwo\r\n==A==\r\nOne\r\n");

            // Act
            var table = StringTableReader.Read("ETC_EN", bytes);

            // Assert
            Assert.Equal("ETC_EN", table.Name);
            Assert.Equal(["B", "A"], table.Entries.Select(entry => entry.Key));
        }

        [Fact]
        public void TryGetText_ReturnsText_WhenKeyDiffersInCasing()
        {
            // Arrange
            var table = StringTableReader.Read("UNITS_EN", Encoding.ASCII.GetBytes("==cu0001 name==\r\nBelt\r\n"));

            // Act
            var found = table.TryGetText("CU0001 NAME", out var text);

            // Assert
            Assert.True(found);
            Assert.Equal("Belt", text);
        }

        [Fact]
        public void TryGetText_ReturnsFalse_WhenKeyIsMissing()
        {
            // Arrange
            var table = StringTableReader.Read("UNITS_EN", Encoding.ASCII.GetBytes("==cu0001 name==\r\nBelt\r\n"));

            // Act
            var found = table.TryGetText("cu0002 name", out var text);

            // Assert
            Assert.False(found);
            Assert.Null(text);
        }
    }
}
