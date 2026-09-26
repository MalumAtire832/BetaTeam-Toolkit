using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Fin
{
    public class FinToGltfCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT_DIRECTORY>")]
        [Description("Input directory path")]
        public required string InputDirectoryPath { get; init; }

        [CommandArgument(1, "<OUTPUT_DIRECTORY>")]
        [Description("Output directory path")]
        public required string OutputDirectoryPath { get; init; }

        [CommandOption("-m|--mask")]
        [DefaultValue("*.FIN")]
        public required string Mask { get; init; }

        [CommandOption("--all-lods")]
        [Description("Write every level of detail instead of only the most detailed one")]
        public bool AllLevelsOfDetail { get; init; }

        public override ValidationResult Validate()
        {
            return Directory.Exists(InputDirectoryPath)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Input directory not found: {InputDirectoryPath}");
        }
    }
}
