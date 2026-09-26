using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Fin
{
    public class FinDumpCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT>")]
        [Description("FIN file, or a directory of FIN files")]
        public required string InputPath { get; init; }

        [CommandOption("-o|--output")]
        [Description("Output file, or output directory when INPUT is a directory")]
        public string? OutputPath { get; init; }

        [CommandOption("--json")]
        [Description("Write JSON instead of a text tree")]
        public bool Json { get; init; }

        [CommandOption("--full")]
        [Description("Write every array and key value instead of a summary")]
        public bool Full { get; init; }

        [CommandOption("-m|--mask")]
        [Description("File mask when INPUT is a directory")]
        [DefaultValue("*.FIN")]
        public required string Mask { get; init; }

        public override ValidationResult Validate()
        {
            if (Directory.Exists(InputPath))
            {
                return OutputPath is null
                    ? ValidationResult.Error("--output is required when INPUT is a directory")
                    : ValidationResult.Success();
            }

            return File.Exists(InputPath)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Input not found: {InputPath}");
        }
    }
}
