using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the M12 storage-at-scale benchmark from the menu on two worlds made from WorldLab's
    /// config (its own 16³ chunks at 1 m voxels, and 32³ chunks at 4 voxels per metre, the
    /// finer size M23 tests), and copies the results for <c>Docs/benchmarks.md</c>.
    /// </summary>
    internal static class WorldStorageBenchmarkMenu
    {
        private const string ConfigPath = "Assets/Config/ProceduralWorldLabWorldConfig.asset";

        [MenuItem("Clube/Benchmarks/Storage at scale (M12)")]
        private static void Run()
        {
            var source = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
            WorldConfig fine = Object.Instantiate(source);
            try
            {
                // 32 voxels of 0.25 m: 8 m chunks, 4 layers keep the world 32 m tall.
                fine.name = "WorldLab, 4 voxels per metre";
                fine.ChunkSize = new Vector3Int(32, 32, 32);
                fine.VoxelSize = 0.25f;
                fine.WorldHeightInChunks = 4;

                var scenarios = new[]
                {
                    new WorldStorageBenchmark.Scenario("16³ at 1 m, distance 6", source, 6),
                    new WorldStorageBenchmark.Scenario("32³ at 0.25 m, distance 4", fine, 4),
                };
                var results = WorldStorageBenchmark.Run(scenarios);
                string report = MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + WorldStorageBenchmark.ToMarkdown(results);
                EditorGUIUtility.systemCopyBuffer = report;
                UnityEngine.Debug.Log("Storage at scale benchmark (copied to the clipboard):\n" + report);
            }
            finally
            {
                Object.DestroyImmediate(fine);
            }
        }
    }
}
