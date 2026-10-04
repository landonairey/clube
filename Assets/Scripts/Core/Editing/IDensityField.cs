using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A grid of density samples a brush can read and write, addressed by sample
    /// coordinate: one chunk (<see cref="Chunk"/>, local samples) or a whole world
    /// (<see cref="World"/>, global samples, every copy of a border sample written at once).
    /// Lets <see cref="TerrainBrush"/> work the same on either, and see across chunk borders.
    /// </summary>
    public interface IDensityField
    {
        /// <summary>The density at a sample, or false if the field has no such sample (e.g. not loaded).</summary>
        bool TryGetDensity(Vector3Int sample, out float density);

        /// <summary>Writes a sample through the field's single edit path (A7).</summary>
        void SetDensity(Vector3Int sample, float density);
    }
}
