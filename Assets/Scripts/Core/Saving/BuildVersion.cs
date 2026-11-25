namespace Clube.Core
{
    /// <summary>
    /// How a build's version reads: <c>v&lt;major&gt;.&lt;minor&gt;.&lt;patch&gt; (&lt;hash&gt;)</c>, e.g. <c>v0.2.3FC042 (e84d3c5)</c>.
    /// Major.minor is <c>PlayerSettings.bundleVersion</c>, raised by hand when a new demo goes to playtesters. The patch
    /// is the build's day as YYMMDD in hex (2026-11-24 → 261124 → 3FC04) followed by that day's build number (that
    /// day's second build → 3FC042). A git tag on the commit wins over both.
    /// </summary>
    public static class BuildVersion
    {
        /// <summary>The patch number for a day's nth build: the day as YYMMDD in hex, then n.</summary>
        public static string DayPatch(System.DateTime day, int buildOfDay)
        {
            int yymmdd = (day.Year % 100) * 10000 + day.Month * 100 + day.Day;
            return yymmdd.ToString("X") + buildOfDay;
        }

        /// <summary>The line for a build; the tag if there is one, else the project version and the patch.</summary>
        public static string Format(string projectVersion, string patch, string hash, string tag)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return "v" + projectVersion;
            }
            if (!string.IsNullOrEmpty(tag))
            {
                return $"{tag} ({hash})";
            }
            return $"v{projectVersion}.{patch} ({hash})";
        }

        /// <summary>What the Editor shows, where there is no build: the demo number alone, e.g. <c>v0.2 · Editor</c>.</summary>
        public static string ForEditor(string projectVersion)
        {
            return $"v{projectVersion} · Editor";
        }
    }
}
