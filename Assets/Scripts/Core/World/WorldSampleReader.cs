using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Many reads of nearby samples, fast (tree felling's searches): it keeps the chunk it read last, so a run of reads in
    /// one chunk skips the world's look-up of every chunk holding the sample (a felled crown is tens of thousands of reads,
    /// and each look-up cost ~0.3 µs). Samples whose owning chunk isn't loaded fall back to the world's own reads, so the
    /// answers are the world's. Make one per search: it doesn't notice chunks that load or drop after it read them.
    /// </summary>
    public sealed class WorldSampleReader
    {
        private readonly World world;
        private readonly Vector3Int size;
        private Vector3Int lastCoord;
        private Chunk lastChunk;

        public WorldSampleReader(World world)
        {
            this.world = world;
            size = world.Grid.ChunkSize;
        }

        /// <summary>The world read.</summary>
        public World World => world;

        /// <summary>Drops the chunk it kept: call when chunks may have loaded or dropped since the last read.</summary>
        public void Forget()
        {
            lastChunk = null;
        }

        /// <summary>As <see cref="World.GetDensity"/>.</summary>
        public float? GetDensity(Vector3Int sample)
        {
            return TryOwner(sample, out Chunk chunk, out Vector3Int local) ? chunk.GetDensity(local) : world.GetDensity(sample);
        }

        /// <summary>As <see cref="World.GetMaterial"/>.</summary>
        public byte? GetMaterial(Vector3Int sample)
        {
            return TryOwner(sample, out Chunk chunk, out Vector3Int local) ? chunk.GetMaterial(local) : world.GetMaterial(sample);
        }

        /// <summary>Density and material in one look-up; false (both unset) where no loaded chunk holds the sample.</summary>
        public bool TryRead(Vector3Int sample, out float density, out byte material)
        {
            if (TryOwner(sample, out Chunk chunk, out Vector3Int local))
            {
                density = chunk.GetDensity(local);
                material = chunk.GetMaterial(local);
                return true;
            }
            float? d = world.GetDensity(sample);
            density = d ?? 0f;
            material = d.HasValue ? world.GetMaterial(sample) ?? 0 : (byte)0;
            return d.HasValue;
        }

        private bool TryOwner(Vector3Int sample, out Chunk chunk, out Vector3Int local)
        {
            var coord = new Vector3Int(
                WorldGrid.FloorDiv(sample.x, size.x), WorldGrid.FloorDiv(sample.y, size.y), WorldGrid.FloorDiv(sample.z, size.z));
            local = new Vector3Int(sample.x - coord.x * size.x, sample.y - coord.y * size.y, sample.z - coord.z * size.z);
            if (lastChunk == null || coord != lastCoord)
            {
                if (!world.TryGetChunk(coord, out Chunk found))
                {
                    chunk = null;
                    return false;
                }
                lastCoord = coord;
                lastChunk = found;
            }
            chunk = lastChunk;
            return true;
        }
    }
}
