using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the K32 mesher speed benchmark from the menu and copies the results, as
    /// a Markdown table with the conditions it ran under, for <c>Docs/benchmarks.md</c>.
    /// </summary>
    internal static class MesherSpeedBenchmarkMenu
    {
        [MenuItem("Clube/Benchmarks/Mesher speed (K32)")]
        private static void Run()
        {
            var results = MesherSpeedBenchmark.Run(BenchmarkTerrain.DefaultSizes);
            string report = MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + MesherSpeedBenchmark.ToMarkdown(results);
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("Mesher speed benchmark (copied to the clipboard):\n" + report);
        }
    }
}
