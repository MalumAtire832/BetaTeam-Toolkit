using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Nif;
using Malumware.BetaTeam.Lib.IO.Nif;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Fin
{
    public class FinToNifCommand : Command<FinToNifCommandSettings>
    {
        private static readonly EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };

        protected override int Execute(CommandContext context, FinToNifCommandSettings settings, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(settings.InputPath))
            {
                return ConvertFile(settings.InputPath, settings.OutputPath) ? 0 : 1;
            }

            var files = Directory.EnumerateFiles(settings.InputPath, settings.Mask, ENUMERATION_OPTIONS).Order().ToList();
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No FIN files found.[/]");
                return 0;
            }

            try
            {
                Directory.CreateDirectory(settings.OutputPath);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{settings.OutputPath}[/]: {e.Message}");
                return 1;
            }

            var failed = 0;
            foreach (var filePath in files)
            {
                var outputPath = Path.Combine(settings.OutputPath, Path.GetFileNameWithoutExtension(filePath) + ".nif");
                if (!ConvertFile(filePath, outputPath))
                {
                    failed++;
                }
            }

            AnsiConsole.MarkupLine($"[green]Done.[/] Converted {files.Count - failed} of {files.Count} files.");
            return failed == 0 ? 0 : 1;
        }

        private static bool ConvertFile(string inputPath, string outputPath)
        {
            var name = Path.GetFileName(inputPath);
            try
            {
                var conversion = new FinToNifConverter().Convert(new FinReader().Read(inputPath));
                // Written in full before saving, so a failure leaves no partial file behind
                File.WriteAllBytes(outputPath, NifWriter.Write(conversion.File));

                AnsiConsole.MarkupLineInterpolated($"[blue]{name}[/] -> {outputPath}");
                foreach (var warning in conversion.Warnings)
                {
                    AnsiConsole.MarkupLineInterpolated($"  [yellow]{warning}[/]");
                }
                return true;
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{name}[/]: {e.Message}");
                return false;
            }
        }
    }
}
