using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the playable demo (GL200; before it K33, K34, V22): a Windows build of the main menu and the
    /// World mode's scene, WorldLab, in <c>Builds/Demo/</c> (gitignored). It ships <c>Clube.Debug</c>, which
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
            "Assets/Scenes/WorldLab.unity",
        };

        private const string Output = "Builds/Demo/clube.exe";

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

        /// <summary>Builds the demo; also callable from batch mode with <c>-executeMethod Clube.Debug.Editor.DemoBuild.Build</c>.</summary>
        public static BuildReport Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            string version = Version();
            File.WriteAllText(BuildInfoAsset, version);
            AssetDatabase.ImportAsset(BuildInfoAsset);
            BuildReport report;
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = Output,
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

            BuildSummary summary = report.summary;
            UnityEngine.Debug.Log(
                $"Game demo build {summary.result} ({version}): {summary.totalErrors} errors, " +
                $"{summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalSeconds:0} s → {Output}");
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
