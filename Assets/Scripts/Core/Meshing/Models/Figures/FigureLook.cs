using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>What a figure wears on its head (GL608).</summary>
    public enum FigureHat
    {
        None,
        Cap,
        Brimmed,
        Hood,
        TopHat,
    }

    /// <summary>The pieces of a figure's outfit beyond shirt, trousers and boots (GL608); any mix of them.</summary>
    [Flags]
    public enum FigureOutfit
    {
        None = 0,
        Apron = 1 << 0,
        Robe = 1 << 1,
        Coat = 1 << 2,
        Patched = 1 << 3,
        Backpack = 1 << 4,
        Cloak = 1 << 5,
        Beard = 1 << 6,
        Belt = 1 << 7,
        Scarf = 1 << 8,
        BareArms = 1 << 9,
    }

    /// <summary>What a figure can hold in its right hand (GL608).</summary>
    public enum FigureProp
    {
        None,
        Hammer,
        Broom,
        Pick,
        Book,
        Stick,
        Dagger,
        Staff,
    }

    /// <summary>
    /// A low-poly person described as data (GL608): proportions, colours, outfit pieces and the prop for the hand.
    /// <see cref="FigureMeshes"/> turns it into meshes; <see cref="FigureLooks"/> holds the presets and the seeded variation.
    /// </summary>
    public struct FigureLook
    {
        /// <summary>Overall scale: 1 is 1.76 m to the top of the head.</summary>
        public float Height;

        /// <summary>How broad the shoulders, hips and limbs are; 1 is average.</summary>
        public float Build;

        /// <summary>The head's size; 1 is already a little large for the body.</summary>
        public float HeadSize;

        public Color Skin;
        public Color Hair;
        public Color Shirt;
        public Color Trousers;
        public Color Boots;

        /// <summary>The apron, patches, scarf, pack or trim, whichever the outfit has.</summary>
        public Color Accent;

        public Color Hat;
        public FigureHat HatStyle;
        public FigureOutfit Outfit;
        public FigureProp Prop;

        /// <summary>True when the outfit has a piece.</summary>
        public readonly bool Has(FigureOutfit piece)
        {
            return (Outfit & piece) != 0;
        }

        /// <summary>
        /// Names everything the meshes depend on (not <see cref="Height"/>, which is the rig's scale, nor the prop), so two
        /// figures that look the same share their meshes.
        /// </summary>
        public readonly string MeshKey()
        {
            return $"{Mathf.RoundToInt(Build * 100f)}/{Mathf.RoundToInt(HeadSize * 100f)}/{Hex(Skin)}{Hex(Hair)}{Hex(Shirt)}{Hex(Trousers)}{Hex(Boots)}"
                + $"{Hex(Accent)}{Hex(Hat)}/{(int)HatStyle}/{(int)Outfit}";
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
