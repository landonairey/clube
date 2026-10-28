using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One tree drawn far off (GL162): where it stands, how tall it is, and its kind.</summary>
    public readonly struct DistantTree
    {
        public readonly Vector3 Root;
        public readonly float Height;
        public readonly bool Maple;

        /// <summary>0-1, from the tree's cell: its turn and its width.</summary>
        public readonly float Variation;

        public DistantTree(Vector3 root, float height, bool maple, float variation)
        {
            Root = root;
            Height = height;
            Maple = maple;
            Variation = variation;
        }
    }

    /// <summary>
    /// Where the trees stand out to kilometres (GL162), without building any (<see cref="TreeField"/>
    /// builds a tree's parts for the voxels; drawn far off, a tree is a position, a height and a kind).
    /// The ground is cut into square tiles round the focus; a tile's candidates come from
    /// <see cref="TreeField.TryGetSpot"/>, its heights (the root and the four flatness probes) from a
    /// job on the generator (<see cref="HeightfieldGenerator.SchedulePointHeights"/>), and the trees
    /// kept are the ones on ground flat enough to grow them: the same trees the voxel world grows.
    /// </summary>
    public sealed class DistantTreeBuilder : IDisposable
    {
        public const float TileSize = 128f;

        // Points per candidate: the root, then east, west, north, south probes.
        private const int PointsPerCandidate = 5;

        private readonly HeightfieldGenerator generator;
        private readonly TreeField trees;
        private readonly float range;
        private readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
        private readonly List<Vector2Int> wanted = new List<Vector2Int>();
        private readonly List<Vector2Int> scratch = new List<Vector2Int>();
        private Vector2Int focusTile;
        private bool hasFocus;
        private int nextWanted;

        public sealed class Tile
        {
            public Vector2Int Coord;
            public readonly List<DistantTree> Trees = new List<DistantTree>();
            public bool Ready;

            internal readonly List<Vector2Int> Cells = new List<Vector2Int>();
            internal NativeArray<float2> Points;
            internal NativeArray<float> Heights;
            internal JobHandle Job;
            internal bool Running;

            /// <summary>The tile's centre (x, z), in metres.</summary>
            public Vector2 Centre => ((Vector2)Coord + Vector2.one * 0.5f) * TileSize;

            internal void Free()
            {
                Job.Complete();
                if (Points.IsCreated)
                {
                    Points.Dispose();
                }
                if (Heights.IsCreated)
                {
                    Heights.Dispose();
                }
                Running = false;
            }
        }

        public DistantTreeBuilder(HeightfieldGenerator generator, TreeField trees, float range)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.trees = trees ?? throw new ArgumentNullException(nameof(trees));
            this.range = Mathf.Max(TileSize, range);
        }

        /// <summary>Tiles started per frame.</summary>
        public int MaxStartsPerFrame { get; set; } = 16;

        /// <summary>Goes up whenever the ready tiles change, so a view knows to gather them again.</summary>
        public int Version { get; private set; }

        public float Range => range;

        /// <summary>Every tile in reach, ready or not.</summary>
        public IEnumerable<Tile> Tiles => tiles.Values;

        /// <summary>Tiles still waiting for their trees.</summary>
        public int Pending
        {
            get
            {
                int pending = wanted.Count - nextWanted;
                foreach (Tile tile in tiles.Values)
                {
                    if (!tile.Ready)
                    {
                        pending++;
                    }
                }
                return pending;
            }
        }

        /// <summary>Call once a frame with the focus (world-view space).</summary>
        public void Update(Vector3 focus)
        {
            var tile = new Vector2Int(Mathf.FloorToInt(focus.x / TileSize), Mathf.FloorToInt(focus.z / TileSize));
            if (!hasFocus || tile != focusTile)
            {
                hasFocus = true;
                focusTile = tile;
                Retarget(new Vector2(focus.x, focus.z));
            }

            int starts = MaxStartsPerFrame;
            while (starts > 0 && nextWanted < wanted.Count)
            {
                Vector2Int coord = wanted[nextWanted++];
                if (!tiles.ContainsKey(coord))
                {
                    Start(coord);
                    starts--;
                }
            }

            foreach (Tile running in tiles.Values)
            {
                if (running.Running && running.Job.IsCompleted)
                {
                    Finish(running);
                }
            }
            JobHandle.ScheduleBatchedJobs();
        }

        public void Dispose()
        {
            foreach (Tile tile in tiles.Values)
            {
                tile.Free();
            }
            tiles.Clear();
        }

        // The tiles in reach, nearest first; tiles that left it go.
        private void Retarget(Vector2 focus)
        {
            int reach = Mathf.CeilToInt(range / TileSize) + 1;
            float limit = range + TileSize;
            wanted.Clear();
            for (int z = -reach; z <= reach; z++)
            {
                for (int x = -reach; x <= reach; x++)
                {
                    var coord = new Vector2Int(focusTile.x + x, focusTile.y + z);
                    if ((((Vector2)coord + Vector2.one * 0.5f) * TileSize - focus).sqrMagnitude <= limit * limit)
                    {
                        wanted.Add(coord);
                    }
                }
            }
            wanted.Sort((a, b) =>
                ((((Vector2)a + Vector2.one * 0.5f) * TileSize) - focus).sqrMagnitude.CompareTo(((((Vector2)b + Vector2.one * 0.5f) * TileSize) - focus).sqrMagnitude));
            nextWanted = 0;

            // Drop tiles well out of reach (a tile's width of slack, so walking back and forth doesn't redo them).
            float keep = limit + TileSize;
            scratch.Clear();
            foreach (Tile tile in tiles.Values)
            {
                if ((tile.Centre - focus).sqrMagnitude > keep * keep)
                {
                    scratch.Add(tile.Coord);
                }
            }
            foreach (Vector2Int coord in scratch)
            {
                bool wasReady = tiles[coord].Ready;
                tiles[coord].Free();
                tiles.Remove(coord);
                if (wasReady)
                {
                    Version++;
                }
            }
        }

        // The tile's candidate trees, and a job for the heights under them.
        private void Start(Vector2Int coord)
        {
            var tile = new Tile { Coord = coord };
            tiles.Add(coord, tile);
            float cellSize = trees.CellSize;
            int fromX = Mathf.FloorToInt(coord.x * TileSize / cellSize);
            int fromZ = Mathf.FloorToInt(coord.y * TileSize / cellSize);
            int toX = Mathf.FloorToInt((coord.x + 1) * TileSize / cellSize);
            int toZ = Mathf.FloorToInt((coord.y + 1) * TileSize / cellSize);
            var spots = new List<Vector2>();
            for (int z = fromZ; z < toZ; z++)
            {
                for (int x = fromX; x < toX; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (trees.TryGetSpot(cell, out Vector2 spot))
                    {
                        tile.Cells.Add(cell);
                        spots.Add(spot);
                    }
                }
            }
            if (spots.Count == 0)
            {
                tile.Ready = true;
                return;
            }

            float probe = TreeField.FlatnessProbe;
            tile.Points = new NativeArray<float2>(spots.Count * PointsPerCandidate, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            tile.Heights = new NativeArray<float>(spots.Count * PointsPerCandidate, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < spots.Count; i++)
            {
                var root = new float2(spots[i].x, spots[i].y);
                int first = i * PointsPerCandidate;
                tile.Points[first] = root;
                tile.Points[first + 1] = root + new float2(probe, 0f);
                tile.Points[first + 2] = root - new float2(probe, 0f);
                tile.Points[first + 3] = root + new float2(0f, probe);
                tile.Points[first + 4] = root - new float2(0f, probe);
            }
            tile.Job = generator.SchedulePointHeights(tile.Points, tile.Heights);
            tile.Running = true;
        }

        // The candidates on flat enough ground become the tile's trees.
        private void Finish(Tile tile)
        {
            tile.Job.Complete();
            TreeGeneration settings = trees.Settings;
            for (int i = 0; i < tile.Cells.Count; i++)
            {
                int first = i * PointsPerCandidate;
                float ground = tile.Heights[first];
                if (!trees.IsFlatEnough(ground, tile.Heights[first + 1], tile.Heights[first + 2], tile.Heights[first + 3], tile.Heights[first + 4]))
                {
                    continue;
                }
                Vector2Int cell = tile.Cells[i];
                float2 root = tile.Points[first];
                tile.Trees.Add(new DistantTree(
                    new Vector3(root.x, ground, root.y),
                    TreeShape.HeightOf(trees.Seed, cell, settings),
                    TreeShape.IsMaple(trees.Seed, cell, settings),
                    VoxelHash.Uniform(trees.Seed, cell.x, 7, cell.y, 0x44495354)));
            }
            tile.Free();
            tile.Cells.Clear();
            tile.Ready = true;
            Version++;
        }
    }
}
