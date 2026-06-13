using Malumware.BetaTeam.Lib.IO.Obj;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli.Commands.Convert.Obj
{
    public class SplitObjCommand : Command<SplitObjCommandSettings>
    {
        protected override int Execute(CommandContext context, SplitObjCommandSettings settings, CancellationToken cancellationToken)
        {
            var reader = new WavefrontObjReader();
            var splitter = new WavefrontObjSplitter();
            var writer = new WavefrontObjWriter();

            var source = reader.Read(settings.InputFilePath);
            var baseName = Path.GetFileNameWithoutExtension(settings.InputFilePath);
            var parts = splitter.Split(source);

            var outputDirectory = settings.OutputDirectoryPath
                ?? Path.Combine(
                    Path.GetDirectoryName(settings.InputFilePath)!,
                    $"{baseName.Replace(' ', '_')}_split"
                );

            Directory.CreateDirectory(outputDirectory);

            AnsiConsole.MarkupLine($"Writing to [grey]{outputDirectory}[/]");

            var table = new Table()
                .AddColumn("[grey]Object[/]")
                .AddColumn("[grey]Vertices[/]")
                .AddColumn("[grey]Faces[/]")
                .AddColumn("[grey]Output[/]");

            AnsiConsole.Live(table)
                .Start(ctx =>
                {
                    for (var i = 0; i < parts.Count; i++)
                    {
                        var part = parts[i];
                        var objectName = part.Objects[0].Name ?? i.ToString();
                        var outputPath = Path.Combine(outputDirectory, $"{baseName}_{objectName}.obj");
                        writer.Write(part, outputPath);

                        table.AddRow(
                            $"[blue]{objectName}[/]",
                            part.Vertices.Count.ToString(),
                            part.Objects[0].Faces.Count.ToString(),
                            outputPath
                        );
                        ctx.Refresh();
                    }
                });

            AnsiConsole.MarkupLine($"[green]Done.[/] Split {parts.Count} objects from [blue]{Path.GetFileName(settings.InputFilePath)}[/].");
            return 0;
        }
    }
}
