using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Names for settlements (GL470, first pass of ST8): a first part and an ending put together ("Ash" + "ford" = "Ashford"),
    /// picked by the world's seed and the settlement's parcel, so a settlement keeps its name in every game of that world and
    /// two settlements of one world get different ones. Plain code; signs, maps and journals read it.
    /// </summary>
    public static class SettlementNames
    {
        private static readonly string[] Starts =
        {
            "Ash", "Birch", "Bram", "Brook", "Cold", "Copper", "Crow", "Dun", "Elder", "Fern", "Flint", "Fox", "Glen", "Gold",
            "Grey", "Hart", "Haw", "Holly", "Iron", "Kings", "Lark", "Lin", "Marsh", "Mill", "Moss", "Oak", "Pine", "Raven",
            "Red", "Rook", "Rush", "Salt", "Silver", "Stone", "Thorn", "Tin", "Wald", "West", "Whit", "Wolf", "Wren", "Yew",
        };

        private static readonly string[] Ends =
        {
            "barrow", "bridge", "brook", "burn", "by", "combe", "croft", "dale", "den", "field", "ford", "gate", "hall", "ham",
            "haven", "hill", "holm", "hurst", "ley", "mere", "moor", "mouth", "ridge", "stead", "stow", "thorpe", "ton", "wick",
            "wood", "worth",
        };

        /// <summary>The name of the settlement on a parcel of a world with this seed.</summary>
        public static string Generate(int seed, Vector2Int parcel)
        {
            uint hash = Hash((uint)seed, (uint)parcel.x, (uint)parcel.y);
            string start = Starts[hash % (uint)Starts.Length];
            string end = Ends[(hash / (uint)Starts.Length) % (uint)Ends.Length];
            // No doubled letters across the join that read badly ("Brookkbrook"): skip an ending that repeats the start.
            if (start.ToLowerInvariant().EndsWith(end) || end.StartsWith(start.ToLowerInvariant()))
            {
                end = Ends[(hash / (uint)Starts.Length + 1) % (uint)Ends.Length];
            }
            return start + end;
        }

        /// <summary>
        /// The name of a settlement of the bounded world (GL597), ending in its type ("Brookmere Village"), and unlike any
        /// name in <paramref name="taken"/>, which it is added to.
        /// </summary>
        public static string Generate(int seed, Vector2Int cell, string typeName, ISet<string> taken)
        {
            string name = null;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                name = Generate(seed + attempt * 7919, cell) + " " + typeName;
                if (taken == null || taken.Add(name))
                {
                    break;
                }
            }
            return name;
        }

        // A small integer hash (a few rounds of multiply and shift), the same on every platform.
        private static uint Hash(uint a, uint b, uint c)
        {
            uint h = a * 0x9E3779B1u ^ b * 0x85EBCA77u ^ c * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
