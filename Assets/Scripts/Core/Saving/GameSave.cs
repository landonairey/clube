using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A saved game as plain data (GL205, first pass of S1-S4): what <see cref="GameSaveFile"/> writes to the
    /// drive and reads back. Items, materials and recipes are kept by id or name, never by reference, so a
    /// save outlives the session that made it. The world keeps only its seed and the chunks the player
    /// changed (S2); everything else regenerates from the seed.
    /// </summary>
    public sealed class GameSave
    {
        public SaveHeader Header = new SaveHeader();

        /// <summary>The player; null when saved before the player was switched on (the lab's fly camera), which then starts afresh.</summary>
        public PlayerSave Player;

        /// <summary>In-game days on the clock.</summary>
        public double Days;

        /// <summary>The edited chunks (S2).</summary>
        public List<ChunkSave> Chunks = new List<ChunkSave>();

        /// <summary>What the player built (furnaces, campfires...), with what their stations hold.</summary>
        public List<PlacedSave> Placed = new List<PlacedSave>();

        /// <summary>Dropped clay still drying.</summary>
        public List<DryingSave> Drying = new List<DryingSave>();
    }

    /// <summary>
    /// The start of a save file: what the main menu shows about a save without reading the rest (S4), and
    /// what a game needs before its world starts (the mode and seed).
    /// </summary>
    public sealed class SaveHeader
    {
        /// <summary>
        /// Format version written by this build (S1). Raise it when the format changes, and upgrade older ones on read.
        /// 1: the first. 2: the save's and the world's names (GL206, GL207). 3: the quests done (GL216), at the body's end.
        /// </summary>
        public const int CurrentVersion = 3;

        public int Version = CurrentVersion;

        /// <summary>What the player called the save (GL207); empty in a version 1 save.</summary>
        public string SaveName = "";

        /// <summary>What the player called the world on the new-game page (GL206); empty in a version 1 save.</summary>
        public string WorldName = "";

        /// <summary>The game mode it was played in, e.g. "World".</summary>
        public string Mode = "";

        public int Seed;

        /// <summary>When it was saved, in UTC.</summary>
        public DateTime SavedAt;

        /// <summary>The build that saved it (its version text).</summary>
        public string Build = "";
    }

    /// <summary>The player: where they stand and look, and what they carry and know.</summary>
    public sealed class PlayerSave
    {
        public Vector3 Position;

        /// <summary>Turn about the vertical, in degrees.</summary>
        public float Yaw;

        /// <summary>The head's look up or down, in degrees.</summary>
        public float Pitch;

        /// <summary>Every inventory slot in order, empty ones included.</summary>
        public List<StackSave> Slots = new List<StackSave>();

        public int HotbarSelected;

        public float Coins;

        /// <summary>Experience per skill, by <see cref="SkillType"/> name.</summary>
        public List<SkillSave> Skills = new List<SkillSave>();

        /// <summary>The mining journal's ores.</summary>
        public List<OreEntrySave> Ores = new List<OreEntrySave>();

        /// <summary>Recipes finished at least once, by asset name.</summary>
        public List<string> MadeRecipes = new List<string>();

        /// <summary>Land parcels owned.</summary>
        public List<Vector2Int> Parcels = new List<Vector2Int>();

        /// <summary>Quests done, by id (GL216; save version 3).</summary>
        public List<string> Quests = new List<string>();
    }

    public struct SkillSave
    {
        public string Skill;
        public float Experience;
    }

    public struct OreEntrySave
    {
        public byte Material;
        public float FirstDepth;
        public float LowestGrade;
        public float HighestGrade;
        public int Samples;
    }

    /// <summary>One inventory slot: an item by id, how many, and what it's made of (by material id).</summary>
    public struct StackSave
    {
        /// <summary>The item's id; empty for an empty slot.</summary>
        public string Item;
        public int Count;
        public float Part;
        public int Wear;
        public float Quality;

        /// <summary>Its contents, or null for none.</summary>
        public List<ContentSave> Contents;

        public bool IsEmpty => string.IsNullOrEmpty(Item);
    }

    public struct ContentSave
    {
        public byte Material;
        public float Fraction;
    }

    /// <summary>A placed object: the item it was placed from, where it stands, and what it holds.</summary>
    public sealed class PlacedSave
    {
        public string Item = "";
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.identity;
        public float Quality = 1f;
        public List<ContentSave> Contents;

        /// <summary>A station's slots; empty lists for an object that isn't one.</summary>
        public List<StackSave> Input = new List<StackSave>();
        public List<StackSave> Output = new List<StackSave>();
        public List<StackSave> Fuel = new List<StackSave>();

        /// <summary>The terrain samples a structure (campfire, furnace) stands in; empty for none.</summary>
        public List<Vector3Int> Stamped = new List<Vector3Int>();
    }

    public struct DryingSave
    {
        public Vector3Int Sample;
        public byte Material;
        public double ReadyAt;
    }

    /// <summary>
    /// One edited chunk: every sample's density, material and carve flags. Densities are bytes when the
    /// world stores bytes (M12), so they come back exactly; floats otherwise.
    /// </summary>
    public sealed class ChunkSave
    {
        public Vector3Int Coord;
        public Vector3Int SampleCount;

        /// <summary>Densities as stored bytes (<see cref="ByteVoxelStorage.Quantize"/>), or null when <see cref="FloatDensities"/> holds them.</summary>
        public byte[] ByteDensities;

        public float[] FloatDensities;

        /// <summary>A material id per sample, or null when every sample holds <see cref="UniformMaterial"/>.</summary>
        public byte[] Materials;

        public byte UniformMaterial;

        /// <summary>Carve flags per sample, or null when nothing in or next to the chunk is carved.</summary>
        public byte[] Carves;
    }
}
