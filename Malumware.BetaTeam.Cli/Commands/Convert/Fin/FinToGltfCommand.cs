using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Fin
{
    public class FinToGltfCommand : Command<FinToGltfCommandSettings>
    {
        private static readonly EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };

        protected override int Execute(CommandContext context, FinToGltfCommandSettings settings, CancellationToken cancellationToken)
        {
            var files = Directory
                .EnumerateFiles(settings.InputDirectoryPath, settings.Mask, ENUMERATION_OPTIONS)
                .Order()
                .ToList();
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No FIN files found.[/]");
                return 0;
            }

            try
            {
                Directory.CreateDirectory(settings.OutputDirectoryPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{settings.OutputDirectoryPath}[/]: {e.Message}");
                return 1;
            }

            var reader = new FinReader();
            var converter = new FinToGltfConverter(settings.AllLevelsOfDetail);
            var failed = 0;

            var table = new Table()
                .AddColumn("[grey]Input[/]")
                .AddColumn("[grey]Output[/]");

            AnsiConsole
                .Live(table)
                .Start(ctx =>
                {
                    foreach (var filePath in files)
                    {
                        var inputName = Path.GetFileName(filePath);
                        try
                        {
                            var file = reader.Read(filePath);
                            var outputPath = Path.Combine(settings.OutputDirectoryPath, $"{file.Name}.glb");
                            converter.Convert(file, outputPath);
                            table.AddRow(Markup.Escape(inputName), Markup.Escape(outputPath));
                        }
                        // One broken file shouldn't stop the others
                        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
                        {
                            table.AddRow($"[red]{Markup.Escape(inputName)}[/]", Markup.Escape(e.Message));
                            failed++;
                        }
                        ctx.Refresh();
                    }
                });

            AnsiConsole.MarkupLine($"[green]Done.[/] Converted {files.Count - failed} of {files.Count} files.");

            return failed == 0 ? 0 : 1;
        }
    }
}
