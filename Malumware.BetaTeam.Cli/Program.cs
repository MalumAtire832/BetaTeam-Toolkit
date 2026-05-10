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
            });
            app.Run(args);
        }
    }
}
