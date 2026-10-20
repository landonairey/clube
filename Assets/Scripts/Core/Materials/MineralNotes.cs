using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How a mineral looks and feels, in a prospector's words (GL73): what the mining journal
    /// writes about an ore once it's found. Text only; nothing in the game reads it as a rule.
    /// </summary>
    [Serializable]
    public class MineralNotes
    {
        [Tooltip("Hardness as a prospector would put it, e.g. \"6 to 7 on Mohs' scale; scratches glass\".")]
        public string Hardness = "";

        [Tooltip("How it catches the light, e.g. \"metallic\", \"greasy\".")]
        public string Luster = "";

        [Tooltip("Its colour, e.g. \"brass yellow with an iridescent tarnish\".")]
        public string Colour = "";

        [Tooltip("How it feels in the hand, e.g. \"heavy for its size, brittle\".")]
        public string Feel = "";
    }
}
