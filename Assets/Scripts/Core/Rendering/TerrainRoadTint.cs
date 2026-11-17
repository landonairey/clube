using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL718: hands a <see cref="RoadMask"/> to the terrain shaders as globals (<c>RoadTint.hlsl</c>): the coarse terrain
    /// levels (<c>Clube/Terrain Materials</c> with <c>_LodClip</c> on) and the far terrain (<c>Clube/Far Terrain</c>)
    /// colour the roads into their ground. One mask at a time; <see cref="Clear"/> turns it off.
    /// </summary>
    public static class TerrainRoadTint
    {
        private static readonly int RoadMaskId = Shader.PropertyToID("_RoadMask");
        private static readonly int RoadMaskRectId = Shader.PropertyToID("_RoadMaskRect");

        private static Texture2D texture;

        /// <summary>Whether a mask is set.</summary>
        public static bool IsSet => texture != null;

        /// <summary>
        /// Shows a mask; <paramref name="worldOrigin"/> is where the mask's world (x, z) origin lies in the scene (the
        /// world view's position: the world is never turned).
        /// </summary>
        public static void Set(RoadMask mask, Vector3 worldOrigin)
        {
            if (texture == null || texture.width != mask.Size)
            {
                Clear();
                texture = new Texture2D(mask.Size, mask.Size, TextureFormat.RGBA32, false, true)
                {
                    name = "Road mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
            }
            texture.SetPixels32(mask.Texels);
            texture.Apply(false, false);
            Shader.SetGlobalTexture(RoadMaskId, texture);
            Shader.SetGlobalVector(RoadMaskRectId, new Vector4(worldOrigin.x + mask.Area.xMin, worldOrigin.z + mask.Area.yMin, 1f / mask.Area.width, 1f));
        }

        public static void Clear()
        {
            Shader.SetGlobalVector(RoadMaskRectId, Vector4.zero);
            if (texture != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(texture);
                }
                else
                {
                    Object.DestroyImmediate(texture);
                }
                texture = null;
            }
        }
    }
}
