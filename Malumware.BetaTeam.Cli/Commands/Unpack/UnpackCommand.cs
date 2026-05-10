using Malumware.BetaTeam.Lib.IO.Pac;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Unpack
{
    public class UnpackCommand : Command<UnpackCommandSettings>
    {
        protected override int Execute(CommandContext context, UnpackCommandSettings settings, CancellationToken cancellationToken)
        {
            var reader = new PacArchiveReader();
            var files = Directory.EnumerateFiles(settings.InputDirectoryPath, settings.Mask).ToList();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No archives found.[/]");
                return 0;
            }

            var archives = files.Select(f => reader.Read(f)).ToList();
            var totalFiles = archives.Sum(a => (int)a.Entries.Count);

            AnsiConsole.Progress()
                .Columns(
                    new SpinnerColumn(),
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn())
                .Start(ctx =>
                {
                    var totalTask = ctx.AddTask($"Total (0/{totalFiles} files)", maxValue: totalFiles);

                    foreach (var archive in archives)
                    {
                        var archiveTask = ctx.AddTask($"[blue]{archive.FileName}[/]", maxValue: archive.Entries.Count);
                        var outputDir = Path.Combine(settings.OutputDirectoryPath, archive.FileName);
                        Directory.CreateDirectory(outputDir);

                        foreach (var (entry, data) in archive.Entries)
                        {
                            var destPath = Path.Combine(outputDir, entry.FileName);
                            File.WriteAllBytes(destPath, data);
                            if (entry.LastModified.HasValue)
                                File.SetLastWriteTimeUtc(destPath, entry.LastModified.Value);

                            archiveTask.Increment(1);
                            totalTask.Increment(1);
                            totalTask.Description = $"Total ({(int)totalTask.Value}/{totalFiles} files)";
                        }

                        archiveTask.StopTask();
                    }
                });

            AnsiConsole.MarkupLine($"[green]Done.[/] Extracted {totalFiles} files from {archives.Count} archives.");
            return 0;
        }
    }
}