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
    /// Neighbouring chunks each keep a copy of their shared border samples, so the
    /// world keeps every copy identical (M2), which is what makes meshes meet without
    /// cracks:
    /// <list type="bullet">
    /// <item>Generation samples world positions, so fresh chunks agree on borders.</item>
    /// <item><see cref="SetDensity"/> writes every loaded copy of a sample (A7).</item>
    /// <item><see cref="ApplyBrush"/> applies the brush to every chunk it reaches; the
    /// brush's result depends only on a sample's value and position, so copies stay
    /// equal (M5).</item>
    /// <item>A newly loaded chunk takes its shared border samples from any loaded
    /// neighbour that was edited, so edits made next to it aren't contradicted.</item>
    /// </list>
    /// Edited chunks are kept in memory when unloaded and come back as they were, until
    /// saving exists (Chapter 4).
    /// </remarks>
    public sealed class World
    {
        private readonly Dictionary<Vector3Int, Chunk> loaded = new Dictionary<Vector3Int, Chunk>();
        private readonly Dictionary<Vector3Int, Chunk> editedUnloaded = new Dictionary<Vector3Int, Chunk>();
        private readonly HashSet<Vector3Int> edited = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> scratchChunks = new List<Vector3Int>();
        private readonly List<(Chunk Chunk, Vector3Int Coord, float Entry)> raycastCandidates =
            new List<(Chunk, Vector3Int, float)>();

        public World(Vector3Int chunkSize, float voxelSize)
        {
            Grid = new WorldGrid(chunkSize, voxelSize);
        }

        public WorldGrid Grid { get; }

        /// <summary>Every loaded chunk by coordinate.</summary>
        public IReadOnlyDictionary<Vector3Int, Chunk> Chunks => loaded;

        public int LoadedCount => loaded.Count;

        /// <summary>Chunks touched by any edit, loaded or kept in memory.</summary>
        public int EditedCount => edited.Count;

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

        /// <summary>
        /// Loads a chunk: an edited one comes back as it was; otherwise it's generated,
        /// then takes its shared borders from edited loaded neighbours. Returns the
        /// loaded chunk (the existing one if it was already loaded).
        /// </summary>
        public Chunk Load(Vector3Int coord, ITerrainGenerator generator)
        {
            if (loaded.TryGetValue(coord, out Chunk existing))
            {
                return existing;
            }

            if (editedUnloaded.TryGetValue(coord, out Chunk kept))
            {
                // A neighbour may have been edited along the shared border while this was away.
                editedUnloaded.Remove(coord);
                kept.MarkDirty();
                loaded.Add(coord, kept);
                CopyBordersFromEditedNeighbours(coord, kept);
                return kept;
            }

            var chunk = new Chunk(Grid.ChunkSize);
            if (generator != null)
            {
                ChunkGenerator.Fill(chunk, generator, Grid.ChunkOrigin(coord), Grid.VoxelSize);
            }
            loaded.Add(coord, chunk);
            CopyBordersFromEditedNeighbours(coord, chunk);
            return chunk;
        }

        /// <summary>Unloads a chunk; an edited one is kept so it can come back.</summary>
        public void Unload(Vector3Int coord)
        {
            if (!loaded.TryGetValue(coord, out Chunk chunk))
            {
                return;
            }
            loaded.Remove(coord);
            if (edited.Contains(coord))
            {
                editedUnloaded[coord] = chunk;
            }
        }

        /// <summary>Forgets every chunk, edited ones too, e.g. before regenerating the terrain.</summary>
        public void Clear()
        {
            loaded.Clear();
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

        /// <summary>
        /// Adds or removes a sphere of terrain across every loaded chunk it reaches (M5),
        /// through <see cref="TerrainBrush"/> and so each chunk's edit path (A7).
        /// </summary>
        /// <param name="centre">Brush centre in world units, relative to the world origin.</param>
        /// <returns>How many samples changed, counting each chunk's copy.</returns>
        public int ApplyBrush(Vector3 centre, BrushSettings brush, BrushOperation operation)
        {
            float reach = brush.Falloff == BrushFalloff.Hard ? brush.Radius + TerrainDensity.RampHalfWidth : brush.Radius;
            Vector3Int min = Vector3Int.FloorToInt((centre - Vector3.one * reach) / Grid.VoxelSize);
            Vector3Int max = Vector3Int.CeilToInt((centre + Vector3.one * reach) / Grid.VoxelSize);
            Grid.ChunksOverlapping(min, max, scratchChunks);

            int changed = 0;
            foreach (Vector3Int coord in scratchChunks)
            {
                if (!loaded.TryGetValue(coord, out Chunk chunk))
                {
                    continue;
                }
                int chunkChanged = TerrainBrush.Apply(chunk, centre - Grid.ChunkOrigin(coord), Grid.VoxelSize, brush, operation);
                if (chunkChanged > 0)
                {
                    edited.Add(coord);
                    changed += chunkChanged;
                }
            }
            return changed;
        }

        /// <summary>
        /// Where a ray first hits the surface of any loaded chunk, without meshes or
        /// colliders (<see cref="SurfaceRaycast"/> per chunk, nearest chunks first).
        /// </summary>
        /// <param name="ray">Relative to the world origin.</param>
        /// <param name="maxDistance">Chunks entered beyond this distance aren't tested.</param>
        public bool Raycast(Ray ray, ChunkMeshSettings settings, float maxDistance, out WorldHit hit)
        {
            hit = default;
            Vector3 size = Grid.ChunkWorldSize;
            raycastCandidates.Clear();
            foreach (KeyValuePair<Vector3Int, Chunk> entry in loaded)
            {
                var bounds = new Bounds(Grid.ChunkOrigin(entry.Key) + size * 0.5f, size);
                if (bounds.IntersectRay(ray, out float entryDistance) && entryDistance <= maxDistance)
                {
                    raycastCandidates.Add((entry.Value, entry.Key, entryDistance));
                }
            }
            raycastCandidates.Sort((a, b) => a.Entry.CompareTo(b.Entry));

            // A chunk entered later can still hold a nearer hit than one entered earlier,
            // so keep the nearest hit and stop once chunks start beyond it.
            float best = float.PositiveInfinity;
            foreach ((Chunk chunk, Vector3Int coord, float entry) in raycastCandidates)
            {
                if (entry > best)
                {
                    break;
                }

                Vector3 origin = Grid.ChunkOrigin(coord);
                var localRay = new Ray(ray.origin - origin, ray.direction);
                if (SurfaceRaycast.Cast(localRay, chunk, settings, out Vector3Int voxel, out Vector3 point))
                {
                    float distance = Vector3.Dot(point - localRay.origin, ray.direction.normalized);
                    if (distance < best)
                    {
                        best = distance;
                        hit = new WorldHit(coord, voxel, point + origin);
                    }
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
