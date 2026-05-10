using Malumware.BetaTeam.Cli.Commands.Convert.Dds;
using Malumware.BetaTeam.Cli.Commands.Convert.Tga;
using Malumware.BetaTeam.Cli.Commands.Unpack;
using Spectre.Console.Cli;

namespace Malumware.BetaTeam.Cli
{
    public static class Program
    {
        public static void Main(string[] args)
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

                    convert.AddBranch("tga", tga =>
                    {
                        tga.SetDescription("Convert TGA image files.");

                        tga.AddCommand<TgaToPngCommand>("png")
                            .WithDescription("Convert TGA image files in a directory to PNG format.")
                            .WithExample("convert", "tga", "png", "/game/textures", "/game/png")
                            .WithExample("convert", "tga", "png", "/game/textures", "/game/png", "--mask", "*.TGA");
                    });
                });
            });
            app.Run(args);
        }
    }
}
