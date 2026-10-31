using Malumware.BetaTeam.Lib.IO;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using Malumware.BetaTeam.Lib.IO.Tga;

namespace Malumware.BetaTeam.Lib.Export
{
    // Writes the game into the asset tree of the Godot port: models as glb, textures as PNG, levels and UI data
    // copied, every file named with the lower snake case rule. Only standard formats; the port's importers do the rest.
    public class GodotExport
    {
        private const string MODELS = "models";
        private const string TEXTURES = "textures";
        private const string LEVELS = "levels";
        private const string DATA = "data";

        // The data files the port's later phases read, copied as they are
        private static readonly HashSet<string> DATA_FILES = new(StringComparer.OrdinalIgnoreCase)
        {
            "UILAYOUT.TXT",
            "CATEGORYUNITS.TXT",
            "CATEGORYIMAGES.TXT",
        };

        // Each output folder and the extension of the files it owns
        private static readonly (string Folder, string Extension)[] FOLDERS =
        [
            (MODELS, "glb"),
            (TEXTURES, "png"),
            (LEVELS, "puz"),
            (DATA, "txt"),
        ];

        private readonly bool _force;
        // Every level and unit needs the whole scene graph: PUZ attach paths count every child of the original
        private readonly FinToGltfConverter _converter = new(includeAllLevelsOfDetail: true, includeHidden: true);
        private readonly TgaReader _tgaReader = new();
        private readonly TgaToPngConverter _pngConverter = new();

        // force rewrites every output, for when the toolkit changed rather than the game
        public GodotExport(bool force = false)
        {
            _force = force;
        }

        public GodotExportResult Run(IReadOnlyList<GameFile> files, string outputDirectory)
        {
            // Every name is planned before anything is written, so a clash leaves the output untouched
            var outputs = new SortedDictionary<string, GameFile>(StringComparer.Ordinal);
            var errors = new List<string>();
            foreach (var file in files)
            {
                var output = OutputPath(file);
                if (output is null)
                {
                    continue;
                }
                if (outputs.TryGetValue(output, out var other))
                {
                    errors.Add($"{other.Archive}/{other.Name} and {file.Archive}/{file.Name} both become {output}");
                    continue;
                }
                outputs[output] = file;
            }
            if (errors.Count > 0)
            {
                return new GodotExportResult(0, 0, 0, errors);
            }

            var removed = RemoveStale(outputDirectory, outputs.Keys.ToHashSet());
            var written = 0;
            var skipped = 0;
            foreach (var (output, file) in outputs)
            {
                var path = Path.Combine(outputDirectory, output);
                if (!_force && File.Exists(path) && File.GetLastWriteTimeUtc(path) >= file.Modified)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var bytes = Convert(file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, bytes);
                    written++;
                }
                // One broken file shouldn't stop the others
                catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{file.Archive}/{file.Name}: {e.Message}");
                }
            }

            return new GodotExportResult(written, skipped, removed, errors);
        }

        // Where a game file goes, relative to the output directory; null for files the export leaves out
        private static string? OutputPath(GameFile file)
        {
            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
            return (file.Archive.ToLowerInvariant(), extension) switch
            {
                ("fin", ".fin") => $"{MODELS}/{AssetFileName.WithExtension(file.Name, "glb")}",
                ("map" or "lmap", ".tga") => $"{TEXTURES}/{AssetFileName.WithExtension(file.Name, "png")}",
                ("etc", ".puz") => $"{LEVELS}/{AssetFileName.ToFileName(file.Name)}",
                ("etc", ".txt") when DATA_FILES.Contains(file.Name) => $"{DATA}/{AssetFileName.ToFileName(file.Name)}",
                _ => null,
            };
        }

        private byte[] Convert(GameFile file)
        {
            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
            return extension switch
            {
                ".fin" => _converter.ToGlb(FinReader.Read(Path.GetFileNameWithoutExtension(file.Name), file.Read())),
                ".tga" => _pngConverter.ToPng(_tgaReader.Read(file.Read())),
                _ => file.Read(),
            };
        }

        // Deletes the files in the export's folders that no game file produces any more. Other files are left alone.
        private static int RemoveStale(string outputDirectory, HashSet<string> outputs)
        {
            var removed = 0;
            foreach (var (folder, extension) in FOLDERS)
            {
                var directory = Path.Combine(outputDirectory, folder);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(directory, $"*.{extension}"))
                {
                    if (!outputs.Contains($"{folder}/{Path.GetFileName(path)}"))
                    {
                        File.Delete(path);
                        removed++;
                    }
                }
            }

            return removed;
        }
    }
}
