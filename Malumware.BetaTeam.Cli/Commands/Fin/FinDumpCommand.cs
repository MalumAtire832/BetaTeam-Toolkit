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
            try
            {
                var bytes = Render(settings.InputPath, settings);
                if (settings.OutputPath is null)
                {
                    // Straight to stdout, so Spectre doesn't treat brackets in the dump as markup
                    using (var stdout = Console.OpenStandardOutput())
                    {
                        stdout.Write(bytes);
                    }
                }
                else
                {
                    File.WriteAllBytes(settings.OutputPath, bytes);
                }

                return 0;
            }
            catch (Exception e) when (IsFileError(e))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{Path.GetFileName(settings.InputPath)}[/]: {e.Message}");
                return 1;
            }
        }

        private static int DumpDirectory(FinDumpCommandSettings settings)
        {
            var files = Directory
                .EnumerateFiles(settings.InputPath, settings.Mask, ENUMERATION_OPTIONS)
                .Order()
                .ToList();
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No FIN files found.[/]");
                return 0;
            }

            try
            {
                Directory.CreateDirectory(settings.OutputPath!);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{settings.OutputPath}[/]: {e.Message}");
                return 1;
            }

            var extension = settings.Json ? ".json" : ".txt";
            var failed = 0;

            foreach (var filePath in files)
            {
                try
                {
                    // Rendered in full before writing, so a failure leaves no partial file behind
                    var bytes = Render(filePath, settings);
                    var outputPath = Path.Combine(settings.OutputPath!, Path.GetFileNameWithoutExtension(filePath) + extension);
                    File.WriteAllBytes(outputPath, bytes);
                }
                catch (Exception e) when (IsFileError(e))
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]{Path.GetFileName(filePath)}[/]: {e.Message}");
                    failed++;
                }
            }

            AnsiConsole.MarkupLine($"[green]Done.[/] Dumped {files.Count - failed} of {files.Count} files.");

            return failed == 0 ? 0 : 1;
        }

        // Problems with one file (its format, or reading and writing it) that shouldn't stop the others
        private static bool IsFileError(Exception e)
        {
            return e is InvalidDataException or IOException or UnauthorizedAccessException;
        }

        private static byte[] Render(string filePath, FinDumpCommandSettings settings)
        {
            var dump = FinDumpBuilder.Build(new FinReader().Read(filePath));
            using (var output = new MemoryStream())
            {
                if (settings.Json)
                {
                    FinJsonDumpWriter.Write(dump, output);
                }
                else
                {
                    using (var writer = new StreamWriter(output, leaveOpen: true))
                    {
                        FinTextDumpWriter.Write(dump, writer, settings.Full);
                    }
                }

                return output.ToArray();
            }
        }
    }
}
