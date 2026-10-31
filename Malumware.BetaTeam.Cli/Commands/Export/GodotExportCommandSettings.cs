using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Export
{
    public class GodotExportCommandSettings : CommandSettings
    {
        [CommandArgument(0, "<GAME_DIRECTORY>")]
        [Description("The installed game: the directory with the .pac archives")]
        public required string GameDirectoryPath { get; init; }

        [CommandArgument(1, "<OUTPUT_DIRECTORY>")]
        [Description("The asset folder of the Godot project, e.g. <project>/assets/original")]
        public required string OutputDirectoryPath { get; init; }

        [CommandOption("--force")]
        [Description("Rewrite every file, also those newer than the game's archives (after a toolkit change)")]
        public bool Force { get; init; }

        public override ValidationResult Validate()
        {
            return Directory.Exists(GameDirectoryPath)
                ? ValidationResult.Success()
                : ValidationResult.Error($"Game directory not found: {GameDirectoryPath}");
        }
    }
}
