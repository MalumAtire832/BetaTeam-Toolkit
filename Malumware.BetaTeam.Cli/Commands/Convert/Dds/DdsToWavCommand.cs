using Malumware.BetaTeam.Lib.IO.Dds;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Dds
{
    public class DdsToWavCommand : Command<DdsToWavCommandSettings>
    {
        protected override int Execute(CommandContext context, DdsToWavCommandSettings settings, CancellationToken cancellationToken)
        {
            var reader = new DdsAudioReader();
            var converter = new DdsAudioConverter();
            var files = Directory.EnumerateFiles(settings.InputDirectoryPath, settings.Mask).ToList();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No DDS files found.[/]");
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
                        var audio = reader.Read(filePath);
                        var outputPath = Path.Combine(settings.OutputDirectoryPath, $"{audio.Name}.wav");
                        converter.Convert(audio, outputPath);

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
