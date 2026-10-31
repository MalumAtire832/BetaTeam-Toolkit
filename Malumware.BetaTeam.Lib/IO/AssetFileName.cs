using System.Text.RegularExpressions;

namespace Malumware.BetaTeam.Lib.IO
{
    // The file-name rule shared with the Godot port: lower snake case. Case changes inside a name aren't split, so ids
    // stay readable ("U0179" becomes "u0179", not "u_0179"). Only file names change; file contents keep the original
    // ids.
    public static class AssetFileName
    {
        private static readonly Regex SEPARATORS = new("[^a-z0-9]+", RegexOptions.Compiled);

        public static string ToStem(string name)
        {
            return SEPARATORS
                .Replace(name.ToLowerInvariant(), "_")
                .Trim('_');
        }

        public static string ToFileName(string name)
        {
            var dot = name.LastIndexOf('.');
            if (dot <= 0)
            {
                return ToStem(name);
            }

            return $"{ToStem(name[..dot])}.{ToStem(name[(dot + 1)..])}";
        }

        public static string WithExtension(string name, string extension)
        {
            var dot = name.LastIndexOf('.');
            var stem = dot <= 0 ? name : name[..dot];

            return $"{ToStem(stem)}.{extension.ToLowerInvariant()}";
        }
    }
}
