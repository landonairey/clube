using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// The family parcel's yellow hologram walls during the first quest (GL783): unlit, see-through walls standing on the
    /// parcel's fence line, from the top of the fence up 15 m, fading to nothing at the top. One mesh, built once from the
    /// generated ground (as <see cref="ParcelBorders"/> follows it), so nothing is done per frame. Made and destroyed by
    /// <see cref="ParcelEntryQuest"/>.
    /// </summary>
    public sealed class ParcelHologram : MonoBehaviour
    {
        // How tall the walls are, their colour at the bottom, and the metres between the points where they meet the ground.
        private const float Height = 15f;
        private const float Step = 0.5f;
        private static readonly Color Yellow = new Color(1f, 0.82f, 0.1f, 0.45f);

        private Mesh mesh;

        /// <summary>
        /// Builds the walls round <paramref name="parcel"/>, starting <paramref name="over"/> metres over the ground (the fence's
        /// top), drawn with <paramref name="material"/> (a transparent vertex-colour material, back faces culled).
        /// </summary>
        public static ParcelHologram Build(WorldView world, LandParcels land, Vector2Int parcel, float over, Material material)
        {
            var go = new GameObject("Family parcel hologram");
            go.transform.SetParent(world.transform, false);
            ParcelHologram hologram = go.AddComponent<ParcelHologram>();
            float top = world.World.Grid.ChunkWorldSize.y * world.Config.WorldHeightInChunks;
            Rect area = land.AreaOf(parcel);
            var corners = new[] { new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin), new Vector2(area.xMax, area.yMax), new Vector2(area.xMin, area.yMax) };
            var line = new List<Vector3>();
            for (int c = 0; c < corners.Length; c++)
            {
                Vector2 from = corners[c];
                Vector2 to = corners[(c + 1) % corners.Length];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Step));
                for (int i = c == 0 ? 0 : 1; i <= steps; i++)
                {
                    Vector2 point = Vector2.Lerp(from, to, (float)i / steps);
                    float ground = TerrainSurface.HeightAt(world.Generator, point.x, point.y, 0f, top);
                    line.Add(new Vector3(point.x, Mathf.Min(top, ground) + over, point.y));
                }
            }
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            FadingWall.Add(line, Height, Yellow, vertices, colors, triangles);
            hologram.mesh = new Mesh { name = "Family parcel hologram" };
            hologram.mesh.SetVertices(vertices);
            hologram.mesh.SetColors(colors);
            hologram.mesh.SetTriangles(triangles, 0);
            hologram.mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = hologram.mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return hologram;
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }
    }
}
