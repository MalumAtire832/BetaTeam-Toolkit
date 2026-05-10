using System.ComponentModel;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Tga
{
    public class TgaToPngCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT_DIRECTORY>")]
        [Description("Input directory path")]
        public required string InputDirectoryPath { get; init; }

        [CommandArgument(1, "<OUTPUT_DIRECTORY>")]
        [Description("Output directory path")]
        public required string OutputDirectoryPath { get; init; }

        [CommandOption("-m|--mask")]
        [DefaultValue("*.TGA")]
        public required string Mask { get; init; }
    }
}
