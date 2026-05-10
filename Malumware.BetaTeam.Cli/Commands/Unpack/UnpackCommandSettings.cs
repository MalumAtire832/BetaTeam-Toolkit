using System.ComponentModel;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Unpack
{
    public class UnpackCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT_DIRECTORY>")]
        [Description("Input directory path")]
        public required string InputDirectoryPath { get; init; }

        [CommandArgument(1, "<OUTPUT_DIRECTORY>")]
        [Description("Output directory path")]
        public required string OutputDirectoryPath { get; init; }

        [CommandOption("-m|--mask")]
        [DefaultValue("*.pac")]
        public required string Mask { get; init; }
    }
}