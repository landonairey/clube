using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Takes a world's edited chunks into save data and puts them back (S2, GL205). Only edited chunks are
    /// saved: the rest regenerate from the seed. Restoring keeps each chunk as an edited, unloaded one
    /// (<see cref="World.Keep"/>), so the streamer brings it back instead of generating it.
    /// </summary>
    public static class WorldSaving
    {
        /// <summary>Every edited chunk of the world, loaded or kept.</summary>
        public static List<ChunkSave> Capture(World world)
        {
            var saves = new List<ChunkSave>();
            foreach (KeyValuePair<Vector3Int, Chunk> edited in world.EditedChunks())
            {
                saves.Add(Capture(edited.Key, edited.Value));
            }
            return saves;
        }

        /// <summary>One chunk's samples, x fastest, then y, then z.</summary>
        public static ChunkSave Capture(Vector3Int coord, Chunk chunk)
        {
            Vector3Int size = chunk.SampleCount;
            int count = size.x * size.y * size.z;
            var save = new ChunkSave { Coord = coord, SampleCount = size };
            bool bytes = chunk.StoresBytes || chunk.IsUniform;
            if (bytes)
            {
                save.ByteDensities = new byte[count];
            }
            else
            {
                save.FloatDensities = new float[count];
            }
            VoxelMaterials materials = chunk.Materials;
            if (materials.IsUniform)
            {
                save.UniformMaterial = materials.UniformId;
            }
            else
            {
                save.Materials = new byte[count];
            }
            if (chunk.HasCarves)
            {
                save.Carves = new byte[count];
            }

            int index = 0;
            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++, index++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float density = chunk.GetDensity(sample);
                        if (bytes)
                        {
                            save.ByteDensities[index] = ByteVoxelStorage.Quantize(density);
                        }
                        else
                        {
                            save.FloatDensities[index] = density;
                        }
                        if (save.Materials != null)
                        {
                            save.Materials[index] = materials.Get(x, y, z);
                        }
                        if (save.Carves != null)
                        {
                            save.Carves[index] = chunk.GetCarveFlags(sample);
                        }
                    }
                }
            }
            return save;
        }

        /// <summary>
        /// Puts saved chunks back into a world before it streams them. A chunk saved at another chunk size
        /// (the world's config changed since) is skipped; returns how many came back.
        /// </summary>
        public static int Restore(World world, IEnumerable<ChunkSave> saves)
        {
            int restored = 0;
            Vector3Int size = world.Grid.ChunkSize + Vector3Int.one;
            foreach (ChunkSave save in saves)
            {
                if (save.SampleCount != size)
                {
                    continue;
                }
                world.Keep(save.Coord, Restore(save, world.StorageFactory));
                restored++;
            }
            return restored;
        }

        /// <summary>A chunk holding a save's samples, in storage from <paramref name="createStorage"/>.</summary>
        public static Chunk Restore(ChunkSave save, System.Func<Vector3Int, IVoxelStorage> createStorage)
        {
            Vector3Int size = save.SampleCount;
            var chunk = new Chunk(createStorage(size));
            int index = 0;
            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++, index++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        float density = save.ByteDensities != null ? ByteVoxelStorage.ToDensity(save.ByteDensities[index]) : save.FloatDensities[index];
                        chunk.SetDensity(sample, density);
                        byte material = save.Materials != null ? save.Materials[index] : save.UniformMaterial;
                        if (material != 0)
                        {
                            chunk.SetMaterial(sample, material);
                        }
                        if (save.Carves != null && save.Carves[index] != 0)
                        {
                            chunk.SetCarveFlags(sample, save.Carves[index]);
                        }
                    }
                }
            }
            return chunk;
        }
    }
}
