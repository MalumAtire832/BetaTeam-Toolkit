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
            using (var parser = new FinHeaderParser(new MemoryStream(Encoding.ASCII.GetBytes("Dweezil 23\nrest"))))
            {
                // Act
                var header = parser.Parse();

                // Assert
                Assert.True(header.IsValid);
                Assert.Equal(23, header.Version);
                Assert.Equal(11, header.Size);
            }
        }

        [Theory]
        [InlineData("NetImmerse File Format, Version 7.0\n")]
        [InlineData("Dweezil \n")]
        [InlineData("Dweezil 23 \n")]
        [InlineData("Dweezil 2x\n")]
        [InlineData("Dweezil 23")]
        [InlineData("Dweezil 99999999999\n")]
        public void Parse_ThrowsInvalidDataException_WhenLineIsMalformed(string text)
        {
            // Arrange
            using (var parser = new FinHeaderParser(new MemoryStream(Encoding.ASCII.GetBytes(text))))
            {
                // Act
                var act = () => parser.Parse();

                // Assert
                Assert.Throws<InvalidDataException>(act);
            }
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenVersionIsNot23()
        {
            // Arrange
            using (var parser = new FinHeaderParser(new MemoryStream(Encoding.ASCII.GetBytes("Dweezil 22\n"))))
            {
                // Act
                var act = () => parser.Parse();

                // Assert
                var exception = Assert.Throws<InvalidDataException>(act);
                Assert.Contains("22", exception.Message);
            }
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenNoNewlineWithinMaxLength()
        {
            // Arrange
            var bytes = Encoding.ASCII.GetBytes("Dweezil " + new string('1', 200) + "\n");
            using (var parser = new FinHeaderParser(new MemoryStream(bytes)))
            {
                // Act
                var act = () => parser.Parse();

                // Assert
                Assert.Throws<InvalidDataException>(act);
            }
        }
    }
}
