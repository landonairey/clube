using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How a kind of route is drawn on the map (GL635): its name for the legend, colour, width, and what tells it from
    /// the others at a glance: dashes, or a dark casing along both sides. Lengths are pixels of a picture about 750 wide.
    /// </summary>
    public readonly struct MapLineStyle
    {
        public MapLineStyle(string name, Color colour, float width, float dash = 0f, float gap = 0f, float casing = 0f)
        {
            Name = name;
            Colour = colour;
            Width = width;
            Dash = dash;
            Gap = gap;
            Casing = casing;
        }

        public string Name { get; }
        public Color Colour { get; }
        public float Width { get; }

        /// <summary>Length of each dash; 0 for a solid line.</summary>
        public float Dash { get; }

        /// <summary>Length of the gap after each dash.</summary>
        public float Gap { get; }

        /// <summary>Width of the dark edge on each side; 0 for none.</summary>
        public float Casing { get; }

        public bool IsDashed => Dash > 0f;
    }
}
