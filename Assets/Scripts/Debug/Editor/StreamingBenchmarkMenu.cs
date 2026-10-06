using System;
using System.IO;
using System.Text;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the streaming benchmark (P3, K35) on WorldLab's config, as it is (1 m voxels) and at
    /// 8 voxels per metre (M25's lead: 4 m chunks of 32 voxels, 8 layers), from the menu (results
    /// copied to the clipboard) or from the command line with
    /// <c>-executeMethod Clube.Debug.Editor.StreamingBenchmarkMenu.RunBatch -benchmarkOutput &lt;file&gt;</c>.
    /// Also runs the world meshing benchmark on both, for the per-stage numbers.
    /// </summary>
    internal static class StreamingBenchmarkMenu
    {
        private const string ConfigPath = "Assets/Config/WorldLabWorldConfig.asset";
        private const float FrameBudgetMs = 3f;

        private static readonly int[] RenderDistances = { 4, 6, 8 };

        [MenuItem("Clube/Benchmarks/World streaming (P3)")]
        private static void Run()
        {
            string report = Report();
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("World streaming benchmark (copied to the clipboard):\n" + report);
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
            var asset = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            var text = new StringBuilder(MeshStorageBenchmarkMenu.DescribeConditions()).AppendLine().AppendLine();
            foreach (WorldConfig config in new[] { Copy(asset, 16, 1f, 2), Copy(asset, 32, 0.125f, 8) })
            {
                try
                {
                    text.AppendLine(StreamingBenchmark.ToMarkdown(config, StreamingBenchmark.Run(config, RenderDistances, FrameBudgetMs), FrameBudgetMs));
                    text.AppendLine(WorldMeshBenchmark.ToMarkdown(config, WorldMeshBenchmark.Run(config, RenderDistances, warmup: 1, runs: 3)));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            }
            return text.ToString();
        }

        private static WorldConfig Copy(WorldConfig asset, int side, float voxelSize, int layers)
        {
            WorldConfig config = UnityEngine.Object.Instantiate(asset);
            config.name = $"{asset.name} ({Mathf.RoundToInt(1f / voxelSize)}/m)";
            config.ChunkSize = new Vector3Int(side, side, side);
            config.VoxelSize = voxelSize;
            config.WorldHeightInChunks = layers;
            return config;
        }
    }
}
