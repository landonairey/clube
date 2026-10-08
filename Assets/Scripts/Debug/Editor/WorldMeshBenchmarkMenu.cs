using System;
using System.IO;
using Clube.Core;
using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the world-scale mesher benchmark (K32, 2D) on WorldLab's config, from the menu
    /// (results copied to the clipboard) or from the command line with
    /// <c>-executeMethod Clube.Debug.Editor.WorldMeshBenchmarkMenu.RunBatch -benchmarkOutput &lt;file&gt;</c>.
    /// </summary>
    internal static class WorldMeshBenchmarkMenu
    {
        private const string ConfigPath = "Assets/Config/ProceduralWorldLabWorldConfig.asset";

        // WorldLab's default, the game's player setting, and a far view.
        private static readonly int[] RenderDistances = { 4, 6, 8 };

        [MenuItem("Clube/Benchmarks/World meshing (K32)")]
        private static void Run()
        {
            string report = Report();
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("World meshing benchmark (copied to the clipboard):\n" + report);
        }

        /// <summary>Batch-mode entry point: writes the report to the -benchmarkOutput file and quits.</summary>
        public static void RunBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-benchmarkOutput");
            if (index < 0 || index + 1 >= args.Length)
            {
                UnityEngine.Debug.LogError("Pass -benchmarkOutput <file>.");
                EditorApplication.Exit(1);
                return;
            }

            File.WriteAllText(args[index + 1], Report());
            EditorApplication.Exit(0);
        }

        private static string Report()
        {
            var config = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            var results = WorldMeshBenchmark.Run(config, RenderDistances);
            return MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + WorldMeshBenchmark.ToMarkdown(config, results);
        }
    }
}
