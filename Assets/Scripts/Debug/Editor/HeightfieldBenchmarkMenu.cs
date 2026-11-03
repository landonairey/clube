using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Runs the P12 heightfield benchmark (Landscape against the eroded landscape) from the menu and copies the results,
    /// as a Markdown table with the conditions it ran under, for <c>Docs/benchmarks.md</c>.
    /// </summary>
    public static class HeightfieldBenchmarkMenu
    {
        [MenuItem("Clube/Benchmarks/Heightfield generation (P12)")]
        private static void Run()
        {
            var results = HeightfieldBenchmark.Run();
            string report = MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + HeightfieldBenchmark.ToMarkdown(results);
            EditorGUIUtility.systemCopyBuffer = report;
            UnityEngine.Debug.Log("Heightfield benchmark (copied to the clipboard):\n" + report);
        }

        /// <summary>For batch mode (<c>-executeMethod Clube.Debug.Editor.HeightfieldBenchmarkMenu.RunFromCommandLine</c>): logs the table.</summary>
        public static void RunFromCommandLine()
        {
            var results = HeightfieldBenchmark.Run(10, 60);
            UnityEngine.Debug.Log("Heightfield benchmark:\n" + MeshStorageBenchmarkMenu.DescribeConditions() + "\n\n" + HeightfieldBenchmark.ToMarkdown(results));
        }
    }
}
