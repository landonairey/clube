using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The Mining Corp's quarry for the opening tutorial (GL479): a pit (<see cref="OpenPit"/>) dug out of the gate of the
    /// trading post and beside its road (<see cref="CompanyWork.QuarryCentre"/>), or near the spawn when there's no town,
    /// and the company's ore cart on its rim. Digs a slice of the pit a frame once its chunks have loaded.
    /// </summary>
    public sealed class CompanyQuarry
    {
        // Samples emptied a frame while the pit is dug.
        private const int SamplesPerFrame = 2000;

        private readonly WorldView worldView;
        private Queue<Vector3Int> order;

        public CompanyQuarry(WorldView worldView, Vector2 centre)
        {
            this.worldView = worldView;
            Centre = centre;
            Ground = TerrainSurface.HeightAt(worldView.Generator, centre.x, centre.y, 0f, Top);
        }

        /// <summary>The pit's centre (the world's local metres, x and z).</summary>
        public Vector2 Centre { get; }

        /// <summary>The ground at the centre as generated (local metres).</summary>
        public float Ground { get; }

        public float Floor => Ground - CompanyWork.Depth;

        /// <summary>The pit's radius at the ground.</summary>
        public float RimRadius => OpenPit.RimRadius(CompanyWork.Depth);

        /// <summary>True once the whole pit is dug.</summary>
        public bool IsDug { get; private set; }

        private float Top => worldView.Config.WorldHeightInChunks * worldView.World.Grid.ChunkWorldSize.y;

        /// <summary>A point on the rim (world space), <paramref name="angle"/> degrees round from the town's side.</summary>
        public Vector3 OnRim(float angle, float extra = 1.5f)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 at = Centre + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * (RimRadius + extra);
            float ground = TerrainSurface.HeightAt(worldView.Generator, at.x, at.y, 0f, Top);
            return worldView.transform.TransformPoint(new Vector3(at.x, ground, at.y));
        }

        /// <summary>Metres (flat) from the pit's centre to a point in world space.</summary>
        public float DistanceTo(Vector3 world)
        {
            Vector3 local = worldView.transform.InverseTransformPoint(world);
            return (new Vector2(local.x, local.z) - Centre).magnitude;
        }

        /// <summary>Digs the next slice of the pit, once its chunks are loaded; true when it's all dug.</summary>
        public bool Dig()
        {
            if (IsDug || !worldView.IsReady)
            {
                return IsDug;
            }
            float iso = worldView.Config.IsoLevel;
            if (order == null)
            {
                if (!Loaded())
                {
                    return false;
                }
                order = OpenPit.Plan(worldView.World, Centre, Floor, Ground + 2f, iso);
            }
            OpenPit.Dig(worldView.World, order, SamplesPerFrame, iso, null);
            IsDug = order.Count == 0;
            return IsDug;
        }

        private bool Loaded()
        {
            WorldGrid grid = worldView.World.Grid;
            float reach = RimRadius + 3f;
            Vector3Int min = grid.WorldToChunk(new Vector3(Centre.x - reach, Mathf.Max(0f, Floor - 1f), Centre.y - reach));
            Vector3Int max = grid.WorldToChunk(new Vector3(Centre.x + reach, Ground + 3f, Centre.y + reach));
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(worldView.Config.WorldHeightInChunks - 1, max.y); y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!worldView.World.IsLoaded(new Vector3Int(x, y, z)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>The company's ore cart (a low-poly hand cart, solid, with its hand-in prompt) at a point, facing the pit.</summary>
        public static OreCart BuildCart(Vector3 at, Vector3 facing, Material material, MiningCorpTutorial tutorial)
        {
            var wood = new Color(0.42f, 0.29f, 0.17f);
            var iron = new Color(0.28f, 0.29f, 0.31f);
            var mesh = new LowPolyMeshBuilder();
            mesh.Box(new Vector3(0f, 0.75f, 0f), new Vector3(1.1f, 0.5f, 1.5f), Quaternion.identity, wood);
            mesh.Box(new Vector3(0f, 1.0f, 0f), new Vector3(0.95f, 0.06f, 1.35f), Quaternion.identity, new Color(0.45f, 0.45f, 0.43f));
            foreach (float side in new[] { -0.6f, 0.6f })
            {
                mesh.Prism(new Vector3(side - 0.05f, 0.35f, 0f), new Vector3(side + 0.05f, 0.35f, 0f), 0.35f, 0.35f, 8, iron);
            }
            mesh.Prism(new Vector3(0f, 0.75f, 0.75f), new Vector3(0f, 0.9f, 1.5f), 0.04f, 0.04f, 5, wood);
            var cart = new GameObject("Company ore cart");
            cart.transform.position = at;
            Vector3 flat = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                cart.transform.rotation = Quaternion.LookRotation(flat);
            }
            cart.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Ore cart");
            cart.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = cart.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.3f, 1.2f, 1.6f);
            OreCart ore = cart.AddComponent<OreCart>();
            ore.Tutorial = tutorial;
            return ore;
        }
    }
}
