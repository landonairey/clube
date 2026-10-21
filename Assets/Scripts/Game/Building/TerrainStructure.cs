using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A placed object built from the ground itself (GL85, first pass of PK17's structures): when
    /// placed it writes its samples (a campfire's ring of stone and its logs) into the terrain
    /// through the world's edit path, and while they all stand it works as itself (its
    /// <see cref="StationObject"/>). Mining any of them breaks it: what's left is plain ground, and
    /// the object goes, handing what its station held to the player. Picked up whole (hold B), it
    /// takes its samples back out of the terrain, so nothing is gained either way. Samples of one material can
    /// glow along their inner side (a campfire's logs smouldering).
    /// </summary>
    /// <remarks>
    /// Offsets are in samples from the first air sample above the ground at the object's centre;
    /// only samples that were air are written, so it never turns ground into something else. A
    /// child named in <see cref="placementPreview"/> carries the model the build preview copies,
    /// and is hidden once the samples are in.
    /// </remarks>
    [RequireComponent(typeof(PlacedObject))]
    public class TerrainStructure : MonoBehaviour
    {
        /// <summary>One sample of the structure: where it goes and what it's made of.</summary>
        [Serializable]
        public struct Part
        {
            public Vector3Int Offset;
            public VoxelMaterial Material;

            [Tooltip("Its density; 0 takes the structure's sample density. Shaping densities let marching cubes round a structure off between samples (the furnace's taper).")]
            [Range(0f, 1f)]
            public float Density;
        }

        // Seconds between checks that the structure still stands.
        private const float CheckSeconds = 0.25f;

        [SerializeField]
        private List<Part> parts = new List<Part>();

        [Tooltip("Density its samples are written at: solid, but below 1 keeps each stone or log a small lump of its own.")]
        [SerializeField, Range(0.55f, 1f)]
        private float sampleDensity = 0.8f;

        [Tooltip("A child with the model the build preview shows; hidden once the samples are in the ground.")]
        [SerializeField]
        private GameObject placementPreview;

        [Tooltip("Samples of this material glow along their inner side, facing the centre (a campfire's logs, smouldering).")]
        [SerializeField]
        private VoxelMaterial glowsOn;

        [Tooltip("An unlit, bright material for the glowing lines (Glow).")]
        [SerializeField]
        private Material glowMaterial;

        private readonly List<Vector3Int> stamped = new List<Vector3Int>();
        private Vector3Int origin;
        private GameObject glow;
        private Mesh glowMesh;
        private WorldView worldView;
        private float nextCheck;

        /// <summary>What it's mostly built of: its first part's material (the furnace's cobbles or clay).</summary>
        public VoxelMaterial MainMaterial => parts.Count > 0 ? parts[0].Material : null;

        /// <summary>The samples it wrote into the terrain.</summary>
        public IReadOnlyList<Vector3Int> Stamped => stamped;

        public bool IsStanding { get; private set; }

        private void Start()
        {
            worldView = FindFirstObjectByType<WorldView>();
            Stamp();
            BuildGlow();
            if (placementPreview != null)
            {
                placementPreview.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsStanding || Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + CheckSeconds;
            World world = worldView != null ? worldView.World : null;
            if (world == null)
            {
                return;
            }
            float iso = worldView.Config.IsoLevel;
            foreach (Vector3Int sample in stamped)
            {
                float? density = world.GetDensity(sample);
                if (density.HasValue && density.Value < iso)
                {
                    Collapse();
                    return;
                }
            }
        }

        /// <summary>Takes its samples back out of the terrain (picked up whole); nothing is collected.</summary>
        public void Clear()
        {
            World world = worldView != null ? worldView.World : null;
            if (world != null)
            {
                foreach (Vector3Int sample in stamped)
                {
                    world.SetDensity(sample, 0f);
                }
            }
            stamped.Clear();
            IsStanding = false;
        }

        private void OnDestroy()
        {
            Destroy(glowMesh);
        }

        // Two thin glowing lines on the side of each glowing sample that faces the centre, lying on
        // its mesh face (AddSurfaceLine) at different heights.
        private void BuildGlow()
        {
            World world = worldView != null ? worldView.World : null;
            if (world == null || glowsOn == null || glowMaterial == null)
            {
                return;
            }
            float voxel = world.Grid.VoxelSize;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector3Int sample in stamped)
            {
                if (world.GetMaterial(sample) != glowsOn.Id)
                {
                    continue;
                }
                Vector3 centre = worldView.transform.TransformPoint(world.Grid.SampleToWorld(new Vector3Int(origin.x, sample.y, origin.z)));
                Vector3 toLog = worldView.transform.TransformPoint(world.Grid.SampleToWorld(sample)) - centre;
                if (toLog.sqrMagnitude < 1e-6f)
                {
                    continue;
                }
                // Where a ray from the centre meets the log; if it misses, about where a sample's surface sits.
                if (!worldView.Raycast(new Ray(centre, toLog.normalized), out Vector3 surface)
                    || (surface - centre).sqrMagnitude > toLog.sqrMagnitude)
                {
                    surface = centre + toLog * 0.6f;
                }
                Vector3 outward = -toLog.normalized;
                Vector3 along = Vector3.Cross(Vector3.up, outward).normalized;
                AddSurfaceLine(vertices, triangles, surface + Vector3.down * (voxel * 0.08f), along, voxel * 0.8f, outward, voxel * 0.4f);
                AddSurfaceLine(vertices, triangles, surface + Vector3.up * (voxel * 0.12f), along, voxel * 0.5f, outward, voxel * 0.4f);
            }
            if (vertices.Count == 0)
            {
                return;
            }
            glowMesh = new Mesh { name = "Structure glow" };
            glowMesh.SetVertices(vertices);
            glowMesh.SetTriangles(triangles, 0);
            glowMesh.RecalculateNormals();
            glow = new GameObject("Glow lines");
            glow.transform.SetParent(transform, false);
            glow.AddComponent<MeshFilter>().sharedMesh = glowMesh;
            var meshRenderer = glow.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = glowMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static readonly List<Vector3> LinePoints = new List<Vector3>();

        // A glowing line lying on the terrain's face: points along it (world space) are each cast
        // onto the surface from the fire's side, a little way out, and joined, so the line bends with
        // the mesh. Where a cast misses, the line breaks there.
        private void AddSurfaceLine(List<Vector3> vertices, List<int> triangles, Vector3 middle, Vector3 along, float length, Vector3 outward, float reach)
        {
            const int points = 9;
            LinePoints.Clear();
            for (int i = 0; i < points; i++)
            {
                Vector3 onLine = middle + along * (length * (i / (points - 1f) - 0.5f));
                Vector3 from = onLine + outward * reach;
                if (worldView.Raycast(new Ray(from, -outward), out Vector3 hit) && (hit - from).sqrMagnitude <= 4f * reach * reach)
                {
                    // A hair off the face, so it draws over it.
                    LinePoints.Add(transform.InverseTransformPoint(hit + outward * 0.004f));
                }
                else
                {
                    AddPolyline(vertices, triangles, transform.InverseTransformDirection(outward));
                    LinePoints.Clear();
                }
            }
            AddPolyline(vertices, triangles, transform.InverseTransformDirection(outward));
        }

        // LinePoints joined by thin flat strips facing out, drawn from both sides.
        private static void AddPolyline(List<Vector3> vertices, List<int> triangles, Vector3 facing)
        {
            const float halfWidth = 0.012f;
            for (int i = 1; i < LinePoints.Count; i++)
            {
                Vector3 a = LinePoints[i - 1];
                Vector3 b = LinePoints[i];
                Vector3 across = Vector3.Cross(b - a, facing).normalized * halfWidth;
                int first = vertices.Count;
                vertices.Add(a - across);
                vertices.Add(b - across);
                vertices.Add(b + across);
                vertices.Add(a + across);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }
        }

        // Writes each part into an air sample around the centre.
        private void Stamp()
        {
            World world = worldView != null ? worldView.World : null;
            if (world == null)
            {
                return;
            }
            float iso = worldView.Config.IsoLevel;
            Vector3 local = worldView.transform.InverseTransformPoint(transform.position) + Vector3.up * (world.Grid.VoxelSize * 0.5f);
            origin = world.Grid.WorldToNearestSample(local);
            // Turned as the object was placed (R turns it a quarter, GL28), so a furnace's mouth faces its front.
            int quarters = Mathf.RoundToInt(transform.eulerAngles.y / 90f) & 3;
            // Each column settles on the ground under it, all before any part is written: a part higher up
            // (a furnace's roof) stands on its column's ground, not on the hollow under it.
            var samples = new List<Vector3Int>(parts.Count);
            foreach (Part part in parts)
            {
                Vector3Int offset = Turn(part.Offset, quarters);
                Vector3Int ground = Settle(world, origin + new Vector3Int(offset.x, 0, offset.z), iso);
                samples.Add(ground + Vector3Int.up * offset.y);
            }
            for (int i = 0; i < parts.Count; i++)
            {
                Part part = parts[i];
                Vector3Int sample = samples[i];
                float? density = world.GetDensity(sample);
                if (part.Material == null || !density.HasValue || density.Value >= iso)
                {
                    continue;
                }
                world.SetDensity(sample, part.Density > 0f ? Mathf.Max(part.Density, iso + 0.02f) : sampleDensity);
                world.SetMaterial(sample, part.Material.Id);
                stamped.Add(sample);
            }
            IsStanding = stamped.Count > 0;
        }

        // An offset turned clockwise seen from above by quarter turns, as Unity turns +Z towards +X.
        private static Vector3Int Turn(Vector3Int offset, int quarters)
        {
            for (int i = 0; i < quarters; i++)
            {
                offset = new Vector3Int(offset.z, offset.y, -offset.x);
            }
            return offset;
        }

        // A part's sample moved up or down its column (two samples at most) to the first air sample
        // resting on solid ground, so the ring follows ground that isn't quite flat.
        private static Vector3Int Settle(World world, Vector3Int sample, float iso)
        {
            for (int step = 0; step < 2 && Solid(world, sample, iso); step++)
            {
                sample += Vector3Int.up;
            }
            for (int step = 0; step < 2 && !Solid(world, sample + Vector3Int.down, iso); step++)
            {
                sample += Vector3Int.down;
            }
            return sample;
        }

        private static bool Solid(World world, Vector3Int sample, float iso)
        {
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value >= iso;
        }

        // Mined into: it's just ground now. The station's contents go to the player.
        private void Collapse()
        {
            IsStanding = false;
            var station = GetComponent<StationObject>();
            var player = FindFirstObjectByType<PlayerInventory>();
            if (station != null && player != null && player.Inventory != null)
            {
                Return(station.Station.Input, player.Inventory);
                Return(station.Station.Output, player.Inventory);
                if (station.Station.Fuel != null)
                {
                    Return(station.Station.Fuel, player.Inventory);
                }
            }
            Destroy(gameObject);
        }

        private static void Return(Inventory from, Inventory to)
        {
            for (int slot = 0; slot < from.SlotCount; slot++)
            {
                from.MoveTo(slot, to);
            }
        }
    }
}
