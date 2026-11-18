using System;
using System.Collections.Generic;
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
    /// (GL201): the tag on the commit if it has one, else the project version with the commit count and
    /// short hash, e.g. "v0.1.0 · build 412 (a514430)". The file is gitignored and removed again after the
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

        private const string TimingOutput = "Builds/DemoTiming/clube.exe";

        // The bounded 5 km map alone, so the exe starts straight in it, for quick playtests of the map.
        private static readonly string[] MapGenScenes = { "Assets/Scenes/MapGen.unity" };

        private const string MapGenOutput = "Builds/MapGen/clube.exe";

        private const string BuildInfoAsset = "Assets/Resources/BuildInfo.txt";

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

        /// <summary>
        /// A development build for timing each system in the built game (GL770; batch mode:
        /// <c>-executeMethod Clube.Debug.Editor.DemoBuild.BuildForTiming</c>). It connects out to this machine instead of
        /// listening on the network, so Windows Firewall has nothing to ask. Delete it when the measuring is done.
        /// </summary>
        public static BuildReport BuildForTiming()
        {
            BuildReport report = Build(Scenes, TimingOutput, BuildOptions.Development);
            string bootConfig = Path.Combine(Path.GetDirectoryName(TimingOutput), "clube_Data", "boot.config");
            if (File.Exists(bootConfig))
            {
                var lines = new List<string>(File.ReadAllLines(bootConfig));
                lines.RemoveAll(line => line.StartsWith("player-connection-"));
                lines.Add("player-connection-mode=Connect");
                lines.Add("player-connection-ip=127.0.0.1");
                lines.Add("player-connection-wait-timeout=0");
                File.WriteAllLines(bootConfig, lines);
            }
            return report;
        }

        private static BuildReport Build(string[] scenes, string output, BuildOptions buildOptions = BuildOptions.None)
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
                    options = buildOptions,
                    extraScriptingDefines = new[] { LabBuildSymbol },
                };
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                AssetDatabase.DeleteAsset(BuildInfoAsset);
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
            string projectVersion = "v" + PlayerSettings.bundleVersion;
            string hash = Git("rev-parse --short HEAD");
            if (hash == null)
            {
                return projectVersion;
            }
            string tag = Git("describe --tags --exact-match HEAD");
            if (tag != null)
            {
                return $"{tag} ({hash})";
            }
            string count = Git("rev-list --count HEAD");
            return $"{projectVersion} · build {count} ({hash})";
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
