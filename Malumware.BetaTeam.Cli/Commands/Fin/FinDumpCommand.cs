using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Dump;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Fin
{
    public class FinDumpCommand : Command<FinDumpCommandSettings>
    {
        private static readonly EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };

        protected override int Execute(CommandContext context, FinDumpCommandSettings settings, CancellationToken cancellationToken)
        {
            return Directory.Exists(settings.InputPath)
                ? DumpDirectory(settings)
                : DumpFile(settings);
        }

        private static int DumpFile(FinDumpCommandSettings settings)
        {
            FinDump dump;
            try
            {
                dump = FinDumpBuilder.Build(new FinReader().Read(settings.InputPath));
            }
            catch (InvalidDataException e)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{Path.GetFileName(settings.InputPath)}[/]: {e.Message}");
                return 1;
            }

            if (settings.OutputPath is null)
            {
                // Straight to stdout, so Spectre doesn't treat brackets in the dump as markup
                using var stdout = Console.OpenStandardOutput();
                Write(dump, settings, stdout);
                return 0;
            }

            using var output = File.Create(settings.OutputPath);
            Write(dump, settings, output);
            return 0;
        }

        private static int DumpDirectory(FinDumpCommandSettings settings)
        {
            var files = Directory.EnumerateFiles(settings.InputPath, settings.Mask, ENUMERATION_OPTIONS).Order().ToList();
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No FIN files found.[/]");
                return 0;
            }

            Directory.CreateDirectory(settings.OutputPath!);
            var extension = settings.Json ? ".json" : ".txt";
            var failed = 0;

            foreach (var filePath in files)
            {
                try
                {
                    var dump = FinDumpBuilder.Build(new FinReader().Read(filePath));
                    var outputPath = Path.Combine(settings.OutputPath!, Path.GetFileNameWithoutExtension(filePath) + extension);
                    using var output = File.Create(outputPath);
                    Write(dump, settings, output);
                }
                catch (InvalidDataException e)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{Path.GetFileName(filePath)}[/]: {e.Message}");
                    failed++;
                }
            }

            AnsiConsole.MarkupLine($"[green]Done.[/] Dumped {files.Count - failed} of {files.Count} files.");
            return failed == 0 ? 0 : 1;
        }

        private static void Write(FinDump dump, FinDumpCommandSettings settings, Stream output)
        {
            if (settings.Json)
            {
                FinJsonDumpWriter.Write(dump, output);
                return;
            }

            using var writer = new StreamWriter(output, leaveOpen: true);
            FinTextDumpWriter.Write(dump, writer, settings.Full);
        }
    }
}
