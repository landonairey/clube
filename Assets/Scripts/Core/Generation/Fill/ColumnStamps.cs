using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The tree parts, stamped rocks and ore nodes that reach a heightfield chunk column, gathered once for the whole column
    /// (GL774) instead of once per chunk. A chunk's own share is picked at the start of its fill job (<see cref="ScheduleFill"/>,
    /// Burst), so starting a chunk asks no field anything on the main thread; which layers anything
    /// reaches is known at once (<see cref="TreesReach"/>, <see cref="OresReach"/>), for the chunks made without a job and
    /// for proving a column uniform (GL773).
    /// </summary>
    /// <remarks>
    /// <para>A chunk gets exactly the parts and nodes it would gather itself, in the same order
    /// (<see cref="ChunkGenerator.CollectTrees(TreeField, IStampField, ChunkSampleGrid, Allocator)"/>,
    /// <see cref="ChunkGenerator.CollectOres"/>): the column's boxes have the chunk's footprint and contain its box, so the
    /// fields visit the same cells in the same order, and every part is kept with the bounds the chunk's own gathering
    /// tests. Only a chunk's height is left to test, which the job does. A chunk outside the boxes (<see cref="Covers"/>)
    /// gathers its own as before.</para>
    /// <para>Main thread only to gather; the arrays are read by jobs until they are disposed.</para>
    /// </remarks>
    public sealed class ColumnStamps : IDisposable
    {
        /// <summary>The layers a column's masks cover, from the bottom of the world.</summary>
        public const int Layers = UniformLayers.MaxLayers;

        private static readonly List<TreePart> Parts = new List<TreePart>();
        private static readonly List<Bounds> TreeBounds = new List<Bounds>();
        private static readonly List<OreNode> Nodes = new List<OreNode>();

        private NativeArray<Stamp> stamps;
        private NativeArray<Ore> ores;
        private NativeArray<TreePart> noParts = new NativeArray<TreePart>(0, Allocator.Persistent);
        private NativeArray<OreNodeData> noNodes = new NativeArray<OreNodeData>(0, Allocator.Persistent);
        private readonly float[] layerBottom = new float[Layers];
        private readonly float[] layerTop = new float[Layers];
        private readonly bool hasOres;
        private readonly bool exact;
        private float oreBottom, oreTop;
        private ulong treesLow, treesHigh, oresLow, oresHigh;

        private ColumnStamps(int stampCount, int oreCount, bool hasOres, bool exact)
        {
            stamps = new NativeArray<Stamp>(stampCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            ores = new NativeArray<Ore>(oreCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            this.hasOres = hasOres;
            this.exact = exact;
        }

        /// <summary>
        /// Gathers what reaches the column: every tree part and rock over its whole height, and the ore nodes up to above its
        /// highest ground or tree, whichever is higher (<paramref name="highest"/> is its highest ground, with its border).
        /// </summary>
        /// <param name="rampReach">How far over the ground the density ramp reaches (<see cref="ChunkFillSettings.RampReach"/>).</param>
        public static ColumnStamps Gather(
            WorldGrid grid, Vector2Int column, float highest, float rampReach, TreeField trees, IStampField boulders, OreField oreField)
        {
            Bounds first = ChunkSampleGrid.ForChunk(grid, new Vector3Int(column.x, 0, column.y)).Bounds;
            Bounds last = ChunkSampleGrid.ForChunk(grid, new Vector3Int(column.x, Layers - 1, column.y)).Bounds;
            float layerHeight = grid.ChunkWorldSize.y;
            Bounds tall = ColumnBox(first, first.min.y - layerHeight, last.max.y + layerHeight);

            // As ChunkGenerator.CollectTrees: the trees, then each stamp field's parts, each with what the chunk tests.
            Parts.Clear();
            TreeBounds.Clear();
            StreamingMarkers.GatherTrees.Begin();
            if (trees != null)
            {
                trees.CollectParts(tall, Parts, TreeBounds);
            }
            StreamingMarkers.GatherTrees.End();
            int treeParts = Parts.Count;
            StreamingMarkers.GatherRocks.Begin();
            boulders?.CollectParts(tall, Parts, TreeField.GatherMargin);
            StreamingMarkers.GatherRocks.End();
            float stampTop = float.MinValue;
            foreach (TreePart part in Parts)
            {
                stampTop = Mathf.Max(stampTop, part.Bounds(TreeField.GatherMargin).max.y);
            }

            // Ores up to a layer over the higher of the ground's ramp and the trees: no chunk above that gathers any (it is
            // air without a job), and asking the field about the sky costs a height lookup a cell.
            Bounds oreBox = default;
            if (oreField != null)
            {
                float top = Mathf.Min(Mathf.Max(highest + rampReach, stampTop) + 2f * layerHeight, last.max.y + layerHeight);
                oreBox = ColumnBox(first, first.min.y - layerHeight, Mathf.Max(top, first.max.y + layerHeight));
                StreamingMarkers.GatherOres.Begin();
                oreField.CollectNodes(oreBox, Nodes);
                StreamingMarkers.GatherOres.End();
            }
            else
            {
                Nodes.Clear();
            }

            using var marksMarker = StreamingMarkers.GatherMarks.Auto();
            bool exact = SameFootprint(tall, first) && (oreField == null || SameFootprint(oreBox, first));
            var gathered = new ColumnStamps(Parts.Count, Nodes.Count, oreField != null, exact)
            {
                oreBottom = oreBox.min.y,
                oreTop = oreBox.max.y,
            };
            for (int layer = 0; layer < Layers; layer++)
            {
                Bounds box = ChunkSampleGrid.ForChunk(grid, new Vector3Int(column.x, layer, column.y)).Bounds;
                gathered.layerBottom[layer] = box.min.y;
                gathered.layerTop[layer] = box.max.y;
            }

            for (int i = 0; i < Parts.Count; i++)
            {
                TreePart part = Parts[i];
                Bounds bounds = part.Bounds(TreeField.GatherMargin);
                // A tree's part is gathered only where its tree's bounds reach too; a stamp field's part has no tree round it.
                bool tree = i < treeParts;
                var stamp = new Stamp
                {
                    Part = part,
                    Bottom = bounds.min.y,
                    Top = bounds.max.y,
                    GateBottom = tree ? TreeBounds[i].min.y : float.MinValue,
                    GateTop = tree ? TreeBounds[i].max.y : float.MaxValue,
                };
                gathered.stamps[i] = stamp;
                gathered.Mark(stamp.Bottom, stamp.Top, stamp.GateBottom, stamp.GateTop, ref gathered.treesLow, ref gathered.treesHigh);
            }

            if (Nodes.Count > 0)
            {
                using (NativeArray<OreNodeData> data = oreField.ToJobNodes(Nodes, Allocator.Temp))
                {
                    for (int i = 0; i < Nodes.Count; i++)
                    {
                        var bounds = new Bounds(Nodes[i].Centre, Nodes[i].Reach * 2f);
                        var ore = new Ore { Node = data[i], Bottom = bounds.min.y, Top = bounds.max.y };
                        gathered.ores[i] = ore;
                        gathered.Mark(ore.Bottom, ore.Top, float.MinValue, float.MaxValue, ref gathered.oresLow, ref gathered.oresHigh);
                    }
                }
            }
            return gathered;
        }

        /// <summary>True when <see cref="TreesReach"/> answers for the layer: one of the column's <see cref="Layers"/>.</summary>
        public bool Knows(int layer)
        {
            return exact && layer >= 0 && layer < Layers;
        }

        /// <summary>True when a tree part or rock reaches the layer's chunk; only meaningful where it <see cref="Knows"/>.</summary>
        public bool TreesReach(int layer)
        {
            return IsSet(treesLow, treesHigh, layer);
        }

        /// <summary>True when an ore node reaches the layer's chunk; only meaningful where <see cref="Covers"/>.</summary>
        public bool OresReach(int layer)
        {
            return IsSet(oresLow, oresHigh, layer);
        }

        /// <summary>True when the layer's chunk can take its parts and nodes from the column: inside the gathered boxes.</summary>
        public bool Covers(int layer)
        {
            return exact && layer >= 0 && layer < Layers
                   && (!hasOres || (layerBottom[layer] >= oreBottom && layerTop[layer] <= oreTop));
        }

        /// <summary>
        /// Schedules picking the layer's parts and nodes into the two lists, emptied first (nothing else may use them until
        /// the job and anything reading them are done); read them in later jobs with <c>AsDeferredJobArray</c>.
        /// </summary>
        public JobHandle ScheduleSelect(int layer, NativeList<TreePart> parts, NativeList<OreNodeData> nodes)
        {
            parts.Clear();
            nodes.Clear();
            return new SelectJob
            {
                Stamps = stamps,
                Ores = ores,
                Bottom = layerBottom[layer],
                Top = layerTop[layer],
                Parts = parts,
                Nodes = nodes,
            }.Schedule();
        }

        /// <summary>
        /// Schedules filling the layer's chunk from its column's heights, as <see cref="ChunkGenerator.ScheduleFromColumns"/>
        /// does, with its share of the parts and nodes picked first in the same job: one job a chunk, nothing to hand back.
        /// The output stays the caller's.
        /// </summary>
        public JobHandle ScheduleFill(
            int layer, NativeArray<float> heights, NativeArray<float2> heightRange, ChunkSampleGrid grid, ChunkFillSettings settings,
            ChunkFillOutput output, JobHandle dependsOn)
        {
            return new PickAndFillJob
            {
                Stamps = stamps,
                Ores = ores,
                Bottom = layerBottom[layer],
                Top = layerTop[layer],
                Fill = new HeightfieldFillJob
                {
                    Heights = heights,
                    HeightRange = heightRange,
                    Grid = grid,
                    Settings = settings,
                    // Stand-ins until the job puts the chunk's own in: a job refuses unassigned arrays.
                    Ores = noNodes,
                    Trees = noParts,
                    Output = output,
                },
            }.Schedule(dependsOn);
        }

        /// <summary>Frees the arrays; every job reading them must be complete.</summary>
        public void Dispose()
        {
            stamps.Dispose();
            ores.Dispose();
            noParts.Dispose();
            noNodes.Dispose();
        }

        // The chunk's share of the column, in the column's order: what passes the chunk's own height test.
        private static void Select(
            in NativeArray<Stamp> stamps, in NativeArray<Ore> ores, float bottom, float top, ref NativeList<TreePart> parts, ref NativeList<OreNodeData> nodes)
        {
            for (int i = 0; i < stamps.Length; i++)
            {
                Stamp stamp = stamps[i];
                if (stamp.Bottom <= top && stamp.Top >= bottom && stamp.GateBottom <= top && stamp.GateTop >= bottom)
                {
                    parts.Add(stamp.Part);
                }
            }
            for (int i = 0; i < ores.Length; i++)
            {
                Ore ore = ores[i];
                if (ore.Bottom <= top && ore.Top >= bottom)
                {
                    nodes.Add(ore.Node);
                }
            }
        }

        // A box with the chunk's footprint, from bottom to top.
        private static Bounds ColumnBox(Bounds footprint, float bottom, float top)
        {
            return new Bounds(
                new Vector3(footprint.center.x, (bottom + top) * 0.5f, footprint.center.z),
                new Vector3(footprint.size.x, top - bottom, footprint.size.z));
        }

        private static bool SameFootprint(Bounds box, Bounds chunk)
        {
            return box.min.x == chunk.min.x && box.max.x == chunk.max.x && box.min.z == chunk.min.z && box.max.z == chunk.max.z;
        }

        // Marks every layer whose box both ranges reach, as Bounds.Intersects tests them (edges count).
        private void Mark(float bottom, float top, float gateBottom, float gateTop, ref ulong low, ref ulong high)
        {
            float from = Mathf.Max(bottom, gateBottom), to = Mathf.Min(top, gateTop);
            if (from > to)
            {
                return;
            }
            // The layers are evenly spaced: start a layer below the first that can reach, then test each exactly.
            float spacing = layerBottom[1] - layerBottom[0];
            int start = Mathf.Clamp(Mathf.FloorToInt((from - layerTop[0]) / spacing) - 1, 0, Layers - 1);
            for (int layer = start; layer < Layers && layerBottom[layer] <= to; layer++)
            {
                float layerMin = layerBottom[layer], layerMax = layerTop[layer];
                if (bottom <= layerMax && top >= layerMin && gateBottom <= layerMax && gateTop >= layerMin)
                {
                    if (layer < 64)
                    {
                        low |= 1UL << layer;
                    }
                    else
                    {
                        high |= 1UL << (layer - 64);
                    }
                }
            }
        }

        private static bool IsSet(ulong low, ulong high, int layer)
        {
            if (layer < 0 || layer >= Layers)
            {
                return false;
            }
            return (((layer < 64 ? low : high) >> (layer & 63)) & 1UL) != 0UL;
        }

        private struct Stamp
        {
            public TreePart Part;
            public float Bottom, Top, GateBottom, GateTop;
        }

        private struct Ore
        {
            public OreNodeData Node;
            public float Bottom, Top;
        }

        // A chunk's share of its column, into lists the caller reads.
        [BurstCompile]
        private struct SelectJob : IJob
        {
            [ReadOnly] public NativeArray<Stamp> Stamps;
            [ReadOnly] public NativeArray<Ore> Ores;
            public float Bottom, Top;
            public NativeList<TreePart> Parts;
            public NativeList<OreNodeData> Nodes;

            public void Execute()
            {
                Select(Stamps, Ores, Bottom, Top, ref Parts, ref Nodes);
            }
        }

        // A chunk's share of its column, then its fill (HeightfieldFillJob) with that share.
        [BurstCompile]
        private struct PickAndFillJob : IJob
        {
            [ReadOnly] public NativeArray<Stamp> Stamps;
            [ReadOnly] public NativeArray<Ore> Ores;
            public float Bottom, Top;
            public HeightfieldFillJob Fill;

            public void Execute()
            {
                var parts = new NativeList<TreePart>(Allocator.Temp);
                var nodes = new NativeList<OreNodeData>(Allocator.Temp);
                Select(Stamps, Ores, Bottom, Top, ref parts, ref nodes);
                HeightfieldFillJob fill = Fill;
                fill.Trees = parts.AsArray();
                fill.Ores = nodes.AsArray();
                fill.Execute();
            }
        }
    }
}
