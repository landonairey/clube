using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the K26 voxel storage benchmark from the menu and copies the results, as a
    /// Markdown table with the conditions it ran under, for <c>Docs/benchmarks.md</c>.
    /// </summary>
    internal static class StorageBenchmarkMenu
    {
        [MenuItem("Clube/Benchmarks/Voxel storage (K26)")]
        private static void Run()
        {
            var results = StorageBenchmark.Run(StorageBenchmark.DefaultSizes);
            string report = MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + StorageBenchmark.ToMarkdown(results);
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("Voxel storage benchmark (copied to the clipboard):\n" + report);
        }
    }
}
