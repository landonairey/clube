using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Game
{
    /// <summary>
    /// Shows where the player's land ends (GL42, GL56): a fence along every side of an owned
    /// parcel that borders land not owned yet. It follows the generated ground, standing a little
    /// above it, and carries on down through the ground to the bottom of the world, so it shows
    /// in the walls of a hole dug beside it. A see-through band with a brighter top rail and
    /// posts. Rebuilt when a parcel is granted or bought, or the terrain is regenerated. One
    /// mesh, drawn from both sides, in vertex colours.
    /// </summary>
    [RequireComponent(typeof(PlayerParcels))]
    public class ParcelBorders : MonoBehaviour
    {
        private static readonly Vector2Int[] Sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // The rail's and posts' thickness, in metres.
        private const float RailHeight = 0.08f;
        private const float PostWidth = 0.1f;

        [Tooltip("A transparent material that draws vertex colours (e.g. LabVertexColorTransparent).")]
        [SerializeField]
        private Material material;

        [Tooltip("The band, from the bottom of the world to the top of the fence.")]
        [SerializeField]
        private Color color = new Color(1f, 0.82f, 0.3f, 0.18f);

        [Tooltip("The top rail and the posts.")]
        [SerializeField]
        private Color railColor = new Color(1f, 0.82f, 0.3f, 0.75f);

        [Tooltip("How far the fence stands above the ground, in metres.")]
        [SerializeField, Min(0.1f)]
        private float height = 0.75f;

        [Tooltip("Metres between the points where the fence meets the ground's height.")]
        [SerializeField, Min(0.1f)]
        private float step = 0.5f;

        [Tooltip("Metres between posts.")]
        [SerializeField, Min(0.5f)]
        private float postSpacing = 2f;

        private PlayerParcels player;
        private LandParcels parcels;
        private ITerrainGenerator builtFor;
        private GameObject fence;
        private Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        private void Awake()
        {
            player = GetComponent<PlayerParcels>();
        }

        private void Update()
        {
            if (parcels == null && player.Parcels != null)
            {
                parcels = player.Parcels;
                parcels.Changed += Rebuild;
                Rebuild();
            }
            // A regenerated world (a lab config change) has new ground to follow.
            else if (parcels != null && player.WorldView != null && player.WorldView.Generator != builtFor)
            {
                Rebuild();
            }
        }

        private void OnDestroy()
        {
            if (parcels != null)
            {
                parcels.Changed -= Rebuild;
            }
            if (fence != null)
            {
                Destroy(fence);
            }
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void Rebuild()
        {
            WorldView view = player.WorldView;
            if (view == null || !view.IsReady || material == null)
            {
                return;
            }
            builtFor = view.Generator;
            if (fence == null)
            {
                fence = new GameObject("Parcel fence");
                fence.transform.SetParent(view.transform, false);
                mesh = new Mesh { name = "Parcel fence", indexFormat = IndexFormat.UInt32 };
                fence.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = fence.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            float top = view.World.Grid.ChunkWorldSize.y * view.Config.WorldHeightInChunks;
            float size = parcels.Size;
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            foreach (Vector2Int parcel in parcels.Owned)
            {
                foreach (Vector2Int side in Sides)
                {
                    if (parcels.IsOwned(parcel + side))
                    {
                        continue;
                    }
                    // The side's two ends along the ground.
                    Vector2 centre = ((Vector2)parcel + new Vector2(0.5f, 0.5f) + (Vector2)side * 0.5f) * size;
                    Vector2 along = new Vector2(-side.y, side.x) * (size * 0.5f);
                    AddSide(builtFor, centre - along, centre + along, top);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // One side: the band and rail in steps that follow the ground, and posts along it.
        private void AddSide(ITerrainGenerator generator, Vector2 from, Vector2 to, float worldTop)
        {
            float length = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / step));
            Vector2 direction = (to - from) / length;
            float previousTop = 0f;
            Vector2 previous = from;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, (float)i / steps);
                float fenceTop = Mathf.Min(worldTop, GroundAt(generator, point, worldTop) + height);
                if (i > 0)
                {
                    // The band from the bottom of the world, so it shows underground, then the rail.
                    AddQuad(previous, point, 0f, 0f, previousTop, fenceTop, color);
                    AddQuad(previous, point, previousTop - RailHeight, fenceTop - RailHeight, previousTop, fenceTop, railColor);
                }
                previous = point;
                previousTop = fenceTop;
            }

            int posts = Mathf.Max(1, Mathf.RoundToInt(length / postSpacing));
            for (int i = 0; i <= posts; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, (float)i / posts);
                float postTop = Mathf.Min(worldTop, GroundAt(generator, point, worldTop) + height + RailHeight);
                Vector2 half = direction * (PostWidth * 0.5f);
                AddQuad(point - half, point + half, 0f, 0f, postTop, postTop, railColor);
            }
        }

        private static float GroundAt(ITerrainGenerator generator, Vector2 point, float worldTop)
        {
            return generator != null ? TerrainSurface.HeightAt(generator, point.x, point.y, 0f, worldTop) : 0f;
        }

        // A vertical quad between two points on the ground plane, from each end's bottom to its top, facing both ways.
        private void AddQuad(Vector2 from, Vector2 to, float fromBottom, float toBottom, float fromTop, float toTop, Color tint)
        {
            int first = vertices.Count;
            vertices.Add(new Vector3(from.x, fromBottom, from.y));
            vertices.Add(new Vector3(to.x, toBottom, to.y));
            vertices.Add(new Vector3(to.x, toTop, to.y));
            vertices.Add(new Vector3(from.x, fromTop, from.y));
            for (int i = 0; i < 4; i++)
            {
                colors.Add(tint);
            }
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }
    }
}
