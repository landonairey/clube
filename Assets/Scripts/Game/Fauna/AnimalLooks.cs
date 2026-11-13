using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>What grows on an animal's head (GL604).</summary>
    public enum AnimalHorns
    {
        None,

        /// <summary>Two short horns swept back.</summary>
        Goat,

        /// <summary>Short horns out to the sides and up.</summary>
        Cow,

        /// <summary>Long horns wide to the sides, tips up.</summary>
        Yak,

        /// <summary>Small upturned horns close to a big head.</summary>
        Bison,

        /// <summary>Branching antlers.</summary>
        Antlers,

        /// <summary>Tall, wide antlers with many tines.</summary>
        GreatAntlers,
    }

    /// <summary>
    /// A species' model recipe (GL604): the proportions and colours <see cref="AnimalModels"/> builds its low-poly body
    /// from. Lengths are shares of the species' own size (its length and shoulder height), so the recipe says what makes the
    /// silhouette (a long neck, a hump, wool, antlers), not how big it is. A new species needs a row here by its id.
    /// </summary>
    public sealed class AnimalLook
    {
        public Color Coat = new Color(0.6f, 0.45f, 0.3f);

        /// <summary>Legs, face and horns' base.</summary>
        public Color Skin = new Color(0.3f, 0.22f, 0.16f);

        public Color Horn = new Color(0.85f, 0.8f, 0.68f);

        /// <summary>The trunk's length, as a share of the animal's.</summary>
        public float Trunk = 0.68f;

        /// <summary>The trunk's width, as a share of its length (its depth is what the legs leave of the height).</summary>
        public float Girth = 0.42f;

        /// <summary>The legs' length, as a share of the shoulder height, and their thickness as a share of that length.</summary>
        public float Legs = 0.5f, LegThickness = 0.16f;

        /// <summary>The neck: its length as a share of the animal's, and how steeply it rises (0 level, 90 straight up).</summary>
        public float Neck = 0.18f, NeckRise = 40f;

        /// <summary>The head's length, as a share of the animal's.</summary>
        public float Head = 0.2f;

        /// <summary>Ears' length as a share of the head's; long ones stand up (a rabbit's).</summary>
        public float Ears = 0.35f;

        public bool EarsUp;

        public AnimalHorns Horns;

        /// <summary>A hump over the shoulders, as a share of the trunk's depth (a bison's, a yak's).</summary>
        public float Hump;

        /// <summary>Wool: lumps over the trunk (and up the neck when it is long).</summary>
        public bool Woolly;

        /// <summary>A skirt of long hair down the flanks (a yak's).</summary>
        public bool Shaggy;

        public bool Beard;

        /// <summary>The tail's length, as a share of the animal's; 0 for a tuft.</summary>
        public float Tail = 0.12f;

        /// <summary>How far the head swings down to graze, in degrees.</summary>
        public float GrazeDip = 60f;
    }

    /// <summary>The model recipes by species id (GL604).</summary>
    public static class AnimalLooks
    {
        private static readonly Dictionary<string, AnimalLook> Looks = new Dictionary<string, AnimalLook>
        {
            ["rabbit"] = new AnimalLook
            {
                Coat = new Color(0.62f, 0.52f, 0.42f), Skin = new Color(0.55f, 0.45f, 0.36f), Trunk = 0.62f, Girth = 0.55f,
                Legs = 0.3f, LegThickness = 0.35f, Neck = 0.05f, NeckRise = 50f, Head = 0.3f, Ears = 1.1f, EarsUp = true, Tail = 0f, GrazeDip = 35f,
            },
            ["goat"] = new AnimalLook
            {
                Coat = new Color(0.8f, 0.76f, 0.68f), Skin = new Color(0.42f, 0.36f, 0.3f), Horn = new Color(0.4f, 0.36f, 0.3f),
                Girth = 0.4f, Legs = 0.52f, Neck = 0.2f, NeckRise = 55f, Head = 0.2f, Horns = AnimalHorns.Goat, Beard = true, Tail = 0.07f,
            },
            ["sheep"] = new AnimalLook
            {
                Coat = new Color(0.93f, 0.91f, 0.85f), Skin = new Color(0.2f, 0.18f, 0.17f), Trunk = 0.7f, Girth = 0.56f,
                Legs = 0.38f, LegThickness = 0.2f, Neck = 0.12f, NeckRise = 35f, Head = 0.2f, Woolly = true, Tail = 0f,
            },
            ["yak"] = new AnimalLook
            {
                Coat = new Color(0.25f, 0.19f, 0.15f), Skin = new Color(0.16f, 0.12f, 0.1f), Horn = new Color(0.8f, 0.76f, 0.66f),
                Trunk = 0.72f, Girth = 0.46f, Legs = 0.36f, LegThickness = 0.26f, Neck = 0.12f, NeckRise = 5f, Head = 0.22f,
                Horns = AnimalHorns.Yak, Hump = 0.3f, Shaggy = true, Tail = 0.2f, GrazeDip = 45f,
            },
            ["alpaca"] = new AnimalLook
            {
                Coat = new Color(0.85f, 0.68f, 0.48f), Skin = new Color(0.5f, 0.38f, 0.27f), Trunk = 0.62f, Girth = 0.46f,
                Legs = 0.5f, LegThickness = 0.17f, Neck = 0.5f, NeckRise = 80f, Head = 0.19f, Ears = 0.5f, EarsUp = true, Woolly = true, Tail = 0f,
                GrazeDip = 95f,
            },
            ["cow"] = new AnimalLook
            {
                Coat = new Color(0.56f, 0.34f, 0.2f), Skin = new Color(0.9f, 0.86f, 0.78f), Horn = new Color(0.9f, 0.86f, 0.74f),
                Trunk = 0.72f, Girth = 0.42f, Legs = 0.45f, LegThickness = 0.18f, Neck = 0.14f, NeckRise = 15f, Head = 0.22f,
                Horns = AnimalHorns.Cow, Tail = 0.3f,
            },
            ["deer"] = new AnimalLook
            {
                Coat = new Color(0.72f, 0.5f, 0.3f), Skin = new Color(0.45f, 0.3f, 0.18f), Trunk = 0.62f, Girth = 0.32f,
                Legs = 0.6f, LegThickness = 0.1f, Neck = 0.26f, NeckRise = 60f, Head = 0.19f, Ears = 0.5f, Horns = AnimalHorns.Antlers, Tail = 0f,
                GrazeDip = 85f,
            },
            ["elk"] = new AnimalLook
            {
                Coat = new Color(0.55f, 0.42f, 0.3f), Skin = new Color(0.28f, 0.2f, 0.14f), Trunk = 0.64f, Girth = 0.36f,
                Legs = 0.56f, LegThickness = 0.12f, Neck = 0.26f, NeckRise = 55f, Head = 0.2f, Ears = 0.4f, Horns = AnimalHorns.GreatAntlers,
                Tail = 0f, GrazeDip = 80f,
            },
            ["bison"] = new AnimalLook
            {
                Coat = new Color(0.33f, 0.22f, 0.14f), Skin = new Color(0.17f, 0.12f, 0.09f), Horn = new Color(0.2f, 0.18f, 0.16f),
                Trunk = 0.74f, Girth = 0.42f, Legs = 0.4f, LegThickness = 0.2f, Neck = 0.08f, NeckRise = -15f, Head = 0.24f,
                Horns = AnimalHorns.Bison, Hump = 0.55f, Beard = true, Tail = 0.2f, GrazeDip = 35f,
            },
        };

        private static readonly AnimalLook Plain = new AnimalLook();

        /// <summary>True when a species has its own recipe.</summary>
        public static bool Has(string speciesId)
        {
            return Looks.ContainsKey(speciesId);
        }

        /// <summary>A species' recipe, or a plain four-legged one when it has none.</summary>
        public static AnimalLook For(string speciesId)
        {
            return Looks.TryGetValue(speciesId, out AnimalLook look) ? look : Plain;
        }
    }
}
