using System.Globalization;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The player's one waypoint on the bounded map (GL866): either a settlement of the plan (by its index) or a dropped
    /// pin, at a position in the map's own metres (x east, y north, as <see cref="PlannedSettlement.Position"/>).
    /// A value type: setting a new one replaces the old. Saved as one short string (<see cref="Encode"/>).
    /// </summary>
    public readonly struct MapWaypoint
    {
        /// <summary>No waypoint.</summary>
        public static readonly MapWaypoint None = default;

        /// <summary>True when there is a waypoint.</summary>
        public bool IsSet { get; }

        /// <summary>The settlement's index in the plan, or -1 for a dropped pin.</summary>
        public int Settlement { get; }

        /// <summary>Where it is, in the map's metres (x east, y north).</summary>
        public Vector2 Position { get; }

        /// <summary>True for a dropped pin rather than a settlement.</summary>
        public bool IsPin => IsSet && Settlement < 0;

        private MapWaypoint(int settlement, Vector2 position)
        {
            IsSet = true;
            Settlement = settlement;
            Position = position;
        }

        /// <summary>A waypoint on the settlement with this plan index, at its position.</summary>
        public static MapWaypoint AtSettlement(int index, Vector2 position)
        {
            return new MapWaypoint(Mathf.Max(0, index), position);
        }

        /// <summary>A pin dropped at this map position.</summary>
        public static MapWaypoint Pin(Vector2 position)
        {
            return new MapWaypoint(-1, position);
        }

        /// <summary>"settlement;x;y" (settlement -1 for a pin), or empty for none.</summary>
        public string Encode()
        {
            if (!IsSet)
            {
                return "";
            }
            CultureInfo c = CultureInfo.InvariantCulture;
            return Settlement.ToString(c) + ";" + Position.x.ToString("R", c) + ";" + Position.y.ToString("R", c);
        }

        /// <summary>Reads <see cref="Encode"/>'s text; anything else (empty, damaged) is <see cref="None"/>.</summary>
        public static MapWaypoint Decode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return None;
            }
            string[] parts = text.Split(';');
            CultureInfo c = CultureInfo.InvariantCulture;
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.Integer, c, out int settlement)
                || !float.TryParse(parts[1], NumberStyles.Float, c, out float x)
                || !float.TryParse(parts[2], NumberStyles.Float, c, out float y))
            {
                return None;
            }
            return settlement < 0 ? Pin(new Vector2(x, y)) : AtSettlement(settlement, new Vector2(x, y));
        }

        /// <summary>
        /// Where a mark sits on a north-up square minimap (GL867): <paramref name="offset"/> is the mark's place from the
        /// player in metres (x east, y north) and <paramref name="range"/> the metres from the middle to the map's edge.
        /// Returns pixels from the map's middle (x right, y down) for a map <paramref name="halfSize"/> pixels from middle
        /// to edge. Inside the map's range the mark is at its real spot; further, it is held on the rim (the square's
        /// edge, <paramref name="inset"/> pixels in) along the bearing from the player, and <paramref name="onRim"/> is true.
        /// </summary>
        public static Vector2 OnMinimap(Vector2 offset, float range, float halfSize, float inset, out bool onRim)
        {
            Vector2 pixels = new Vector2(offset.x, -offset.y) / Mathf.Max(0.001f, range) * halfSize;
            float limit = Mathf.Max(0f, halfSize - inset);
            float far = Mathf.Max(Mathf.Abs(pixels.x), Mathf.Abs(pixels.y));
            onRim = far > limit;
            return onRim ? pixels * (limit / far) : pixels;
        }
    }
}
