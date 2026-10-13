using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Shows where the player's land ends (GL42): a wall fading out upwards, the world's full height, along
    /// every side of an owned parcel that borders land not owned yet. Rebuilt when a parcel is
    /// granted or bought. One mesh, drawn from both sides, in vertex colours.
    /// </summary>
    [RequireComponent(typeof(PlayerParcels))]
    public class ParcelBorders : MonoBehaviour
    {
        private static readonly Vector2Int[] Sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        [Tooltip("A transparent material that draws vertex colours (e.g. LabVertexColorTransparent).")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color color = new Color(1f, 0.82f, 0.3f, 0.18f);

        private PlayerParcels player;
        private LandParcels parcels;
        private GameObject wall;
        private Mesh mesh;

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
        }

        private void OnDestroy()
        {
            if (parcels != null)
            {
                parcels.Changed -= Rebuild;
            }
            if (wall != null)
            {
                Destroy(wall);
            }
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void Rebuild()
        {
            WorldView view = player.WorldView;
            if (view == null || material == null)
            {
                return;
            }
            if (wall == null)
            {
                wall = new GameObject("Parcel borders");
                wall.transform.SetParent(view.transform, false);
                mesh = new Mesh { name = "Parcel borders" };
                wall.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = wall.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            float top = view.World.Grid.ChunkWorldSize.y * view.Config.WorldHeightInChunks;
            float size = parcels.Size;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
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
                    AddWall(vertices, triangles, centre - along, centre + along, top);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            // Faded out towards the top (each quad's last two vertices), so it reads near the ground
            // and doesn't wall off the sky.
            var colors = new Color[vertices.Count];
            var clear = new Color(color.r, color.g, color.b, 0f);
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = i % 4 < 2 ? color : clear;
            }
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // A vertical quad from the bottom of the world to its top, facing both ways.
        private static void AddWall(List<Vector3> vertices, List<int> triangles, Vector2 from, Vector2 to, float top)
        {
            int first = vertices.Count;
            vertices.Add(new Vector3(from.x, 0f, from.y));
            vertices.Add(new Vector3(to.x, 0f, to.y));
            vertices.Add(new Vector3(to.x, top, to.y));
            vertices.Add(new Vector3(from.x, top, from.y));
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }
    }
}
