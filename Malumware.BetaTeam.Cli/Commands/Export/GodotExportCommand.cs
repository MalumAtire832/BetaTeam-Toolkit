using Malumware.BetaTeam.Lib.Export;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Export
{
    public class GodotExportCommand : Command<GodotExportCommandSettings>
    {
        protected override int Execute(CommandContext context, GodotExportCommandSettings settings, CancellationToken cancellationToken)
        {
            IReadOnlyList<GameFile> files;
            try
            {
                files = GameFiles.FromGameDirectory(settings.GameDirectoryPath);
            }
            catch (Exception e) when (e is FileNotFoundException or InvalidDataException or IOException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{e.Message}[/]");
                return 1;
            }

            var result = AnsiConsole
                .Status()
                .Start($"Exporting {files.Count} game files...", _ => new GodotExport(settings.Force).Run(files, settings.OutputDirectoryPath));

            foreach (var error in result.Errors)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{error}[/]");
            }
            AnsiConsole.MarkupLine(
                $"[green]Done.[/] Written {result.Written}, up to date {result.Skipped}, removed {result.Removed}, errors {result.Errors.Count}."
            );

            return result.Errors.Count == 0 ? 0 : 1;
        }
    }
}
