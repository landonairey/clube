namespace Clube.Debug
{
    /// <summary>The three volume measures for one voxel, as fractions of the unit cube (V11, V12, V14).</summary>
    public readonly struct VolumeReport
    {
        public VolumeReport(
            float approximate,
            float exact,
            float trilinearEstimate,
            int monteCarloSamples,
            int positiveTetrahedra,
            int negativeTetrahedra)
        {
            Approximate = approximate;
            Exact = exact;
            TrilinearEstimate = trilinearEstimate;
            MonteCarloSamples = monteCarloSamples;
            PositiveTetrahedra = positiveTetrahedra;
            NegativeTetrahedra = negativeTetrahedra;
        }

        /// <summary>V11: mean corner density.</summary>
        public float Approximate { get; }

        /// <summary>V12: signed tetrahedra sum of the closed Marching Cubes solid.</summary>
        public float Exact { get; }

        /// <summary>V14: Monte Carlo estimate of the smooth trilinear field; only valid when <see cref="MonteCarloSamples"/> &gt; 0.</summary>
        public float TrilinearEstimate { get; }

        public int MonteCarloSamples { get; }

        public int PositiveTetrahedra { get; }

        public int NegativeTetrahedra { get; }
    }
}
