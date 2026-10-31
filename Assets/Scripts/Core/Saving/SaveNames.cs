using System;
using System.Text;

namespace Clube.Core
{
    /// <summary>
    /// Names for worlds and saves (GL206, GL207): the defaults, and the file a save's name is kept in. A
    /// save's file is its name with the characters a file name can't hold swapped for '-', so saving under
    /// the same name again replaces that save.
    /// </summary>
    public static class SaveNames
    {
        public const string DefaultWorldName = "Hello World";

        public const string Extension = ".clubesave";

        /// <summary>Longest name kept, in characters.</summary>
        public const int MaxLength = 64;

        private const string NotInFileNames = "\\/:*?\"<>|";

        /// <summary>"Hello World - World - 2026-10-30 15:04": the world, the game mode and when, to the minute.</summary>
        public static string DefaultSaveName(string worldName, string mode, DateTime localTime)
        {
            string world = Clean(worldName);
            return $"{(world.Length > 0 ? world : DefaultWorldName)} - {mode} - {localTime:yyyy-MM-dd HH:mm}";
        }

        /// <summary>A name trimmed, on one line, and no longer than <see cref="MaxLength"/>; empty for none.</summary>
        public static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }
            string line = name.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return line.Length > MaxLength ? line.Substring(0, MaxLength).TrimEnd() : line;
        }

        /// <summary>The file name a save's name is kept under, e.g. "Hello World - World - 2026-10-30 15-04.clubesave".</summary>
        public static string FileNameFor(string saveName)
        {
            string name = Clean(saveName);
            var file = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                // The characters Windows refuses, on every platform, so a save's file name is the same everywhere.
                file.Append(c < 32 || NotInFileNames.IndexOf(c) >= 0 ? '-' : c);
            }
            // Windows refuses names ending in a dot or a space.
            string cleaned = file.ToString().TrimEnd('.', ' ');
            return (cleaned.Length > 0 ? cleaned : "Save") + Extension;
        }
    }
}
