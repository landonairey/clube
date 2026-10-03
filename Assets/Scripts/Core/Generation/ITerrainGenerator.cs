using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A terrain generator (K9): the density at any world position, 1 = solid and
    /// 0 = empty, with the surface where it crosses 0.5. Pure function of position,
    /// so neighbouring chunks sampled at shared border positions agree (M2).
    /// Chosen once from <see cref="TerrainSettings"/> by <see cref="TerrainGenerators"/>.
    /// </summary>
    public interface ITerrainGenerator
    {
        float Density(Vector3 position);
    }
}
