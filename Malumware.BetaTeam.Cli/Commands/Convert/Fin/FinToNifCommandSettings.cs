using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Fin
{
    public class FinToNifCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<INPUT>")]
        [Description("FIN file, or a directory of FIN files")]
        public required string InputPath { get; init; }

        [CommandArgument(1, "<OUTPUT>")]
        [Description("NIF file, or output directory when INPUT is a directory")]
        public required string OutputPath { get; init; }

        [CommandOption("-m|--mask")]
        [Description("File mask when INPUT is a directory")]
        [DefaultValue("*.FIN")]
        public required string Mask { get; init; }

        public override ValidationResult Validate()
        {
            return Directory.Exists(InputPath) || File.Exists(InputPath)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Input not found: {InputPath}");
        }
    }
}
