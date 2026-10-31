using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Which build this is (GL201), for the main menu's corner and for saves: the demo build writes a line
    /// from git into <c>Resources/BuildInfo</c> just before building (the project version, the commit count
    /// and short hash, or the tag on that commit; <c>DemoBuild</c>). Played in the Editor there's none, so it
    /// says so.
    /// </summary>
    public static class BuildInfo
    {
        public const string ResourceName = "BuildInfo";

        private static string version;

        /// <summary>e.g. "v0.1.0 · build 412 (a514430)".</summary>
        public static string Version
        {
            get
            {
                if (version == null)
                {
                    var text = Resources.Load<TextAsset>(ResourceName);
                    version = text != null && !string.IsNullOrWhiteSpace(text.text)
                        ? text.text.Trim()
                        : $"v{Application.version} · Editor";
                }
                return version;
            }
        }
    }
}
