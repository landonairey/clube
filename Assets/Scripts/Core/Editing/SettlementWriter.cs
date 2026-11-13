using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Writes one settlement into the terrain (GL613, GL642), a piece at a time and always in the same order: its
    /// terraces and their banks in tiles (<see cref="GroundLeveling"/> with the site's own heights), then its lanes
    /// (each a <see cref="RoadWriter"/> over the terraced ground), then its buildings in whole voxels
    /// (<see cref="SquareVoxels"/>). Whoever owns it decides when (near the viewer, once a piece's chunks are loaded,
    /// a few milliseconds a frame). How many pieces are written is all a save needs, so a loaded game doesn't write
    /// them again over the player's digging. Ground a planned road shapes is never touched: the road writes that.
    /// </summary>
    public sealed class SettlementWriter
    {
        /// <summary>The side of a tile of ground, in metres: small, so one piece is a few milliseconds.</summary>
        public const float TileSize = 6f;

        // Metres of air made over a terrace, for what stands on it.
        private const float Headroom = 3f;

        private enum Kind
        {
            Ground,
            Lane,
            Building,
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

        // A building is written in slices across its width, so no one piece is long.
        private const int BuildingSlices = 4;

        /// <param name="street">The numbers streets are cut with (width, surface, how they are evened out).</param>
        /// <param name="climb">The same for the climbs between rows.</param>
        public SettlementWriter(SettlementSite site, RoadClassDefinition street, RoadClassDefinition climb)
        {
            Site = site;
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
                var writer = new RoadWriter(new RoadBed(line, lane.Kind == SettlementLaneKind.Street ? street : climb, null, site.Settlement.Index * 31 + l));
                lanes.Add(writer);
                for (int p = 0; p < writer.Pieces; p++)
                {
                    Vector2 middle = writer.Middle(p);
                    // On a planned road's own ground the road is the street.
                    if (site.RoadBeyond(middle.x, middle.y) <= 0f)
                    {
                        writer.Skip(p);
                        continue;
                    }
                    pieces.Add(new Piece { Kind = Kind.Lane, Index = l, Part = p });
                }
            }
            buildingPiece = new int[site.Buildings.Count];
            float half = Mathf.Max(TownLayout.ShopWidth, TownLayout.ShopDepth) * 0.5f + 0.5f;
            for (int b = 0; b < site.Buildings.Count; b++)
            {
                SettlementBuilding building = site.Buildings[b];
                for (int slice = 0; slice < BuildingSlices; slice++)
                {
                    // It stands once its last slice is written.
                    buildingPiece[b] = pieces.Count;
                    pieces.Add(new Piece
                    {
                        Kind = Kind.Building,
                        Box = new Rect(building.Centre.x - half, building.Centre.y - half, half * 2f, half * 2f),
                        Low = building.Pad - 1f,
                        High = building.Pad + TownLayout.ShopHeight + 3f,
                        Index = b,
                        Part = slice,
                    });
                }
            }
        }

        public SettlementSite Site { get; }

        /// <summary>Pieces in all: tiles of ground, metres of lane, buildings.</summary>
        public int Pieces => pieces.Count;

        /// <summary>Pieces written so far; they are written in order.</summary>
        public int Written { get; private set; }

        public bool Done => Written >= pieces.Count;

        /// <summary>What the next piece is, for timing: 0 a tile of ground, 1 a metre of lane, 2 a part of a building; -1 when done.</summary>
        public int NextKind => Done ? -1 : (int)pieces[Written].Kind;

        /// <summary>True once a building stands in the terrain.</summary>
        public bool IsStanding(int building)
        {
            return Written > buildingPiece[building];
        }

        /// <summary>Marks the first pieces as written (from a save); a count that doesn't fit this settlement is ignored.</summary>
        public void Restore(int written)
        {
            if (written >= 0 && written <= pieces.Count)
            {
                Written = Mathf.Max(Written, written);
            }
        }

        /// <summary>True when every chunk the next piece may change is loaded.</summary>
        public bool NextLoaded(World world, int heightInChunks)
        {
            if (Done)
            {
                return false;
            }
            Piece piece = pieces[Written];
            if (piece.Kind == Kind.Lane)
            {
                return lanes[piece.Index].Loaded(world, heightInChunks, piece.Part);
            }
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToChunk(new Vector3(piece.Box.xMin - 1f, Mathf.Max(0f, piece.Low), piece.Box.yMin - 1f));
            Vector3Int max = grid.WorldToChunk(new Vector3(piece.Box.xMax + 1f, piece.High, piece.Box.yMax + 1f));
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(heightInChunks - 1, max.y); y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!world.IsLoaded(new Vector3Int(x, y, z)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>Writes the next piece into the world.</summary>
        /// <param name="natural">The ground as generated at (x, z).</param>
        public void WriteNext(World world, MaterialRegistry registry, Materials materials, Func<float, float, float> natural, float iso)
        {
            if (Done)
            {
                return;
            }
            Piece piece = pieces[Written++];
            SettlementSite site = Site;
            switch (piece.Kind)
            {
                case Kind.Ground:
                {
                    SettlementTerrace terrace = site.Terraces[piece.Index];
                    float ramp = site.Rules.Ramp;
                    GroundLeveling.Level(world, registry, new GroundLeveling.Area
                    {
                        Centre = piece.Box.center,
                        Half = piece.Box.size * 0.5f,
                        Natural = natural,
                        Headroom = Headroom,
                        KeepTop = true,
                        Surface = (x, z, ground) => site.HeightAt(x, z, ground),
                        // A planned road's ground is the road's; a tile only works on its own terrace and bank.
                        Inside = (x, z) => site.RoadBeyond(x, z) > 0f && SettlementSite.Beyond(terrace.Area, new Vector2(x, z)) < ramp,
                        // The steep part of a bank is faced with stone.
                        Paving = materials.Bank,
                        Paved = (x, z) => IsBank(site, x, z, natural(x, z)),
                    }, iso);
                    break;
                }
                case Kind.Lane:
                    lanes[piece.Index].Write(world, registry, site.HeightAt, iso, piece.Part);
                    break;
                default:
                {
                    if (materials.Stone == null || materials.Boards == null || materials.Trim == null)
                    {
                        break;
                    }
                    bool turnAbout = piece.Index % 2 == 1;
                    SquareVoxels.Build(world, SettlementStructures.Blocks(site.Buildings[piece.Index], world.Grid.VoxelSize, materials.Stone.Id,
                        (turnAbout ? materials.Trim : materials.Boards).Id, (turnAbout ? materials.Boards : materials.Trim).Id, piece.Part, BuildingSlices), iso);
                    break;
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
