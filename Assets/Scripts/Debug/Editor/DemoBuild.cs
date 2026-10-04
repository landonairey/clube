using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the playable lab demo (K33, K34, V22): a Windows build of the DemoMenu
    /// welcome screen and the two labs it launches, VoxelLab (single voxel) and ChunkLab
    /// (single chunk), in <c>Builds/Demo/</c> (gitignored). It ships <c>Clube.Debug</c>,
    /// which is fine for a lab demo; the A10 check (no debug components) is for the Game
    /// scene. Not a development build: that opens a profiler port (a firewall prompt on
    /// first run) and stamps a watermark, neither of which a demo needs. Instead it sets
    /// <c>CLUBE_LAB_BUILD</c>, which keeps step-through recording (A5) in, so the labs'
    /// step-through works in the exe. A game build never sets it.
    /// </summary>
    public static class DemoBuild
    {
        // The welcome screen comes first: the player starts in the first scene listed.
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/DemoMenu.unity",
            "Assets/Scenes/VoxelLab.unity",
            "Assets/Scenes/ChunkLab.unity",
        };

        private const string Output = "Builds/Demo/clube.exe";

        // Keeps step-through recording in the exe (see MeshingRecorder).
        private const string LabBuildSymbol = "CLUBE_LAB_BUILD";

        [MenuItem("Clube/Build/Lab demo")]
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
                scenes = Scenes,
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { LabBuildSymbol },
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
