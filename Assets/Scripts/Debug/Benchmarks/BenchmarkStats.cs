using System;

namespace Clube.Debug
{
    /// <summary>Summaries and formatting shared by the benchmarks (K11, K32).</summary>
    internal static class BenchmarkStats
    {
        public static double Median(double[] values)
        {
            var sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        public static double Mean(double[] values)
        {
            double sum = 0;
            foreach (double value in values)
            {
                sum += value;
            }
            return sum / values.Length;
        }

        /// <summary>A byte count as B, KB or MB; "n/a" for NaN.</summary>
        public static string Bytes(double bytes)
        {
            if (double.IsNaN(bytes))
            {
                return "n/a";
            }
            if (bytes < 1024)
            {
                return $"{bytes:0} B";
            }
            return bytes < 1024 * 1024 ? $"{bytes / 1024:0.0} KB" : $"{bytes / (1024 * 1024):0.0} MB";
        }
    }
}
