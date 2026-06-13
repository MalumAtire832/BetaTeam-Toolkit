using System.ComponentModel;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Obj
{
    public class SplitObjCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT_FILE>")]
        [Description("Input OBJ file path")]
        public required string InputFilePath { get; init; }

        [CommandArgument(1, "[OUTPUT_DIRECTORY]")]
        [Description("Output directory path. Defaults to a subdirectory named after the input file (spaces replaced with underscores) alongside the input file.")]
        public string? OutputDirectoryPath { get; init; }
    }
}
