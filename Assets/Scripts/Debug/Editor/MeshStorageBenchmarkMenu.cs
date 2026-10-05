using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the K11 mesh storage benchmark from the menu and copies the results, as
    /// a Markdown table with the conditions it ran under, for <c>Docs/benchmarks.md</c>.
    /// </summary>
    internal static class MeshStorageBenchmarkMenu
    {
        [MenuItem("Clube/Benchmarks/Mesh storage: List vs array (K11)")]
        private static void Run()
        {
            var results = MeshStorageBenchmark.Run(BenchmarkTerrain.DefaultSizes);
            string report = DescribeConditions() + "\n\n" + MeshStorageBenchmark.ToMarkdown(results);
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("Mesh storage benchmark (copied to the clipboard):\n" + report);
        }

        /// <summary>Editor version, code optimization and machine: the numbers mean little without them.</summary>
        public static string DescribeConditions()
        {
            return $"Unity {Application.unityVersion}, in the Editor with {CompilationPipeline.codeOptimization} " +
                   $"code optimization, {SystemInfo.processorType} ({SystemInfo.processorCount} threads).";
        }
    }
}
