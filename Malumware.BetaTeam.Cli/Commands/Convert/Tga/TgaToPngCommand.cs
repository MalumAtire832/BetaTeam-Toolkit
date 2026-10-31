using Malumware.BetaTeam.Lib.IO;
using Malumware.BetaTeam.Lib.IO.Tga;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Tga
{
    public class TgaToPngCommand : Command<TgaToPngCommandSettings>
    {
        private static readonly EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };

        protected override int Execute(CommandContext context, TgaToPngCommandSettings settings, CancellationToken cancellationToken)
        {
            var files = Directory
                .EnumerateFiles(settings.InputDirectoryPath, settings.Mask, ENUMERATION_OPTIONS)
                .Order()
                .ToList();
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No TGA files found.[/]");
                return 0;
            }

            // Names are planned before anything is written, so a clash leaves the output untouched
            var outputs = new Dictionary<string, string>();
            var clashes = 0;
            foreach (var filePath in files)
            {
                var outputName = AssetFileName.WithExtension(Path.GetFileName(filePath), "png");
                if (outputs.TryGetValue(outputName, out var other))
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"[red]{Path.GetFileName(other)}[/] and [red]{Path.GetFileName(filePath)}[/] both become {outputName}"
                    );
                    clashes++;
                    continue;
                }
                outputs[outputName] = filePath;
            }
            if (clashes > 0)
            {
                return 1;
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

            var reader = new TgaReader();
            var converter = new TgaToPngConverter();
            var failed = 0;

            var table = new Table()
                .AddColumn("[grey]Input[/]")
                .AddColumn("[grey]Output[/]");

            AnsiConsole
                .Live(table)
                .Start(ctx =>
                {
                    foreach (var (outputName, filePath) in outputs)
                    {
                        var inputName = Path.GetFileName(filePath);
                        try
                        {
                            var outputPath = Path.Combine(settings.OutputDirectoryPath, outputName);
                            converter.Convert(reader.Read(filePath), outputPath);
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
