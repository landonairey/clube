using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The loaded chunks of a world, keyed by chunk coordinate (M1), and the edit paths
    /// that span them. Pure data, no Unity components: <see cref="WorldView"/> decides
    /// what to load and renders it.
    /// </summary>
    /// <remarks>
    /// Neighbouring chunks each keep a copy of their shared border samples (density and
    /// material, M10), so the
    /// world keeps every copy identical (M2), which is what makes meshes meet without
    /// cracks:
    /// <list type="bullet">
    /// <item>Generation samples world positions, so fresh chunks agree on borders.</item>
    /// <item><see cref="SetDensity"/> writes every loaded copy of a sample (A7).</item>
    /// <item><see cref="ApplyBrush"/> runs the brush on the world as one density field
    /// (it implements <see cref="IDensityField"/>), so every write goes through
    /// <see cref="SetDensity"/> to every copy (M5).</item>
    /// <item>A newly loaded chunk takes its shared border samples from any loaded
    /// neighbour that was edited, so edits made next to it aren't contradicted.</item>
    /// </list>
    /// Edited chunks are kept in memory when unloaded and come back as they were, until
    /// saving exists (Chapter 4).
    /// </remarks>
    public sealed class World : IDensityField
    {
        private readonly Dictionary<Vector3Int, Chunk> loaded = new Dictionary<Vector3Int, Chunk>();
        private readonly Dictionary<Vector3Int, Chunk> editedUnloaded = new Dictionary<Vector3Int, Chunk>();
        private readonly HashSet<Vector3Int> edited = new HashSet<Vector3Int>();

        // Loaded chunks that became dirty since they were last collected (CollectDirty); a chunk may
        // have been cleaned since, so readers check.
        private readonly HashSet<Vector3Int> dirty = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> scratchChunks = new List<Vector3Int>();
        // The box of chunk coordinates anything was ever loaded in (it only grows, until Clear): rays
        // walk the chunk grid inside it, so a cast costs the same whatever the world size.
        private Vector3Int loadedMin;
        private Vector3Int loadedMax;
        private bool anyLoaded;

        private readonly Func<Vector3Int, IVoxelStorage> createStorage;
        private DensityFormat? preferredFormat;

        /// <param name="createStorage">Makes each new chunk's storage from its sample count (2G); flat floats if null.</param>
        public World(Vector3Int chunkSize, float voxelSize, Func<Vector3Int, IVoxelStorage> createStorage = null)
        {
            Grid = new WorldGrid(chunkSize, voxelSize);
            this.createStorage = createStorage ?? (samples => new FlatVoxelStorage(samples));
        }

        public WorldGrid Grid { get; }

        /// <summary>Every loaded chunk by coordinate.</summary>
        public IReadOnlyDictionary<Vector3Int, Chunk> Chunks => loaded;

        public int LoadedCount => loaded.Count;

        /// <summary>Chunks touched by any edit, loaded or kept in memory.</summary>
        public int EditedCount => edited.Count;

        /// <summary>Edited chunks that are unloaded but kept in memory until saving exists, by coordinate.</summary>
        public IReadOnlyDictionary<Vector3Int, Chunk> KeptChunks => editedUnloaded;

        public bool IsLoaded(Vector3Int coord)
        {
            return loaded.ContainsKey(coord);
        }

        public bool TryGetChunk(Vector3Int coord, out Chunk chunk)
        {
            return loaded.TryGetValue(coord, out chunk);
        }

        /// <summary>True once any edit has touched the chunk.</summary>
        public bool IsEdited(Vector3Int coord)
        {
            return edited.Contains(coord);
        }

        /// <summary>Makes each new chunk's density storage from its sample count (2G).</summary>
        public Func<Vector3Int, IVoxelStorage> StorageFactory => createStorage;

        /// <summary>
        /// Loads a chunk and waits for it (labs, tests, benchmarks): an edited one comes back as
        /// it was; otherwise it's generated, then takes its shared borders from edited loaded
        /// neighbours. Returns the loaded chunk (the existing one if it was already loaded).
        /// The streamed world loads through <see cref="ChunkPipeline"/> instead, which generates
        /// in jobs and calls <see cref="Add"/> when they finish.
        /// </summary>
        /// <param name="generator">The terrain; null leaves the chunk empty (all air).</param>
        /// <param name="layers">Materials by depth for generated chunks (M10); null leaves them all id 0.</param>
        /// <param name="ores">Ore nodes for generated chunks (3D); null for none.</param>
        /// <param name="trees">Trees for generated chunks (GL30); null for none.</param>
        public Chunk Load(
            Vector3Int coord, ITerrainGenerator generator, TerrainLayers layers = null, OreField ores = null, TreeField trees = null)
        {
            if (loaded.TryGetValue(coord, out Chunk existing))
            {
                return existing;
            }
            if (TryRestoreKept(coord, out Chunk kept))
            {
                return kept;
            }

            Chunk chunk = generator != null
                ? ChunkGenerator.Generate(generator, ChunkSampleGrid.ForChunk(Grid, coord), createStorage, layers, ores, PreferredFormat, trees)
                : Chunk.Uniform(Grid.ChunkSize + Vector3Int.one, 0f, 0, createStorage);
            Add(coord, chunk);
            return chunk;
        }

        /// <summary>
        /// The density format generation should write for this world's storage: bytes when the
        /// storage holds bytes (M12), so they copy straight in (K35); floats otherwise.
        /// </summary>
        public DensityFormat PreferredFormat
        {
            get
            {
                if (!preferredFormat.HasValue)
                {
                    preferredFormat = createStorage(new Vector3Int(2, 2, 2)) is ByteVoxelStorage ? DensityFormat.Byte : DensityFormat.Float;
                }
                return preferredFormat.Value;
            }
        }

        /// <summary>
        /// Adds a freshly generated chunk (from <see cref="ChunkPipeline"/>, once its job is done):
        /// it takes its shared borders from any edited loaded neighbour, so edits made while it
        /// was generating aren't contradicted (M2).
        /// </summary>
        public void Add(Vector3Int coord, Chunk chunk)
        {
            loaded.Add(coord, chunk);
            loadedMin = anyLoaded ? Vector3Int.Min(loadedMin, coord) : coord;
            loadedMax = anyLoaded ? Vector3Int.Max(loadedMax, coord) : coord;
            anyLoaded = true;
            chunk.BecameDirty = _ => dirty.Add(coord);
            if (chunk.IsDirty)
            {
                dirty.Add(coord);
            }
            CopyBordersFromEditedNeighbours(coord, chunk);
        }

        /// <summary>
        /// Adds every loaded chunk that is dirty now to <paramref name="into"/> (cleared first), and
        /// forgets the ones cleaned since they were noted: what needs meshing, found without
        /// scanning every loaded chunk.
        /// </summary>
        public void CollectDirty(List<Vector3Int> into)
        {
            into.Clear();
            foreach (Vector3Int coord in dirty)
            {
                if (loaded.TryGetValue(coord, out Chunk chunk) && chunk.IsDirty)
                {
                    into.Add(coord);
                }
            }
            if (into.Count != dirty.Count)
            {
                dirty.Clear();
                dirty.UnionWith(into);
            }
        }

        /// <summary>
        /// Brings back an edited chunk kept in memory since it was unloaded, if there is one: it
        /// needs no generating. Its borders are refreshed from edited neighbours.
        /// </summary>
        public bool TryRestoreKept(Vector3Int coord, out Chunk chunk)
        {
            if (!editedUnloaded.TryGetValue(coord, out chunk))
            {
                return false;
            }

            // A neighbour may have been edited along the shared border while this was away.
            editedUnloaded.Remove(coord);
            chunk.MarkDirty();
            Add(coord, chunk);
            return true;
        }

        /// <summary>
        /// Unloads a chunk; an edited one is kept so it can come back. An unedited one can be
        /// generated again, so its memory goes back to the pool (<see cref="Chunk.Release"/>).
        /// </summary>
        public void Unload(Vector3Int coord)
        {
            if (!loaded.TryGetValue(coord, out Chunk chunk))
            {
                return;
            }
            loaded.Remove(coord);
            dirty.Remove(coord);
            chunk.BecameDirty = null;
            if (edited.Contains(coord))
            {
                editedUnloaded[coord] = chunk;
            }
            else
            {
                chunk.Release();
            }
        }

        /// <summary>Forgets every chunk, edited ones too, e.g. before regenerating the terrain; their memory goes back to the pool.</summary>
        public void Clear()
        {
            foreach (Chunk chunk in loaded.Values)
            {
                chunk.Release();
            }
            foreach (Chunk chunk in editedUnloaded.Values)
            {
                chunk.Release();
            }
            loaded.Clear();
            dirty.Clear();
            anyLoaded = false;
            editedUnloaded.Clear();
            edited.Clear();
        }

        /// <summary>The density at a global sample, or null when no loaded chunk holds it.</summary>
        public float? GetDensity(Vector3Int sample)
        {
            Grid.ChunksContainingSample(sample, scratchChunks);
            foreach (Vector3Int coord in scratchChunks)
            {
                if (loaded.TryGetValue(coord, out Chunk chunk))
                {
                    return chunk.GetDensity(Grid.GlobalToLocal(sample, coord));
                }
            }
            return null;
        }

        /// <summary>
        /// The world's single density-edit path (A7): writes every loaded copy of a
        /// global sample, so chunk borders stay in step (M2, M5). Returns how many
        /// chunks changed.
        /// </summary>
        public int SetDensity(Vector3Int sample, float density)
        {
            Grid.ChunksContainingSample(sample, scratchChunks);
            int changed = 0;
            foreach (Vector3Int coord in scratchChunks)
            {
                if (!loaded.TryGetValue(coord, out Chunk chunk))
                {
                    continue;
                }
                Vector3Int local = Grid.GlobalToLocal(sample, coord);
                if (chunk.GetDensity(local) != density)
                {
                    chunk.SetDensity(local, density);
                    edited.Add(coord);
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>The material id at a global sample (M10), or null when no loaded chunk holds it.</summary>
        public byte? GetMaterial(Vector3Int sample)
        {
            Grid.ChunksContainingSample(sample, scratchChunks);
            foreach (Vector3Int coord in scratchChunks)
            {
                if (loaded.TryGetValue(coord, out Chunk chunk))
                {
                    return chunk.GetMaterial(Grid.GlobalToLocal(sample, coord));
                }
            }
            return null;
        }

        /// <summary>
        /// Writes a global sample's material id to every loaded copy (M10), like
        /// <see cref="SetDensity"/>, so both sides of a border show the same material.
        /// </summary>
        public void SetMaterial(Vector3Int sample, byte material)
        {
            Grid.ChunksContainingSample(sample, scratchChunks);
            foreach (Vector3Int coord in scratchChunks)
            {
                if (!loaded.TryGetValue(coord, out Chunk chunk))
                {
                    continue;
                }
                Vector3Int local = Grid.GlobalToLocal(sample, coord);
                if (chunk.GetMaterial(local) != material)
                {
                    chunk.SetMaterial(local, material);
                    edited.Add(coord);
                }
            }
        }

        /// <summary>True when the sample meshes as a sharp cube (GL86); false when not loaded.</summary>
        public bool IsSquare(Vector3Int sample)
        {
            Grid.ChunksContainingSample(sample, scratchChunks);
            foreach (Vector3Int coord in scratchChunks)
            {
                if (loaded.TryGetValue(coord, out Chunk chunk))
                {
                    return chunk.IsSquare(Grid.GlobalToLocal(sample, coord));
                }
            }
            return false;
        }

        /// <summary>Marks a sample square or not in every loaded chunk that holds a copy of it (GL86, A7, like <see cref="SetMaterial"/>).</summary>
        public void SetSquare(Vector3Int sample, bool square)
        {
            Grid.ChunksContainingSample(sample, scratchChunks);
            foreach (Vector3Int coord in scratchChunks)
            {
                if (!loaded.TryGetValue(coord, out Chunk chunk))
                {
                    continue;
                }
                Vector3Int local = Grid.GlobalToLocal(sample, coord);
                if (chunk.IsSquare(local) != square)
                {
                    chunk.SetSquare(local, square);
                    edited.Add(coord);
                }
            }
        }

        /// <summary>
        /// Adds or removes a sphere of terrain across every loaded chunk it reaches (M5).
        /// The brush works on the world as one density field, so it sees across borders
        /// and each write goes through <see cref="SetDensity"/> to every copy (A7).
        /// </summary>
        /// <param name="centre">Brush centre in world units, relative to the world origin.</param>
        public BrushResult ApplyBrush(Vector3 centre, BrushSettings brush, BrushOperation operation)
        {
            return TerrainBrush.Apply(this, centre, Grid.VoxelSize, brush, operation);
        }

        bool IDensityField.TryGetDensity(Vector3Int sample, out float density)
        {
            float? value = GetDensity(sample);
            density = value ?? 0f;
            return value.HasValue;
        }

        void IDensityField.SetDensity(Vector3Int sample, float density)
        {
            SetDensity(sample, density);
        }

        /// <summary>
        /// Where a ray first hits the surface of any loaded chunk, without meshes or
        /// colliders (<see cref="SurfaceRaycast"/> per chunk, nearest chunks first). Walks the
        /// chunk grid along the ray, so its cost depends on the ray's length, not on how many
        /// chunks are loaded.
        /// </summary>
        /// <param name="ray">Relative to the world origin.</param>
        /// <param name="maxDistance">Chunks entered beyond this distance aren't tested.</param>
        public bool Raycast(Ray ray, ChunkMeshSettings settings, float maxDistance, out WorldHit hit)
        {
            hit = default;
            if (!anyLoaded)
            {
                return false;
            }
            Vector3 size = Grid.ChunkWorldSize;
            Vector3 direction = ray.direction.normalized;

            // Only the part of the ray inside the box of loaded chunks.
            Vector3 boxMin = Grid.ChunkOrigin(loadedMin);
            Vector3 boxMax = Grid.ChunkOrigin(loadedMax + Vector3Int.one);
            float near = 0f;
            float far = maxDistance;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 1e-8f)
                {
                    if (ray.origin[axis] < boxMin[axis] || ray.origin[axis] > boxMax[axis])
                    {
                        return false;
                    }
                    continue;
                }
                float t0 = (boxMin[axis] - ray.origin[axis]) / direction[axis];
                float t1 = (boxMax[axis] - ray.origin[axis]) / direction[axis];
                near = Mathf.Max(near, Mathf.Min(t0, t1));
                far = Mathf.Min(far, Mathf.Max(t0, t1));
            }
            if (near > far)
            {
                return false;
            }

            // Grid walk (Amanatides & Woo) from the chunk the ray enters the box in. tNext is the
            // distance at which the ray crosses into the next chunk along each axis.
            Vector3 start = ray.origin + direction * near;
            Vector3Int cell = Vector3Int.Min(Vector3Int.Max(Grid.WorldToChunk(start), loadedMin), loadedMax);
            var step = new Vector3Int(direction.x > 0f ? 1 : -1, direction.y > 0f ? 1 : -1, direction.z > 0f ? 1 : -1);
            Vector3 tNext = default;
            Vector3 tDelta = default;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 1e-8f)
                {
                    tNext[axis] = float.PositiveInfinity;
                    tDelta[axis] = float.PositiveInfinity;
                    continue;
                }
                float boundary = (cell[axis] + (step[axis] > 0 ? 1 : 0)) * size[axis];
                tNext[axis] = (boundary - ray.origin[axis]) / direction[axis];
                tDelta[axis] = size[axis] / Mathf.Abs(direction[axis]);
            }

            // A chunk entered later can still hold a slightly nearer hit than one entered
            // earlier (a surface ending at a border), so keep the nearest hit and stop once
            // chunks start beyond it.
            float best = float.PositiveInfinity;
            float entry = near;
            while (entry <= far && entry <= best)
            {
                // Every sample the same, border samples too: no surface crosses the chunk.
                if (loaded.TryGetValue(cell, out Chunk chunk) && !chunk.IsUniform)
                {
                    Vector3 origin = Grid.ChunkOrigin(cell);
                    var localRay = new Ray(ray.origin - origin, direction);
                    if (SurfaceRaycast.Cast(localRay, chunk, settings, out Vector3Int voxel, out Vector3 point))
                    {
                        float distance = Vector3.Dot(point - localRay.origin, direction);
                        if (distance < best)
                        {
                            best = distance;
                            hit = new WorldHit(cell, voxel, point + origin);
                        }
                    }
                }

                int next = tNext.x < tNext.y ? (tNext.x < tNext.z ? 0 : 2) : (tNext.y < tNext.z ? 1 : 2);
                entry = tNext[next];
                tNext[next] += tDelta[next];
                cell[next] += step[next];
                if (cell[next] < loadedMin[next] || cell[next] > loadedMax[next])
                {
                    break;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        // Any loaded neighbour that was edited may hold different values on the shared
        // border than the freshly generated chunk; its values win, so the meshes meet.
        private void CopyBordersFromEditedNeighbours(Vector3Int coord, Chunk chunk)
        {
            Vector3Int size = Grid.ChunkSize;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var offset = new Vector3Int(dx, dy, dz);
                        Vector3Int neighbourCoord = coord + offset;
                        if (offset == Vector3Int.zero || !edited.Contains(neighbourCoord)
                            || !loaded.TryGetValue(neighbourCoord, out Chunk neighbour))
                        {
                            continue;
                        }

                        // The shared samples, in this chunk's local coordinates: the face,
                        // edge or corner on the neighbour's side.
                        Vector3Int from = new Vector3Int(
                            dx > 0 ? size.x : 0, dy > 0 ? size.y : 0, dz > 0 ? size.z : 0);
                        Vector3Int to = new Vector3Int(
                            dx < 0 ? 0 : size.x, dy < 0 ? 0 : size.y, dz < 0 ? 0 : size.z);
                        Vector3Int shift = Vector3Int.Scale(offset, size);
                        for (int z = from.z; z <= to.z; z++)
                        {
                            for (int y = from.y; y <= to.y; y++)
                            {
                                for (int x = from.x; x <= to.x; x++)
                                {
                                    var local = new Vector3Int(x, y, z);
                                    chunk.SetDensity(local, neighbour.GetDensity(local - shift));
                                    chunk.SetMaterial(local, neighbour.GetMaterial(local - shift));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>Where a <see cref="World.Raycast"/> hit the surface.</summary>
    public readonly struct WorldHit
    {
        public WorldHit(Vector3Int chunk, Vector3Int voxel, Vector3 point)
        {
            Chunk = chunk;
            Voxel = voxel;
            Point = point;
        }

        /// <summary>The chunk whose surface was hit.</summary>
        public Vector3Int Chunk { get; }

        /// <summary>The hit voxel, chunk-local.</summary>
        public Vector3Int Voxel { get; }

        /// <summary>The hit point, relative to the world origin.</summary>
        public Vector3 Point { get; }
    }
}
