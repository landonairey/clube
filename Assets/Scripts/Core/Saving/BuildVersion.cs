namespace Clube.Core
{
    /// <summary>
    /// How a build's version reads (GL790): <c>v0.&lt;demo&gt;.&lt;build&gt; (&lt;hash&gt;)</c>, e.g. <c>v0.2.1312 (e84d3c5)</c>.
    /// The demo number is <c>PlayerSettings.bundleVersion</c> (<c>0.&lt;demo&gt;</c>), raised by hand when a new demo
    /// goes to playtesters; the build is the git commit count. A git tag on the commit wins over both.
    /// </summary>
    public static class BuildVersion
    {
        /// <summary>The line for a commit; the tag if there is one, else the project version and commit count.</summary>
        public static string Format(string projectVersion, string commitCount, string hash, string tag)
        {
            if (string.IsNullOrEmpty(hash))
            {
                return "v" + projectVersion;
            }
            if (!string.IsNullOrEmpty(tag))
            {
                return $"{tag} ({hash})";
            }
            return $"v{projectVersion}.{commitCount} ({hash})";
        }

        /// <summary>What the Editor shows, where there is no build: the demo number alone, e.g. <c>v0.2 · Editor</c>.</summary>
        public static string ForEditor(string projectVersion)
        {
            return $"v{projectVersion} · Editor";
        }
    }
}
