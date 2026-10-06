using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A terrain generator (K9): how far below the surface any world position is, positive
    /// underground and negative in the air. Pure function of position, so neighbouring
    /// chunks sampled at shared border positions agree (M2). The density follows from the
    /// depth (<see cref="TerrainDensity.FromDepth"/>, 1 = solid, 0 = empty, the surface at
    /// 0.5), and so do the material layers (<see cref="TerrainLayers"/>, M10).
    /// Chosen once from <see cref="TerrainSettings"/> by <see cref="TerrainGenerators"/>.
    /// </summary>
    public interface ITerrainGenerator
    {
        /// <summary>World units below the surface: positive underground, negative above.</summary>
        float Depth(Vector3 position);
    }

    /// <summary>The density every generator gives, from its depth.</summary>
    public static class TerrainGeneratorExtensions
    {
        public static float Density(this ITerrainGenerator generator, Vector3 position)
        {
            return TerrainDensity.FromDepth(generator.Depth(position));
        }
    }
}
