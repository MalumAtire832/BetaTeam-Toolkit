using Malumware.BetaTeam.Cli.Commands.Convert.Dds;
using Malumware.BetaTeam.Cli.Commands.Fin;
using Malumware.BetaTeam.Cli.Commands.Unpack;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("betateam");

                config.AddCommand<UnpackCommand>("unpack")
                    .WithDescription("Extract all archives in a directory to an output directory. Each archive is unpacked into its own sub-folder named after the archive file.")
                    .WithExample("unpack", "/game/packs", "/game/extracted")
                    .WithExample("unpack", "/game/packs", "/game/extracted", "--mask", "*.pac");

                config.AddBranch("convert", convert =>
                {
                    convert.SetDescription("Convert files from one format to another.");

                    convert.AddBranch("dds", dds =>
                    {
                        dds.SetDescription("Convert DDS audio files.");

                        dds.AddCommand<DdsToWavCommand>("wav")
                            .WithDescription("Convert DDS audio files in a directory to WAV format.")
                            .WithExample("convert", "dds", "wav", "/game/extracted/audio", "/game/wav")
                            .WithExample("convert", "dds", "wav", "/game/extracted/audio", "/game/wav", "--mask", "*.DDS");
                    });
                });

                config.AddBranch("fin", fin =>
                {
                    fin.SetDescription("Inspect FIN model files.");

                    fin.AddCommand<FinDumpCommand>("dump")
                        .WithDescription("Dump the block tree of FIN files as text or JSON, to the terminal or to files.")
                        .WithExample("fin", "dump", "/game/extracted/Fin/U0207.FIN")
                        .WithExample("fin", "dump", "/game/extracted/Fin", "-o", "/game/dumps", "--json");
                });
            });

            return app.Run(args);
        }
    }
}
