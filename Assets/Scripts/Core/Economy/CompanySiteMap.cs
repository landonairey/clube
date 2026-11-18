using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The map of the Mining Corp's site on the tutorial's own map (GL760): the square of ground it covers and its picture,
    /// drawn from <see cref="CompanyWork"/>'s layout: the open ground, the warehouse, and the mountain in height bands.
    /// </summary>
    public static class CompanySiteMap
    {
        private const float ContourStep = 12f;
        private const float ContourShade = 0.78f;

        /// <summary>The ground the map shows (x east, y north): the company's yard with the mountain's face all round it.</summary>
        public static readonly Rect Area = new Rect(-6f, -36f, 112f, 112f);

        /// <summary>The colour of the level ground.</summary>
        public static readonly Color32 OpenGround = new Color32(142, 133, 96, 255);

        /// <summary>The colour of the warehouse's roof.</summary>
        public static readonly Color32 WarehouseRoof = new Color32(84, 56, 38, 255);

        private static readonly Color32 MountainFoot = new Color32(118, 112, 104, 255);
        private static readonly Color32 MountainTop = new Color32(205, 203, 198, 255);

        /// <summary>Where a point (x east, y north) lies on the map: 0..1 each way from the south-west corner.</summary>
        public static Vector2 Fraction(Vector2 point)
        {
            return new Vector2((point.x - Area.xMin) / Area.width, (point.y - Area.yMin) / Area.height);
        }

        /// <summary>The map's picture, <paramref name="size"/> pixels a side, row by row from the south.</summary>
        public static Color32[] Render(int size)
        {
            var pixels = new Color32[size * size];
            float step = Area.width / size;
            for (int row = 0; row < size; row++)
            {
                float z = Area.yMin + (row + 0.5f) * step;
                for (int column = 0; column < size; column++)
                {
                    float x = Area.xMin + (column + 0.5f) * step;
                    pixels[row * size + column] = ColourAt(x, z, step);
                }
            }
            return pixels;
        }

        private static Color32 ColourAt(float x, float z, float step)
        {
            if (CompanyWork.Warehouse.Contains(new Vector2(x, z)))
            {
                return WarehouseRoof;
            }
            float height = CompanyWork.Height(x, z);
            float rise = height - CompanyWork.Ground;
            if (rise <= 0f)
            {
                return OpenGround;
            }
            Color colour = Color32.Lerp(MountainFoot, MountainTop, rise / CompanyWork.MountainRise);
            int band = Mathf.FloorToInt(height / ContourStep);
            bool contour = band != Mathf.FloorToInt(CompanyWork.Height(x + step, z) / ContourStep)
                || band != Mathf.FloorToInt(CompanyWork.Height(x, z + step) / ContourStep);
            return contour ? Color.Lerp(Color.black, colour, ContourShade) : colour;
        }
    }
}
