using System.Text;
using Malumware.BetaTeam.Lib.Export;
using Malumware.BetaTeam.Lib.Tests.IO.Fin;
using Malumware.BetaTeam.Lib.Tests.IO.Tga;

namespace Malumware.BetaTeam.Lib.Tests.Export
{
    public sealed class GodotExportTests : IDisposable
    {
        private static readonly DateTime SOURCE_TIME = new(2000, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly string _output = Path.Combine(Path.GetTempPath(), $"godot-export-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_output))
            {
                Directory.Delete(_output, true);
            }
        }

        // A FIN with one empty node, enough to convert
        private static byte[] MinimalFin()
        {
            return new FinStreamBuilder()
                .Header()
                .TopLevel()
                .SizedString("NiNode").NiObject(0x10, "Root")
                .NiAVObjectFields()
                .NiNodeFields([], [])
                .EndOfFile()
                .ToArray();
        }

        private static byte[] MinimalTga()
        {
            return new TgaBuilder()
                .Header(imageType: 2, width: 1, height: 1, pixelDepth: 24)
                .Bytes(1, 2, 3)
                .ToArray();
        }

        // PCM, 1 channel, 22050 Hz, 16 bits, and two samples
        private static byte[] MinimalDds()
        {
            return
            [
                1, 0, 1, 0, 0x22, 0x56, 0, 0, 0x44, 0xAC, 0, 0, 2, 0, 16, 0, 0, 0,
                1, 2, 3, 4,
            ];
        }

        private static byte[] Table(string text)
        {
            return Encoding.Latin1.GetBytes(text);
        }

        private static GameFile File(string archive, string name, byte[] data, DateTime? modified = null)
        {
            return new GameFile(archive, name, modified ?? SOURCE_TIME, () => data);
        }

        private static List<GameFile> EverySource()
        {
            return
            [
                File("Fin", "U0001.FIN", MinimalFin()),
                File("Map", "Studs Gry.tga", MinimalTga()),
                File("Etc", "M1010_SOLVED.PUZ", Encoding.ASCII.GetBytes("VERSION 1\n")),
                File("Etc", "UILAYOUT.TXT", Encoding.ASCII.GetBytes("layout\n")),
                File("Audio", "T0011_WHOA.DDS", MinimalDds()),
                File("Locale_en", "ABORT1_EN.DDS", MinimalDds()),
                File("Ambient", "E1010.DDS", MinimalDds()),
                File("Ambient", "AMBIENT.PAC", []),
                File("Locale_en", "UNITS_EN.TXT", Table("==cu0001 name==\r\nBelt\r\n")),
                File("Locale_en", "KBD_EN.TXT", Table("==Kbd Escape==\r\n\\x1B\r\n")),
            ];
        }

        private string[] OutputFiles()
        {
            return Directory
                .EnumerateFiles(_output, "*", SearchOption.AllDirectories)
                .Select(e => Path.GetRelativePath(_output, e).Replace('\\', '/'))
                .Order()
                .ToArray();
        }

        [Fact]
        public void Run_WritesLowerSnakeCaseTree_WhenEverySourceIsPresent()
        {
            // Act
            var result = new GodotExport().Run(EverySource(), _output);

            // Assert
            Assert.Empty(result.Errors);
            Assert.Equal(9, result.Written);
            Assert.Equal(
                [
                    "audio/abort1_en.wav",
                    "audio/e1010.wav",
                    "audio/t0011_whoa.wav",
                    "data/uilayout.txt",
                    "levels/m1010_solved.puz",
                    "models/u0001.glb",
                    "text/alphateam.pot",
                    "text/en.po",
                    "textures/studs_gry.png",
                ],
                OutputFiles()
            );
            Assert.Equal("RIFF", Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(Path.Combine(_output, "audio/e1010.wav")), 0, 4));
            var po = System.IO.File.ReadAllText(Path.Combine(_output, "text/en.po"));
            Assert.Contains("msgid \"CU0001 NAME\"", po);
            Assert.DoesNotContain("KBD", po);
            Assert.Equal("VERSION 1\n", System.IO.File.ReadAllText(Path.Combine(_output, "levels/m1010_solved.puz")));
            Assert.Equal("glTF", Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(Path.Combine(_output, "models/u0001.glb")), 0, 4));
        }

        [Fact]
        public void Run_SkipsUpToDateOutputs_WhenRunTwice()
        {
            // Arrange
            new GodotExport().Run(EverySource(), _output);

            // Act
            var result = new GodotExport().Run(EverySource(), _output);

            // Assert
            Assert.Equal(0, result.Written);
            Assert.Equal(9, result.Skipped);
        }

        [Fact]
        public void Run_RewritesOutputs_WhenForced()
        {
            // Arrange
            new GodotExport().Run(EverySource(), _output);

            // Act
            var result = new GodotExport(force: true).Run(EverySource(), _output);

            // Assert
            Assert.Equal(9, result.Written);
            Assert.Equal(0, result.Skipped);
        }

        [Fact]
        public void Run_RewritesOutput_WhenSourceIsNewer()
        {
            // Arrange
            new GodotExport().Run(EverySource(), _output);
            var newer = new List<GameFile>
            {
                File("Etc", "M1010_SOLVED.PUZ", Encoding.ASCII.GetBytes("VERSION 2\n"), DateTime.UtcNow.AddDays(1)),
            };

            // Act
            var result = new GodotExport().Run(newer, _output);

            // Assert
            Assert.Equal(1, result.Written);
            Assert.Equal("VERSION 2\n", System.IO.File.ReadAllText(Path.Combine(_output, "levels/m1010_solved.puz")));
        }

        [Fact]
        public void Run_RemovesOutputsNoSourceProduces()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_output, "models"));
            System.IO.File.WriteAllText(Path.Combine(_output, "models/old.glb"), "");
            System.IO.File.WriteAllText(Path.Combine(_output, "models/notes.md"), "");

            // Act
            var result = new GodotExport().Run(EverySource(), _output);

            // Assert
            Assert.Equal(1, result.Removed);
            Assert.DoesNotContain("models/old.glb", OutputFiles());
            Assert.Contains("models/notes.md", OutputFiles());
        }

        [Fact]
        public void Run_FailsNamingBothSources_WhenTwoMapToOneName()
        {
            // Arrange
            var files = EverySource();
            files.Add(File("Map", "A B.TGA", MinimalTga()));
            files.Add(File("LMap", "A_B.TGA", MinimalTga()));

            // Act
            var result = new GodotExport().Run(files, _output);

            // Assert
            var error = Assert.Single(result.Errors);
            Assert.Contains("A B.TGA", error);
            Assert.Contains("A_B.TGA", error);
            Assert.Equal(0, result.Written);
            Assert.False(Directory.Exists(_output) && OutputFiles().Length > 0);
        }

        [Fact]
        public void Run_ContinuesAndReportsError_WhenAModelIsBroken()
        {
            // Arrange
            var files = EverySource();
            files.Add(File("Fin", "BROKEN.FIN", [1, 2, 3]));

            // Act
            var result = new GodotExport().Run(files, _output);

            // Assert
            var error = Assert.Single(result.Errors);
            Assert.Contains("BROKEN.FIN", error);
            Assert.Equal(9, result.Written);
        }

        [Fact]
        public void Run_IgnoresEtcFilesThatAreNotLevelsOrData()
        {
            // Arrange
            var files = EverySource();
            files.Add(File("Etc", "ARIAL.DDF", [0]));
            files.Add(File("Etc", "BELT.TGA", MinimalTga()));
            files.Add(File("Etc", "NOTES.TXT", [0]));

            // Act
            var result = new GodotExport().Run(files, _output);

            // Assert
            Assert.Equal(9, result.Written);
            Assert.Equal(9, OutputFiles().Length);
        }

        [Fact]
        public void Run_RewritesText_WhenAnyTableIsNewer()
        {
            // Arrange
            new GodotExport().Run(EverySource(), _output);
            var files = EverySource();
            files.Add(File("Locale_en", "ETC_EN.TXT", Table("==a==\r\nb\r\n"), DateTime.UtcNow.AddDays(1)));

            // Act
            var result = new GodotExport().Run(files, _output);

            // Assert
            Assert.Empty(result.Errors);
            Assert.Equal(2, result.Written);
            Assert.Equal(7, result.Skipped);
        }

        [Fact]
        public void Run_ReportsTextError_AndWritesTheRest_WhenATableHasAControlCharacter()
        {
            // Arrange
            var files = EverySource();
            files.RemoveAll(e => e.Name == "UNITS_EN.TXT");
            files.Add(File("Locale_en", "UNITS_EN.TXT", Table("==cu0001 name==\r\n\\x07\r\n")));

            // Act
            var result = new GodotExport().Run(files, _output);

            // Assert
            var error = Assert.Single(result.Errors);
            Assert.StartsWith("text/en.po", error);
            Assert.Equal(8, result.Written);
            Assert.Contains("models/u0001.glb", OutputFiles());
            Assert.Contains("textures/studs_gry.png", OutputFiles());
            Assert.Contains("audio/t0011_whoa.wav", OutputFiles());
        }

        [Fact]
        public void Run_RemovesStaleAudioAndText_ButKeepsOtherFiles()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_output, "audio"));
            Directory.CreateDirectory(Path.Combine(_output, "text"));
            System.IO.File.WriteAllText(Path.Combine(_output, "audio/old.wav"), "");
            System.IO.File.WriteAllText(Path.Combine(_output, "text/de.po"), "");
            System.IO.File.WriteAllText(Path.Combine(_output, "audio/t0011_whoa.wav.import"), "");

            // Act
            var result = new GodotExport().Run(EverySource(), _output);

            // Assert
            Assert.Equal(2, result.Removed);
            Assert.DoesNotContain("audio/old.wav", OutputFiles());
            Assert.DoesNotContain("text/de.po", OutputFiles());
            Assert.Contains("audio/t0011_whoa.wav.import", OutputFiles());
        }
    }
}
