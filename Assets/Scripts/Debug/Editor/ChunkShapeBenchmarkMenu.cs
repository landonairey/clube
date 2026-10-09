using System;
using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the chunk shape comparison (M17) on the streamed lab's terrain, from the menu
    /// (results copied to the clipboard) or from the command line with
    /// <c>-executeMethod Clube.Debug.Editor.ChunkShapeBenchmarkMenu.RunBatch -benchmarkOutput &lt;file&gt;</c>.
    /// </summary>
    internal static class ChunkShapeBenchmarkMenu
    {
        private const string ConfigPath = "Assets/Config/ProceduralWorldLabWorldConfig.asset";

        // The loop site's plains and foothills, and the range south-west of it (GL26); 48 m squares.
        private static readonly ChunkShapeBenchmark.Area[] Areas =
        {
            new ChunkShapeBenchmark.Area("Plains and foothills", new Vector2(0f, 0f), 48f),
            new ChunkShapeBenchmark.Area("Mountains", new Vector2(-184f, -192f), 48f),
        };

        [MenuItem("Clube/Benchmarks/Chunk shape (M17)")]
        private static void Run()
        {
            string report = Report();
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("Chunk shape benchmark (copied to the clipboard):\n" + report);
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

        public static string Report()
        {
            var config = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            var results = ChunkShapeBenchmark.Run(config, Areas);
            return MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + ChunkShapeBenchmark.ToMarkdown(config, results);
        }
    }
}
