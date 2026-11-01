using System.Text;
using Malumware.BetaTeam.Lib.IO;
using Malumware.BetaTeam.Lib.IO.Dds;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using Malumware.BetaTeam.Lib.IO.Locale;
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
        private const string AUDIO = "audio";
        private const string TEXT = "text";
        private const string POT_NAME = "alphateam.pot";
        private const string LOCALE_PREFIX = "Locale_";

        // The keyboard tables hold Windows virtual-key codes, not text; the port takes its controls from Godot's input map
        private const string KEYBOARD_TABLE_PREFIX = "KBD_";

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
            (AUDIO, "wav"),
            (TEXT, "po"),
            (TEXT, "pot"),
        ];

        // An output file, the game files it is made from, and how
        private sealed record PlannedOutput(string Path, IReadOnlyList<GameFile> Sources, Func<byte[]> Convert, string Label);

        private readonly bool _force;
        // Every level and unit needs the whole scene graph: PUZ attach paths count every child of the original
        private readonly FinToGltfConverter _converter = new(includeAllLevelsOfDetail: true, includeHidden: true);
        private readonly TgaReader _tgaReader = new();
        private readonly TgaToPngConverter _pngConverter = new();
        private readonly DdsAudioConverter _wavConverter = new();

        // force rewrites every output, for when the toolkit changed rather than the game
        public GodotExport(bool force = false)
        {
            _force = force;
        }

        public GodotExportResult Run(IReadOnlyList<GameFile> files, string outputDirectory)
        {
            // Every name is planned before anything is written, so a clash leaves the output untouched
            var outputs = new SortedDictionary<string, PlannedOutput>(StringComparer.Ordinal);
            var errors = new List<string>();
            foreach (var planned in Plan(files))
            {
                if (outputs.TryGetValue(planned.Path, out var other))
                {
                    var first = other.Sources[0];
                    var second = planned.Sources[0];
                    errors.Add($"{first.Archive}/{first.Name} and {second.Archive}/{second.Name} both become {planned.Path}");
                    continue;
                }
                outputs[planned.Path] = planned;
            }
            if (errors.Count > 0)
            {
                return new GodotExportResult(0, 0, 0, errors);
            }

            var removed = RemoveStale(outputDirectory, outputs.Keys.ToHashSet());
            var written = 0;
            var skipped = 0;
            foreach (var (output, planned) in outputs)
            {
                var path = Path.Combine(outputDirectory, output);
                var newest = planned.Sources.Max(e => e.Modified);
                if (!_force && File.Exists(path) && File.GetLastWriteTimeUtc(path) >= newest)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var bytes = planned.Convert();
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, bytes);
                    written++;
                }
                // One broken file shouldn't stop the others
                catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{planned.Label}: {e.Message}");
                }
            }

            return new GodotExportResult(written, skipped, removed, errors);
        }

        private IEnumerable<PlannedOutput> Plan(IReadOnlyList<GameFile> files)
        {
            foreach (var file in files)
            {
                var output = OutputPath(file);
                if (output is not null)
                {
                    yield return new PlannedOutput(output, [file], () => Convert(file), $"{file.Archive}/{file.Name}");
                }
            }

            foreach (var planned in PlanText(files))
            {
                yield return planned;
            }
        }

        // One PO file per language, and a POT from the first language's tables. Each depends on all of its tables.
        private static IEnumerable<PlannedOutput> PlanText(IReadOnlyList<GameFile> files)
        {
            var locales = files
                .Where(e => e.Archive.StartsWith(LOCALE_PREFIX, StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Archive, StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var first = true;
            foreach (var locale in locales)
            {
                var tables = locale
                    .Where(e => Path.GetExtension(e.Name).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    .Where(e => !e.Name.StartsWith(KEYBOARD_TABLE_PREFIX, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (tables.Count == 0)
                {
                    continue;
                }

                var language = locale.Key[LOCALE_PREFIX.Length..].ToLowerInvariant();
                var poPath = $"{TEXT}/{AssetFileName.ToFileName(language)}.po";
                yield return new PlannedOutput(poPath, tables, () => ToUtf8(GettextWriter.ToPo(ReadTables(tables), language)), poPath);
                if (first)
                {
                    first = false;
                    var potPath = $"{TEXT}/{POT_NAME}";
                    yield return new PlannedOutput(potPath, tables, () => ToUtf8(GettextWriter.ToPot(ReadTables(tables))), potPath);
                }
            }
        }

        private static List<StringTable> ReadTables(List<GameFile> tables)
        {
            return tables
                .Select(e => StringTableReader.Read(Path.GetFileNameWithoutExtension(e.Name), e.Read()))
                .ToList();
        }

        private static byte[] ToUtf8(string text)
        {
            return new UTF8Encoding(false).GetBytes(text);
        }

        // Where a game file goes, relative to the output directory; null for files the export leaves out
        private static string? OutputPath(GameFile file)
        {
            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
            var archive = file.Archive.ToLowerInvariant();
            if (extension == ".dds" && (archive is "audio" or "ambient" || archive.StartsWith("locale_")))
            {
                return $"{AUDIO}/{AssetFileName.WithExtension(file.Name, "wav")}";
            }

            return (archive, extension) switch
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
                ".dds" => _wavConverter.ToWav(DdsAudioReader.Read(Path.GetFileNameWithoutExtension(file.Name), file.Read())),
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
