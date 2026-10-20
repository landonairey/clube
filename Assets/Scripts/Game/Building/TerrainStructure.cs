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
    /// takes its samples back out of the terrain, so nothing is gained either way.
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

        private readonly List<Vector3Int> stamped = new List<Vector3Int>();
        private WorldView worldView;
        private float nextCheck;

        /// <summary>The samples it wrote into the terrain.</summary>
        public IReadOnlyList<Vector3Int> Stamped => stamped;

        public bool IsStanding { get; private set; }

        private void Start()
        {
            worldView = FindFirstObjectByType<WorldView>();
            Stamp();
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
            Vector3Int origin = world.Grid.WorldToNearestSample(local);
            foreach (Part part in parts)
            {
                Vector3Int sample = Settle(world, origin + part.Offset, iso);
                float? density = world.GetDensity(sample);
                if (part.Material == null || !density.HasValue || density.Value >= iso)
                {
                    continue;
                }
                world.SetDensity(sample, sampleDensity);
                world.SetMaterial(sample, part.Material.Id);
                stamped.Add(sample);
            }
            IsStanding = stamped.Count > 0;
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
