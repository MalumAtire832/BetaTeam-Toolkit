using Malumware.BetaTeam.Lib.IO.Tga;
using Malumware.BetaTeam.Lib.IO.Tga.Conversion;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Tga
{
    public class TgaToPngCommand : Command<TgaToPngCommandSettings>
    {
        protected override int Execute(CommandContext context, TgaToPngCommandSettings settings, CancellationToken cancellationToken)
        {
            var reader = new TgaImageReader();
            var converter = new TgaImageConverter();
            var files = Directory.EnumerateFiles(settings.InputDirectoryPath, settings.Mask).ToList();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No TGA files found.[/]");
                return 0;
            }

            Directory.CreateDirectory(settings.OutputDirectoryPath);

            var table = new Table()
                .AddColumn("[grey]Input[/]")
                .AddColumn("[grey]Output[/]");

            AnsiConsole.Live(table)
                .Start(ctx =>
                {
                    foreach (var filePath in files)
                    {
                        var image = reader.Read(filePath);
                        var outputPath = Path.Combine(
                            settings.OutputDirectoryPath,
                            image.FileName + ".png"
                        );
                        /*converter.Convert(image, outputPath);*/

                        table.AddRow(
                            $"[blue]{Path.GetFileName(filePath)}[/]",
                            outputPath
                        );
                        ctx.Refresh();
                    }
                });

            AnsiConsole.MarkupLine($"[green]Done.[/] Converted {files.Count} files.");
            return 0;
        }
    }
}
