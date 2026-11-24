using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Writes one settlement into the terrain (GL613, GL642), a piece at a time: its
    /// terraces and their banks in tiles (<see cref="GroundLeveling"/> with the site's own heights), then its lanes
    /// (each a <see cref="RoadWriter"/> over the terraced ground), then its buildings in whole voxels
    /// (<see cref="SquareVoxels"/>). Whoever owns it decides when (near the viewer, once a piece's chunks are loaded,
    /// a few milliseconds a frame), in any order that writes a piece after those it stands on (GL653: <see cref="IsReady"/>).
    /// Which pieces are written is all a save needs, so a loaded game doesn't write
    /// them again over the player's digging. Ground a planned road shapes is never touched: the road writes that.
    /// </summary>
    public sealed class SettlementWriter
    {
        /// <summary>The side of a tile of ground, in metres: small, so one piece is a few milliseconds.</summary>
        public const float TileSize = 3f;

        // Metres of air made over a terrace, for what stands on it.
        private const float Headroom = 3f;

        private enum Kind
        {
            Ground,
            Lane,
            Building,
            // GL715: a stretch of a town wall; written, like a building, over everything under it.
            Wall,
        }

        private struct Piece
        {
            public Kind Kind;
            public Rect Box;
            public float Low, High;
            public int Index, Part;
        }

        /// <summary>The materials a settlement is built of; any may be null (that part is then left out).</summary>
        public struct Materials
        {
            /// <summary>The banks between terraces: a rough stone retaining slope.</summary>
            public VoxelMaterial Bank;
            public VoxelMaterial Stone;
            /// <summary>The two kinds of boards: a building's walls are one and its trim the other, turn about.</summary>
            public VoxelMaterial Boards, Trim;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private readonly List<RoadWriter> lanes = new List<RoadWriter>();
        private readonly int[] buildingPiece;
        private readonly List<SettlementWalls.Stretch> stretches;
        // Which pieces are written; for each, how many pieces it stands on are still to write, and which stand on it.
        private readonly bool[] written;
        private readonly int[] blockers;
        private readonly List<int>[] dependents;
        // The first unwritten piece.
        private int next;
        private readonly List<LaneBridge> bridges = new List<LaneBridge>();
        private readonly Func<float, float, bool> wetAt;
        private readonly TreeRoom treeRoom;

        /// <summary>A bridge where a lane crosses water (GL645): its span, the deck's width and the lane it carries.</summary>
        public readonly struct LaneBridge
        {
            public LaneBridge(RouteBridges.Span span, float width, int lane)
            {
                Span = span;
                Width = width;
                Lane = lane;
            }

            public readonly RouteBridges.Span Span;
            public readonly float Width;
            public readonly int Lane;
        }

        /// <summary>The bridges its lanes cross water on; whoever owns the writer stands them once their ground has loaded.</summary>
        public IReadOnlyList<LaneBridge> Bridges => bridges;

        /// <summary>True where a water map holds water at (x, z); null for no map.</summary>
        public static Func<float, float, bool> WetOf(WaterMap water)
        {
            if (!water.IsCreated)
            {
                return null;
            }
            return (x, z) => water.TrySurfaceAt(new Unity.Mathematics.float2(x, z), out _, out _);
        }

        // A building is written in slices across its width, so no one piece is long.
        private const int BuildingSlices = 8;

        /// <param name="street">The numbers streets are cut with (width, surface, how they are evened out).</param>
        /// <param name="climb">The same for the climbs between rows.</param>
        /// <param name="water">The map's water (GL645): a lane across it is a bridge, and no ground is written where it stands.</param>
        public SettlementWriter(SettlementSite site, RoadClassDefinition street, RoadClassDefinition climb, WaterMap water = default, TreeRoom? trees = null)
        {
            Site = site;
            wetAt = WetOf(water);
            treeRoom = trees ?? TreeRoom.None;
            float ramp = site.Rules.Ramp;
            for (int t = 0; t < site.Terraces.Count; t++)
            {
                SettlementTerrace terrace = site.Terraces[t];
                Rect outer = Rect.MinMaxRect(terrace.Area.xMin - ramp, terrace.Area.yMin - ramp, terrace.Area.xMax + ramp, terrace.Area.yMax + ramp);
                int across = Mathf.Max(1, Mathf.CeilToInt(outer.width / TileSize)), deep = Mathf.Max(1, Mathf.CeilToInt(outer.height / TileSize));
                for (int j = 0; j < deep; j++)
                {
                    for (int i = 0; i < across; i++)
                    {
                        var box = new Rect(outer.xMin + i * outer.width / across, outer.yMin + j * outer.height / deep, outer.width / across, outer.height / deep);
                        float low = Mathf.Min(terrace.Height, terrace.NaturalLow) - ramp, high = Mathf.Max(terrace.Height, terrace.NaturalHigh) + ramp;
                        pieces.Add(new Piece { Kind = Kind.Ground, Box = box, Low = low, High = high + Headroom, Index = t });
                    }
                }
            }
            for (int l = 0; l < site.Lanes.Count; l++)
            {
                SettlementLane lane = site.Lanes[l];
                var line = new RoadLine(lane.Points, site.HeightAt, 1f);
                if (line.Length < RoadWriter.PieceLength)
                {
                    lanes.Add(null);
                    continue;
                }
                RoadClassDefinition definition = lane.Kind == SettlementLaneKind.Street ? street : climb;
                List<RouteBridges.Span> spans = RouteBridges.Find(line, water, true);
                foreach (RouteBridges.Span span in spans)
                {
                    bridges.Add(new LaneBridge(span, Mathf.Max(definition.Width, 1.6f), l));
                }
                var writer = new RoadWriter(new RoadBed(line, definition, spans, site.Settlement.Index * 31 + l) { WetAt = wetAt }) { Trees = treeRoom };
                lanes.Add(writer);
                for (int p = 0; p < writer.Pieces; p++)
                {
                    Vector2 middle = writer.Middle(p);
                    // On a planned road's own surface the road is the street (GL649: the lane runs on across its shoulder).
                    if (site.RoadSurfaceBeyond(middle.x, middle.y) <= 0f)
                    {
                        writer.Skip(p);
                        continue;
                    }
                    pieces.Add(new Piece { Kind = Kind.Lane, Index = l, Part = p, Box = writer.Footprint(p) });
                }
            }
            buildingPiece = new int[site.Buildings.Count];
            // GL710: each building's own footprint, its yard and its roof's height.
            for (int b = 0; b < site.Buildings.Count; b++)
            {
                SettlementBuilding building = site.Buildings[b];
                BuildingDesign design = SettlementStructures.DesignOf(building);
                float half = Mathf.Max(design.Width, design.Depth + 2f * design.YardDepth) * 0.5f + 0.75f;
                float high = SettlementStructures.RoofAt(design, building.Pitch, 0.25f, 0f) + 1.5f;
                for (int slice = 0; slice < BuildingSlices; slice++)
                {
                    // It stands once its last slice is written.
                    buildingPiece[b] = pieces.Count;
                    pieces.Add(new Piece
                    {
                        Kind = Kind.Building,
                        Box = new Rect(building.Centre.x - half, building.Centre.y - half, half * 2f, half * 2f),
                        Low = building.Pad - 1f,
                        High = building.Pad + high,
                        Index = b,
                        Part = slice,
                    });
                }
            }
            // GL715: the town wall, a stretch at a time.
            stretches = SettlementWalls.Stretches(site);
            for (int s = 0; s < stretches.Count; s++)
            {
                Rect box = SettlementWalls.Box(site, stretches[s]);
                float low = Mathf.Min(site.HeightAt(box.xMin, box.yMin), site.HeightAt(box.xMax, box.yMax));
                float high = Mathf.Max(site.HeightAt(box.xMin, box.yMin), site.HeightAt(box.xMax, box.yMax));
                pieces.Add(new Piece
                {
                    Kind = Kind.Wall, Box = box, Low = low - 2f,
                    High = high + site.Rules.WallHeight + site.Rules.WallTowerRise + 1f, Index = s,
                });
            }
            written = new bool[pieces.Count];
            blockers = new int[pieces.Count];
            dependents = new List<int>[pieces.Count];
            FindOrder();
        }

        public SettlementSite Site { get; }

        /// <summary>Pieces in all: tiles of ground, metres of lane, buildings.</summary>
        public int Pieces => pieces.Count;

        /// <summary>Pieces written so far.</summary>
        public int Written { get; private set; }

        public bool Done => Written >= pieces.Count;

        /// <summary>What the first unwritten piece is, for timing: 0 a tile of ground, 1 a metre of lane, 2 a part of a building; -1 when done.</summary>
        public int NextKind => Done ? -1 : KindOf(next);

        /// <summary>What a piece is: 0 a tile of ground, 1 a metre of lane, 2 a part of a building or a stretch of town wall.</summary>
        public int KindOf(int piece)
        {
            return Mathf.Min((int)pieces[piece].Kind, (int)Kind.Building);
        }

        public bool IsWritten(int piece)
        {
            return written[piece];
        }

        /// <summary>
        /// True when a piece can be written now (GL653): it isn't yet, and every piece it stands on is: the terraces
        /// under a lane, the terraces and lanes under a building, an earlier terrace's tiles where two overlap. Pieces
        /// are written in any order that keeps to this, so a settlement larger than the loaded area still gets built
        /// round the viewer.
        /// </summary>
        public bool IsReady(int piece)
        {
            return !written[piece] && blockers[piece] == 0;
        }

        /// <summary>The ground a piece may change (x, z): what a hold keeps from being shown until it is written.</summary>
        public Rect Footprint(int piece)
        {
            return pieces[piece].Box;
        }

        /// <summary>Adds the pieces that can be written now (<see cref="IsReady"/>) whose ground is within <paramref name="range"/> metres of a point.</summary>
        public void Ready(Vector2 near, float range, List<int> into)
        {
            for (int piece = next; piece < pieces.Count; piece++)
            {
                if (IsReady(piece) && SettlementSite.Beyond(pieces[piece].Box, near) <= range)
                {
                    into.Add(piece);
                }
            }
        }

        /// <summary>True once a building stands in the terrain.</summary>
        public bool IsStanding(int building)
        {
            return written[buildingPiece[building]];
        }

        /// <summary>Marks the first pieces as written (an older save's count); a count that doesn't fit this settlement is ignored.</summary>
        public void Restore(int count)
        {
            if (count >= 0 && count <= pieces.Count)
            {
                for (int piece = 0; piece < count; piece++)
                {
                    Mark(piece);
                }
            }
        }

        /// <summary>Marks the pieces a save says are written (<see cref="Save"/>); a settlement of another number of pieces is ignored.</summary>
        public void Restore(string saved)
        {
            var flags = new bool[pieces.Count];
            PieceFlags.Restore(saved, flags);
            for (int piece = 0; piece < flags.Length; piece++)
            {
                if (flags[piece])
                {
                    Mark(piece);
                }
            }
        }

        /// <summary>Which pieces are written, for the save (<see cref="PieceFlags"/>).</summary>
        public string Save()
        {
            return PieceFlags.Save(written);
        }

        private void Mark(int piece)
        {
            if (written[piece])
            {
                return;
            }
            written[piece] = true;
            Written++;
            if (dependents[piece] != null)
            {
                foreach (int dependent in dependents[piece])
                {
                    blockers[dependent]--;
                }
            }
            while (next < pieces.Count && written[next])
            {
                next++;
            }
        }

        // Which pieces each must wait for: those it is written over. Found through a grid of the pieces' boxes.
        private void FindOrder()
        {
            const float Cell = 8f, Touch = 0.5f;
            var grid = new Dictionary<Vector2Int, List<int>>();
            var seen = new int[pieces.Count];
            for (int j = 0; j < pieces.Count; j++)
            {
                Piece piece = pieces[j];
                Rect box = Rect.MinMaxRect(piece.Box.xMin - Touch, piece.Box.yMin - Touch, piece.Box.xMax + Touch, piece.Box.yMax + Touch);
                int x0 = Mathf.FloorToInt(box.xMin / Cell), x1 = Mathf.FloorToInt(box.xMax / Cell);
                int z0 = Mathf.FloorToInt(box.yMin / Cell), z1 = Mathf.FloorToInt(box.yMax / Cell);
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var cell = new Vector2Int(x, z);
                        if (!grid.TryGetValue(cell, out List<int> here))
                        {
                            if (piece.Kind >= Kind.Building)
                            {
                                continue;
                            }
                            grid[cell] = here = new List<int>();
                        }
                        foreach (int i in here)
                        {
                            if (seen[i] == j + 1 || !WaitsFor(piece, pieces[i]) || !box.Overlaps(pieces[i].Box))
                            {
                                continue;
                            }
                            seen[i] = j + 1;
                            Wait(j, i);
                        }
                        // Buildings stand on everything and nothing on them: they needn't be found.
                        if (piece.Kind < Kind.Building)
                        {
                            here.Add(j);
                        }
                    }
                }
                // A building goes up a slice at a time.
                if (piece.Kind == Kind.Building && piece.Part > 0)
                {
                    Wait(j, j - 1);
                }
            }
        }

        // Whether a piece is written over an earlier one where their ground overlaps: a terrace's tile over another
        // terrace's (its own tiles lie side by side), a lane over tiles, a building over tiles and lanes. Lanes cross
        // in any order, as the roads do.
        private static bool WaitsFor(Piece later, Piece earlier)
        {
            switch (later.Kind)
            {
                case Kind.Ground:
                    return earlier.Kind == Kind.Ground && earlier.Index != later.Index;
                case Kind.Lane:
                    return earlier.Kind == Kind.Ground;
                default:
                    return true;
            }
        }

        private void Wait(int piece, int earlier)
        {
            blockers[piece]++;
            (dependents[earlier] ??= new List<int>()).Add(piece);
        }

        /// <summary>True when every chunk the first unwritten piece may change is loaded.</summary>
        public bool NextLoaded(World world, int heightInChunks)
        {
            return !Done && Loaded(world, heightInChunks, next);
        }

        /// <summary>True when every chunk a piece may change is loaded.</summary>
        /// <param name="missing">Given the chunks still to load, when wanted (GL770: the streamer fetches them first).</param>
        public bool Loaded(World world, int heightInChunks, int index, List<Vector3Int> missing = null)
        {
            Piece piece = pieces[index];
            if (piece.Kind == Kind.Lane)
            {
                return lanes[piece.Index].Loaded(world, heightInChunks, piece.Part, missing);
            }
            WorldGrid grid = world.Grid;
            // GL652: a tile of ground cuts the trees on it whole, so it waits for every chunk such a tree reaches.
            float spread = piece.Kind == Kind.Ground ? Mathf.Max(1f, treeRoom.Spread) : 1f;
            float top = piece.Kind == Kind.Ground ? Mathf.Max(piece.High, piece.High - Headroom + treeRoom.Height) : piece.High;
            Vector3Int min = grid.WorldToChunk(new Vector3(piece.Box.xMin - spread, Mathf.Max(0f, piece.Low), piece.Box.yMin - spread));
            Vector3Int max = grid.WorldToChunk(new Vector3(piece.Box.xMax + spread, top, piece.Box.yMax + spread));
            return world.AreLoaded(min, max, heightInChunks, missing);
        }

        /// <summary>Writes the first unwritten piece into the world.</summary>
        /// <param name="natural">The ground as generated at (x, z).</param>
        public void WriteNext(World world, MaterialRegistry registry, Materials materials, Func<float, float, float> natural, float iso)
        {
            if (!Done)
            {
                Write(world, registry, materials, natural, iso, next);
            }
        }

        /// <summary>Writes a piece into the world and marks it written; one that is <see cref="IsReady"/>.</summary>
        /// <param name="natural">The ground as generated at (x, z).</param>
        public void Write(World world, MaterialRegistry registry, Materials materials, Func<float, float, float> natural, float iso, int index)
        {
            SlicedWork.RunToEnd(WriteSteps(world, registry, materials, natural, iso, index));
        }

        /// <summary>
        /// <see cref="Write"/> in small steps for <see cref="SlicedWork"/> (GL771). The piece counts as written, and what stands
        /// on it as ready, only once it is done.
        /// </summary>
        public IEnumerator WriteSteps(World world, MaterialRegistry registry, Materials materials, Func<float, float, float> natural, float iso, int index)
        {
            if (written[index])
            {
                yield break;
            }
            Piece piece = pieces[index];
            SettlementSite site = Site;
            switch (piece.Kind)
            {
                case Kind.Ground:
                {
                    SettlementTerrace terrace = site.Terraces[piece.Index];
                    float ramp = site.Rules.Ramp;
                    yield return GroundLeveling.LevelSteps(world, registry, new GroundLeveling.Area
                    {
                        Centre = piece.Box.center,
                        Half = piece.Box.size * 0.5f,
                        Natural = natural,
                        Headroom = Headroom,
                        KeepTop = true,
                        Surface = (x, z, ground) => site.HeightAt(x, z, ground),
                        // A planned road's ground is the road's; a tile only works on its own terrace and bank.
                        Inside = (x, z) => site.RoadBeyond(x, z) > 0f && (wetAt == null || !wetAt(x, z)) && SettlementSite.Beyond(terrace.Area, new Vector2(x, z)) < ramp,
                        // The steep part of a bank is faced with stone.
                        Paving = materials.Bank,
                        Paved = (x, z) => IsBank(site, x, z, natural(x, z)),
                    }, iso);
                    // GL734: the quarry pit is stone, floor and faces.
                    if (site.Quarry != null && site.Quarry.Terrace == piece.Index && materials.Stone != null)
                    {
                        StoneOver(world, registry, piece, site.Quarry.Floor - QuarryYard.StoneDepth, materials.Stone.Id);
                    }
                    break;
                }
                case Kind.Lane:
                    yield return lanes[piece.Index].WriteSteps(world, registry, site.HeightAt, iso, piece.Part);
                    break;
                case Kind.Wall:
                    if (materials.Stone != null)
                    {
                        yield return SquareVoxels.BuildSteps(world, SettlementWalls.Blocks(site, stretches[piece.Index], world.Grid.VoxelSize, materials.Stone.Id, wetAt), iso);
                    }
                    break;
                default:
                {
                    if (materials.Stone == null || materials.Boards == null || materials.Trim == null)
                    {
                        break;
                    }
                    bool turnAbout = piece.Index % 2 == 1;
                    yield return SquareVoxels.BuildSteps(world, SettlementStructures.Blocks(site.Buildings[piece.Index], world.Grid.VoxelSize, materials.Stone.Id,
                        (turnAbout ? materials.Trim : materials.Boards).Id, (turnAbout ? materials.Boards : materials.Trim).Id, piece.Part, BuildingSlices), iso);
                    // GL711: the roof's top drawn as a slant over its square steps.
                    yield return SquareVoxels.ShapeSteps(world, SettlementStructures.RoofSkin(site.Buildings[piece.Index], world.Grid.VoxelSize, piece.Part, BuildingSlices),
                        (turnAbout ? materials.Trim : materials.Boards).Id);
                    break;
                }
            }
            Mark(index);
        }

        // Every sample of a tile with any density, from a height up to the tile's top, becomes stone (GL734, as GL557's tutorial
        // map); not a tree's wood or leaves (GL849: a tree on the bank leaning over the pit was drawn as stone).
        private static void StoneOver(World world, MaterialRegistry registry, Piece piece, float bottom, byte stone)
        {
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToNearestSample(new Vector3(piece.Box.xMin, bottom, piece.Box.yMin));
            Vector3Int max = grid.WorldToNearestSample(new Vector3(piece.Box.xMax, piece.High, piece.Box.yMax));
            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int y = min.y; y <= max.y; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) && chunk.GetDensity(local) > 0
                            && !(registry != null && registry.Get(chunk.GetMaterial(local)) is VoxelMaterial was && was.Kind == MaterialKind.Plant))
                        {
                            world.SetMaterial(sample, stone);
                        }
                    }
                }
            }
        }

        /// <summary>True where the settlement's ground is a steep bank between a terrace and the land: over a metre off the land, and off the level.</summary>
        public static bool IsBank(SettlementSite site, float x, float z, float ground)
        {
            var point = new Vector2(x, z);
            foreach (SettlementTerrace terrace in site.Terraces)
            {
                if (terrace.Area.Contains(point))
                {
                    return false;
                }
            }
            return Mathf.Abs(site.HeightAt(x, z, ground) - ground) > 1f;
        }
    }
}
