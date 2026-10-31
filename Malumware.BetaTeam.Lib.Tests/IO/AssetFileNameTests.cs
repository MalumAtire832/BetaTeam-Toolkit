using Malumware.BetaTeam.Lib.IO;

namespace Malumware.BetaTeam.Lib.Tests.IO
{
    // The same cases as the Godot port's tests/import/test_asset_names.gd, so both sides name files alike
    public class AssetFileNameTests
    {
        [Fact]
        public void ToStem_LowerCasesIds_WhenNameIsAnId()
        {
            // Act
            var stem = AssetFileName.ToStem("U0179");

            // Assert
            Assert.Equal("u0179", stem);
        }

        [Fact]
        public void ToStem_KeepsCaseChangesTogether_WhenNameMixesCase()
        {
            // Act
            var stem = AssetFileName.ToStem("e1010LM0066rc");

            // Assert
            Assert.Equal("e1010lm0066rc", stem);
        }

        [Fact]
        public void ToStem_CollapsesAndTrimsSeparators_WhenNameHasRunsOfThem()
        {
            // Act
            var stem = AssetFileName.ToStem("  -Odd--Name- ");

            // Assert
            Assert.Equal("odd_name", stem);
        }

        [Fact]
        public void ToStem_DropsTrailingSpace_WhenShippedNameEndsWithOne()
        {
            // Act
            var stem = AssetFileName.ToStem("GRILLBLKTOGRAY ");

            // Assert
            Assert.Equal("grillblktogray", stem);
        }

        [Theory]
        [InlineData("Studs Gry.tga", "studs_gry.tga")]
        [InlineData("M1010_SOLVED.PUZ", "m1010_solved.puz")]
        [InlineData("UILAYOUT.TXT", "uilayout.txt")]
        public void ToFileName_KeepsExtension_WhenNameHasOne(string name, string expected)
        {
            // Act
            var fileName = AssetFileName.ToFileName(name);

            // Assert
            Assert.Equal(expected, fileName);
        }

        [Fact]
        public void ToFileName_ReturnsStem_WhenNameHasNoExtension()
        {
            // Act
            var fileName = AssetFileName.ToFileName("README");

            // Assert
            Assert.Equal("readme", fileName);
        }

        [Theory]
        [InlineData("U0179.FIN", "glb", "u0179.glb")]
        [InlineData("T0011_WHOA.DDS", "wav", "t0011_whoa.wav")]
        [InlineData("Studs Gry.tga", "png", "studs_gry.png")]
        [InlineData("e1010LM0066rc.tga", "png", "e1010lm0066rc.png")]
        public void WithExtension_ReplacesExtension_WhenNameHasOne(string name, string extension, string expected)
        {
            // Act
            var fileName = AssetFileName.WithExtension(name, extension);

            // Assert
            Assert.Equal(expected, fileName);
        }
    }
}
