using Clube.Core;
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the playable demo (GL200; before it K33, K34, V22): a Windows build of the main menu and the
    /// Demo mode's scene, MapGen, with the Debug modes' labs, in <c>Builds/Demo/</c> (gitignored). It ships <c>Clube.Debug</c>, which
    /// is fine for a lab demo; the A10 check (no debug components) is for the Game scene. Not a development
    /// build: that opens a profiler port (a firewall prompt on first run) and stamps a watermark. It sets
    /// <c>CLUBE_LAB_BUILD</c>, which keeps step-through recording (A5) in. A game build never sets it.
    /// </summary>
    /// <remarks>
    /// Just before building it writes the build's version into <c>Resources/BuildInfo.txt</c> from git
    /// (GL201): the tag on the commit if it has one, else the project version with the day's patch number and
    /// short hash, e.g. "v0.2.3FC042 (e84d3c5)" (see <see cref="BuildVersion"/>: major.minor is
    /// <c>PlayerSettings.bundleVersion</c>, raised by hand; the patch is the day as YYMMDD in hex plus that day's build
    /// number, counted in <c>Builds/DayBuild.txt</c>). The file is gitignored and removed again after the
    /// build, so the Editor shows its own "Editor" version.
    /// </remarks>
    public static class DemoBuild
    {
        // The menu comes first: the player starts in the first scene listed.
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Tutorial.unity",
            "Assets/Scenes/MapGen.unity",
            // The menu's Debug modes (GL655).
            "Assets/Scenes/VoxelLab.unity",
            "Assets/Scenes/ChunkLab.unity",
            "Assets/Scenes/WorldLab.unity",
            "Assets/Scenes/ProceduralWorldLab.unity",
        };

        private const string Output = "Builds/Demo/clube.exe";

        // The bounded 5 km map alone, so the exe starts straight in it, for quick playtests of the map.
        private static readonly string[] MapGenScenes = { "Assets/Scenes/MapGen.unity" };

        private const string MapGenOutput = "Builds/MapGen/clube.exe";

        private const string BuildInfoAsset = "Assets/Resources/BuildInfo.txt";

        // The last build's day (YYMMDD) and its number that day, so each build of a day gets the next patch (gitignored).
        private const string DayBuildFile = "Builds/DayBuild.txt";

        // Keeps step-through recording in the exe (see MeshingRecorder).
        private const string LabBuildSymbol = "CLUBE_LAB_BUILD";

        [MenuItem("Clube/Build/Game demo")]
        private static void BuildFromMenu()
        {
            BuildReport report = Build();
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(Output);
            }
        }

        [MenuItem("Clube/Build/MapGen playtest")]
        private static void BuildMapGenFromMenu()
        {
            BuildReport report = BuildMapGen();
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(MapGenOutput);
            }
        }

        /// <summary>Builds the demo; also callable from batch mode with <c>-executeMethod Clube.Debug.Editor.DemoBuild.Build</c>.</summary>
        public static BuildReport Build() => Build(Scenes, Output);

        /// <summary>Builds the MapGen playtest exe; batch mode: <c>-executeMethod Clube.Debug.Editor.DemoBuild.BuildMapGen</c>.</summary>
        public static BuildReport BuildMapGen() => Build(MapGenScenes, MapGenOutput);

        private static BuildReport Build(string[] scenes, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            // The texture array is gitignored (generated): rebuild it, so a fresh clone never ships a stale one
            // missing the newest materials (sand once drew as the last layer, maple bark).
            MaterialTextureTools.BuildTextureArrays();
            string version = Version();
            File.WriteAllText(BuildInfoAsset, version);
            AssetDatabase.ImportAsset(BuildInfoAsset);
            BuildReport report;
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { LabBuildSymbol },
                };
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                AssetDatabase.DeleteAsset(BuildInfoAsset);
            }

            // IL2CPP leaves the C++ it compiled beside the game: over a gigabyte that is no part of it (GL772).
            string compiledCode = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_BackUpThisFolder_ButDontShipItWithYourGame");
            if (Directory.Exists(compiledCode))
            {
                Directory.Delete(compiledCode, true);
            }

            BuildSummary summary = report.summary;
            UnityEngine.Debug.Log(
                $"Game demo build {summary.result} ({version}): {summary.totalErrors} errors, " +
                $"{summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalSeconds:0} s → {output}");
            return report;
        }

        /// <summary>The version text for this commit, from git; the project version alone when git isn't there.</summary>
        public static string Version()
        {
            string hash = Git("rev-parse --short HEAD");
            string tag = hash != null ? Git("describe --tags --exact-match HEAD") : null;
            return BuildVersion.Format(PlayerSettings.bundleVersion, NextDayPatch(), hash, tag);
        }

        // Counts this build in Builds/DayBuild.txt ("<yymmdd> <n>"): the day's first build is 1, each later one adds 1.
        private static string NextDayPatch()
        {
            System.DateTime today = System.DateTime.Now;
            string day = today.ToString("yyMMdd");
            int n = 1;
            if (File.Exists(DayBuildFile))
            {
                string[] parts = File.ReadAllText(DayBuildFile).Trim().Split(' ');
                if (parts.Length == 2 && parts[0] == day && int.TryParse(parts[1], out int last))
                {
                    n = last + 1;
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(DayBuildFile));
            File.WriteAllText(DayBuildFile, $"{day} {n}");
            return BuildVersion.DayPatch(today, n);
        }

        // One git command's output, trimmed; null when it fails.
        private static string Git(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                };
                using Process git = Process.Start(start);
                string output = git.StandardOutput.ReadToEnd().Trim();
                git.WaitForExit(5000);
                return git.ExitCode == 0 && output.Length > 0 ? output : null;
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception || exception is InvalidOperationException)
            {
                return null;
            }
        }
    }
}
