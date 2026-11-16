using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// What the steps of world planning share (GL595, GL596): the survey's cells as a grid, the resources within reach of
    /// each, the rules scaled to the map, a seeded hash, and a cheapest-path search over the cells.
    /// </summary>
    internal sealed class PlanContext
    {
        private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] StepZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly float[] cost;
        private readonly int[] heap;
        private readonly int[] heapAt;
        private int heapCount;

        public PlanContext(ResourceSurvey survey, int seed, WorldPlanRules rules)
        {
            Survey = survey;
            Seed = seed;
            Rules = rules;
            Size = survey.Size;
            CellSize = survey.CellSize;
            Cells = new SurveyCell[Size * Size];
            for (int z = 0; z < Size; z++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Cells[x + z * Size] = survey.Cell(x, z);
                }
            }
            Resources = SurveyResource.For(survey);
            Reach = new ResourceReach(survey, Resources);
            Scale = Mathf.Clamp(survey.Area.width / Mathf.Max(1f, rules.ReferenceMapSize), 0.25f, 2f);
            NearCells = Mathf.Max(2, Mathf.RoundToInt(rules.NearbyRadius * Scale / CellSize));
            AreaCells = Mathf.Max(NearCells + 1, Mathf.RoundToInt(rules.AreaRadius * Scale / CellSize));
            for (int r = 0; r < Resources.Count; r++)
            {
                if (Resources[r].IsType && Reach.Totals[r] > 0f)
                {
                    TypeResources.Add(r);
                }
            }
            cost = new float[Size * Size];
            heap = new int[Size * Size];
            heapAt = new int[Size * Size];
            Parent = new int[Size * Size];
            Land = Lands(out int main, out int[] counts);
            MainLandId = main;
            LandCells = counts;
            MainLand = new bool[Size * Size];
            for (int i = 0; i < MainLand.Length; i++)
            {
                MainLand[i] = main != 0 && Land[i] == main;
            }
        }

        public ResourceSurvey Survey { get; }
        public int Seed { get; }
        public WorldPlanRules Rules { get; }
        public int Size { get; }
        public float CellSize { get; }
        public SurveyCell[] Cells { get; }
        public List<SurveyResource> Resources { get; }
        public ResourceReach Reach { get; }
        /// <summary>The map's size over the rules' reference size: distances in the rules are multiplied by it.</summary>
        public float Scale { get; }
        /// <summary>"Nearby" and a city's area, in cells.</summary>
        public int NearCells { get; }
        public int AreaCells { get; }
        /// <summary>Indices of the resource types this map has any of.</summary>
        public List<int> TypeResources { get; } = new List<int>();
        /// <summary>True for the cells of the largest piece of land: everything there can be reached on foot.</summary>
        public bool[] MainLand { get; }
        /// <summary>The piece of land each cell belongs to (GL643), from 1; 0 for the sea. Cells of one piece can be walked between.</summary>
        public int[] Land { get; }
        /// <summary>The largest piece of land's number in <see cref="Land"/>; 0 on a map with no land.</summary>
        public int MainLandId { get; }
        /// <summary>How many cells each piece of land has, by its number (index 0 unused).</summary>
        public int[] LandCells { get; }
        /// <summary>After <see cref="Search"/>: the cell each reached cell was come to from (the start points to itself).</summary>
        public int[] Parent { get; }

        public bool IsSea(int cell)
        {
            return Cells[cell].Sea >= 0.5f;
        }

        public Vector2 Centre(int cell)
        {
            return Survey.CellCentre(cell % Size, cell / Size);
        }

        /// <summary>How far a settlement of a type reaches for resources, in cells.</summary>
        public int ReachOf(SettlementType type)
        {
            return type.Site == SiteRule.AllResources ? AreaCells : NearCells;
        }

        /// <summary>A number from 0 to 1 that depends only on the seed and three integers.</summary>
        public float Hash01(int a, int b, int c)
        {
            uint h = (uint)Seed * 0x9E3779B1u ^ (uint)a * 0x85EBCA77u ^ (uint)b * 0xC2B2AE3Du ^ (uint)c * 0x27D4EB2Fu;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>The nearest cell to a centre that matches, within a reach in cells (a square), or -1.</summary>
        public int NearestCell(Vector2Int centre, int reach, Func<int, bool> match)
        {
            int best = -1;
            int bestDistance = int.MaxValue;
            for (int z = Mathf.Max(0, centre.y - reach); z <= Mathf.Min(Size - 1, centre.y + reach); z++)
            {
                for (int x = Mathf.Max(0, centre.x - reach); x <= Mathf.Min(Size - 1, centre.x + reach); x++)
                {
                    int distance = (x - centre.x) * (x - centre.x) + (z - centre.y) * (z - centre.y);
                    if (distance < bestDistance && match(x + z * Size))
                    {
                        bestDistance = distance;
                        best = x + z * Size;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// The cheapest way from a cell to the first cell that is a goal (Dijkstra over the eight neighbours).
        /// </summary>
        /// <param name="stepCost">Cost of a step (from, to, metres); negative when it can't be taken.</param>
        /// <param name="isGoal">Null to reach every cell there is.</param>
        /// <returns>The goal reached, or -1; <see cref="Parent"/> then leads back from any reached cell.</returns>
        public int Search(int start, Func<int, int, float, float> stepCost, Func<int, bool> isGoal)
        {
            for (int i = 0; i < cost.Length; i++)
            {
                cost[i] = float.MaxValue;
                Parent[i] = -1;
                heapAt[i] = -1;
            }
            heapCount = 0;
            cost[start] = 0f;
            Parent[start] = start;
            Push(start);
            while (heapCount > 0)
            {
                int cell = Pop();
                if (isGoal != null && cell != start && isGoal(cell))
                {
                    return cell;
                }
                int x = cell % Size, z = cell / Size;
                for (int n = 0; n < 8; n++)
                {
                    int nx = x + StepX[n], nz = z + StepZ[n];
                    if (nx < 0 || nz < 0 || nx >= Size || nz >= Size)
                    {
                        continue;
                    }
                    int next = nx + nz * Size;
                    float step = stepCost(cell, next, n < 4 ? CellSize : CellSize * 1.41421356f);
                    if (step < 0f || cost[cell] + step >= cost[next])
                    {
                        continue;
                    }
                    cost[next] = cost[cell] + step;
                    Parent[next] = cell;
                    Push(next);
                }
            }
            return -1;
        }

        /// <summary>True when the last <see cref="Search"/> reached a cell.</summary>
        public bool Reached(int cell)
        {
            return Parent[cell] >= 0;
        }

        /// <summary>The cells from the last search's start to a cell it reached.</summary>
        public List<int> PathTo(int cell)
        {
            var path = new List<int>();
            while (true)
            {
                path.Add(cell);
                if (Parent[cell] == cell)
                {
                    break;
                }
                cell = Parent[cell];
            }
            path.Reverse();
            return path;
        }

        private int[] Lands(out int best, out int[] counts)
        {
            var group = new int[Size * Size];
            var sizes = new List<int> { 0 };
            int bestCount = 0, groups = 0;
            best = 0;
            var open = new Stack<int>();
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != 0 || IsSea(i))
                {
                    continue;
                }
                groups++;
                int count = 0;
                group[i] = groups;
                open.Push(i);
                while (open.Count > 0)
                {
                    int cell = open.Pop();
                    count++;
                    int x = cell % Size, z = cell / Size;
                    for (int n = 0; n < 8; n++)
                    {
                        int nx = x + StepX[n], nz = z + StepZ[n];
                        if (nx < 0 || nz < 0 || nx >= Size || nz >= Size || group[nx + nz * Size] != 0 || IsSea(nx + nz * Size))
                        {
                            continue;
                        }
                        group[nx + nz * Size] = groups;
                        open.Push(nx + nz * Size);
                    }
                }
                sizes.Add(count);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = groups;
                }
            }
            counts = sizes.ToArray();
            return group;
        }

        // A binary heap of cells by cost, with each cell's place kept so a cheaper cost moves it up.
        private void Push(int cell)
        {
            int at = heapAt[cell];
            if (at < 0)
            {
                at = heapCount++;
                heap[at] = cell;
                heapAt[cell] = at;
            }
            while (at > 0)
            {
                int up = (at - 1) / 2;
                if (cost[heap[up]] <= cost[heap[at]])
                {
                    break;
                }
                Swap(at, up);
                at = up;
            }
        }

        private int Pop()
        {
            int top = heap[0];
            heapCount--;
            heapAt[top] = -1;
            if (heapCount > 0)
            {
                heap[0] = heap[heapCount];
                heapAt[heap[0]] = 0;
                int at = 0;
                while (true)
                {
                    int left = at * 2 + 1, right = left + 1, least = at;
                    if (left < heapCount && cost[heap[left]] < cost[heap[least]])
                    {
                        least = left;
                    }
                    if (right < heapCount && cost[heap[right]] < cost[heap[least]])
                    {
                        least = right;
                    }
                    if (least == at)
                    {
                        break;
                    }
                    Swap(at, least);
                    at = least;
                }
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            int cell = heap[a];
            heap[a] = heap[b];
            heap[b] = cell;
            heapAt[heap[a]] = a;
            heapAt[heap[b]] = b;
        }
    }
}
