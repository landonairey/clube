using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the playable ChunkLab demo (K33): a Windows build of the ChunkLab scene in
    /// <c>Builds/ChunkLab/</c> (gitignored). It ships <c>Clube.Debug</c>, which is fine for a
    /// lab demo; the A10 check (no debug components) is for the Game scene. Not a
    /// development build: that opens a profiler port (a firewall prompt on first run)
    /// and stamps a watermark, neither of which a demo needs.
    /// </summary>
    public static class DemoBuild
    {
        private const string Scene = "Assets/Scenes/ChunkLab.unity";
        private const string Output = "Builds/ChunkLab/ChunkLab.exe";

        [MenuItem("Clube/Build/ChunkLab demo (K33)")]
        private static void BuildFromMenu()
        {
            BuildReport report = Build();
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(Output);
            }
        }

        /// <summary>Builds the demo; also callable from batch mode with <c>-executeMethod Clube.Debug.Editor.DemoBuild.Build</c>.</summary>
        public static BuildReport Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            UnityEngine.Debug.Log(
                $"ChunkLab demo build {summary.result}: {summary.totalErrors} errors, " +
                $"{summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalSeconds:0} s → {Output}");
            return report;
        }
    }
}
