using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What decides a chunk's grass tufts (GL67, GL234, GL235, GL265): the ground material and grid, how tall grass and
    /// wheat grow, and where dahlias stand. Carried by <see cref="GrassScatterJob"/> (GL775).
    /// </summary>
    public struct GrassTuftRules
    {
        // The grid's rolls: x and z of the point in a cell, its turn, its height, and whether it's wheat.
        public const int SaltX = 1;
        public const int SaltZ = 2;
        public const int SaltYaw = 3;
        public const int SaltHeight = 4;
        public const int SaltWheat = 5;
        public const int SaltDahlia = 6;
        public const int SaltDahliaStray = 7;
        public const int SaltDahliaColour = 8;
        public const int SaltThin = 9;

        /// <summary>The grass material's id: tufts stand on triangles mostly showing it.</summary>
        public float GroundId;

        /// <summary>The tuft grid's cell in metres (one over the square root of the density); 0 or less grows nothing.</summary>
        public float Cell;

        /// <summary>How upward a triangle must face (the y of its normal).</summary>
        public float MinUp;

        /// <summary>Tuft height in metres under dense trees, smallest and largest.</summary>
        public Vector2 Height;

        /// <summary>The world's seed, for the wheat fields.</summary>
        public int Seed;

        public GrassGrowthRules Growth;
        public DahliaRules Dahlias;
    }

    /// <summary>A dahlia among the grass: its colour (0 to <see cref="DahliaGrowth.Colours"/> - 1) and where it stands.</summary>
    public struct DahliaTuft
    {
        public int Colour;
        public float4x4 Matrix;
    }

    /// <summary>
    /// Scatters one chunk's grass in a Burst job (GL775): the ground spots on its mesh (<see cref="GroundSpotSearch"/>), then per
    /// spot a grass tuft, a wheat stalk or a dahlia, as tall as the place grows it; grass and wheat sorted by their thinning
    /// pick (GL379). What <see cref="GrassTuftsReference"/> does on the main thread; <c>GrassScatterJobTests</c> checks they agree.
    /// </summary>
    [BurstCompile]
    public struct GrassScatterJob : IJob
    {
        [ReadOnly] public Mesh.MeshDataArray Meshes;
        public int MeshIndex;

        /// <summary>The chunk mesh's place in the world, and the world's space from there (where trees and nodes are).</summary>
        public float4x4 ChunkToWorld;
        public float4x4 WorldToLocal;

        public GrassTuftRules Rules;

        /// <summary>The tree cover round the chunk (<see cref="TreeCover.Fill"/>).</summary>
        public TreeCoverGrid Cover;
        [ReadOnly] public NativeArray<float3> Roots;

        /// <summary>The gold nodes dahlias grow over, in the world's space, and each one's patch colour.</summary>
        [ReadOnly] public NativeArray<Vector3> DahliaNodes;
        [ReadOnly] public NativeArray<int> DahliaPatches;

        public NativeList<float4x4> Grass;
        public NativeList<float4x4> Wheat;
        public NativeList<DahliaTuft> Dahlias;

        private struct Pick
        {
            public float Value;
            public int Order;
            public float4x4 Matrix;
        }

        private struct ByPick : IComparer<Pick>
        {
            public int Compare(Pick a, Pick b)
            {
                int byValue = a.Value.CompareTo(b.Value);
                return byValue != 0 ? byValue : a.Order.CompareTo(b.Order);
            }
        }

        public void Execute()
        {
            Grass.Clear();
            Wheat.Clear();
            Dahlias.Clear();
            if (Rules.Cell <= 0f)
            {
                return;
            }
            GroundMesh mesh = GroundMesh.Read(Meshes[MeshIndex], Allocator.Temp);
            var spots = new NativeList<GroundSpot>(256, Allocator.Temp);
            var grounds = new FixedList32Bytes<float>();
            grounds.Add(Rules.GroundId);
            GroundSpotSearch.Find(mesh, ChunkToWorld, grounds, Rules.MinUp, Rules.Cell, 0, GrassTuftRules.SaltX, GrassTuftRules.SaltZ, 0f, ref spots);

            var grassPicks = new NativeList<Pick>(spots.Length, Allocator.Temp);
            var wheatPicks = new NativeList<Pick>(Allocator.Temp);
            float3 up = new float3(0f, 1f, 0f);
            for (int s = 0; s < spots.Length; s++)
            {
                GroundSpot spot = spots[s];
                int ix = spot.Cell.x, iz = spot.Cell.y;
                float3 position = spot.Position;
                float3 local = math.transform(WorldToLocal, position);
                float forest = Cover.At(Roots, local.x, local.z);
                float yaw = VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltYaw) * 360f;
                float roll = VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltHeight);
                quaternion turn = quaternion.RotateY(math.radians(yaw));
                quaternion rotation = math.mul(FromTo(up, math.lerp(up, spot.Normal, 0.5f)), turn);

                if (DahliaNodes.Length > 0)
                {
                    float share = Rules.Dahlias.ShareAt(DahliaNodes, local, out int node);
                    if (share > 0f && VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltDahlia) < share)
                    {
                        int colour = DahliaRules.ColourOf(DahliaPatches[node], VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltDahliaStray),
                            VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltDahliaColour));
                        float tall = Rules.Dahlias.HeightFor(roll);
                        // Upright: flowers stand straight whatever the slope.
                        Dahlias.Add(new DahliaTuft { Colour = colour, Matrix = float4x4.TRS(position, turn, new float3(tall)) });
                        continue;
                    }
                }

                float wheatHere = Rules.Growth.WheatAt(Rules.Seed, local.x, local.z, forest);
                if (wheatHere > 0f && VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltWheat) < wheatHere)
                {
                    float tall = Rules.Growth.WheatHeightFor(roll);
                    wheatPicks.Add(new Pick
                    {
                        Value = VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltThin),
                        Order = s,
                        Matrix = float4x4.TRS(position, rotation, new float3(tall * 0.8f, tall, tall * 0.8f)),
                    });
                    continue;
                }
                float baseSize = Mathf.Lerp(Rules.Height.x, Rules.Height.y, roll);
                float scale = Rules.Growth.HeightScale(forest, local.y);
                // Taller grass is a little wider, but mostly it reaches up.
                float width = baseSize * Mathf.Sqrt(scale);
                grassPicks.Add(new Pick
                {
                    Value = VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltThin),
                    Order = s,
                    Matrix = float4x4.TRS(position, rotation, new float3(width, baseSize * scale, width)),
                });
            }
            Sorted(grassPicks, Grass);
            Sorted(wheatPicks, Wheat);
        }

        private static void Sorted(NativeList<Pick> picks, NativeList<float4x4> into)
        {
            picks.Sort(new ByPick());
            for (int i = 0; i < picks.Length; i++)
            {
                into.Add(picks[i].Matrix);
            }
        }

        /// <summary><see cref="Quaternion.FromToRotation"/>: the shortest turn from one direction to another.</summary>
        public static quaternion FromTo(float3 from, float3 to)
        {
            float3 a = math.normalizesafe(from), b = math.normalizesafe(to);
            float d = math.dot(a, b);
            if (d < -0.999999f)
            {
                // Opposite: half a turn about any axis square to them.
                float3 axis = math.cross(new float3(1f, 0f, 0f), a);
                if (math.lengthsq(axis) < 1e-6f)
                {
                    axis = math.cross(new float3(0f, 1f, 0f), a);
                }
                return quaternion.AxisAngle(math.normalize(axis), math.PI);
            }
            return math.normalize(new quaternion(new float4(math.cross(a, b), 1f + d)));
        }
    }

    /// <summary>
    /// The managed reference for <see cref="GrassScatterJob"/> (GL775): what <see cref="GrassScatter"/> did on the main thread
    /// before the job, kept so <c>GrassScatterJobTests</c> can check the job grows the same grass.
    /// </summary>
    public static class GrassTuftsReference
    {
        public static void Scatter(Mesh mesh, Matrix4x4 chunkToWorld, Matrix4x4 worldToLocal, GrassTuftRules rules, TreeCover trees,
            IReadOnlyList<Vector3> dahliaNodes, IReadOnlyList<int> dahliaPatches, List<Matrix4x4> grass, List<Matrix4x4> wheat, List<DahliaTuft> dahlias)
        {
            grass.Clear();
            wheat.Clear();
            dahlias.Clear();
            if (rules.Cell <= 0f)
            {
                return;
            }
            var spots = new List<GroundSpot>();
            new GrassyGround().Find(mesh, chunkToWorld, new[] { rules.GroundId }, rules.MinUp, rules.Cell, 0, GrassTuftRules.SaltX, GrassTuftRules.SaltZ, 0f, spots);
            var grassPicks = new List<(float Pick, Matrix4x4 Matrix)>();
            var wheatPicks = new List<(float Pick, Matrix4x4 Matrix)>();
            foreach (GroundSpot spot in spots)
            {
                int ix = spot.Cell.x, iz = spot.Cell.y;
                Vector3 local = worldToLocal.MultiplyPoint3x4(spot.Position);
                float forest = trees.At(local.x, local.z);
                float yaw = VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltYaw) * 360f;
                float roll = VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltHeight);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, spot.Normal, 0.5f)) * Quaternion.Euler(0f, yaw, 0f);

                if (dahliaNodes.Count > 0)
                {
                    int node = -1;
                    float share = 0f;
                    for (int i = 0; i < dahliaNodes.Count; i++)
                    {
                        float s = rules.Dahlias.ShareOf(dahliaNodes[i], local);
                        if (s > share)
                        {
                            share = s;
                            node = i;
                        }
                    }
                    if (share > 0f && VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltDahlia) < share)
                    {
                        int colour = DahliaRules.ColourOf(dahliaPatches[node], VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltDahliaStray),
                            VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltDahliaColour));
                        float tall = rules.Dahlias.HeightFor(roll);
                        dahlias.Add(new DahliaTuft { Colour = colour, Matrix = Matrix4x4.TRS(spot.Position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * tall) });
                        continue;
                    }
                }

                float wheatHere = rules.Growth.WheatAt(rules.Seed, local.x, local.z, forest);
                if (wheatHere > 0f && VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltWheat) < wheatHere)
                {
                    float tall = rules.Growth.WheatHeightFor(roll);
                    wheatPicks.Add((VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltThin), Matrix4x4.TRS(spot.Position, rotation, new Vector3(tall * 0.8f, tall, tall * 0.8f))));
                    continue;
                }
                float baseSize = Mathf.Lerp(rules.Height.x, rules.Height.y, roll);
                float scale = rules.Growth.HeightScale(forest, local.y);
                float width = baseSize * Mathf.Sqrt(scale);
                grassPicks.Add((VoxelHash.Uniform(0, ix, 0, iz, GrassTuftRules.SaltThin), Matrix4x4.TRS(spot.Position, rotation, new Vector3(width, baseSize * scale, width))));
            }
            grassPicks.Sort((a, b) => a.Pick.CompareTo(b.Pick));
            wheatPicks.Sort((a, b) => a.Pick.CompareTo(b.Pick));
            foreach ((float _, Matrix4x4 matrix) in grassPicks)
            {
                grass.Add(matrix);
            }
            foreach ((float _, Matrix4x4 matrix) in wheatPicks)
            {
                wheat.Add(matrix);
            }
        }
    }
}
