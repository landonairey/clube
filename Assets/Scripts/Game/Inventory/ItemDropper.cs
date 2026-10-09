using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Drops what the selected hotbar slot holds back into the ground (first pass of I4/I6):
    /// tapping Drop (G) drops one; holding it pours (GL27), faster the longer it's held, onto the
    /// same pile, so the mound grows with the amount and any part of a stack can go. Each item becomes one
    /// sample of its material (<see cref="MaterialRegistry.PlacedFrom"/>, e.g. loose stone),
    /// piled where the player is looking (<see cref="TerrainPile"/>), or at their feet when
    /// nothing is in reach; a player the pile would bury is lifted onto it rather than left
    /// inside. Items no material drops (tools, buns, ingots) can't be dropped yet:
    /// they need world pickups (I2).
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(Hotbar), typeof(PlayerController))]
    public class ItemDropper : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Tap to drop one, hold to pour (G).")]
        [SerializeField]
        private InputActionReference dropAction;

        [Tooltip("How far from the head a drop can be aimed, in metres; beyond it, items drop at the feet.")]
        [SerializeField, Min(0f)]
        private float reach = 5f;

        [Tooltip("Seconds Drop must be held before it starts pouring.")]
        [SerializeField, Range(0.1f, 2f)]
        private float holdSeconds = 0.3f;

        [Tooltip("Items per second a pour starts at; it doubles every second it's held.")]
        [SerializeField, Min(1f)]
        private float pourRate = 8f;

        [Tooltip("Fastest a pour gets, in items per second.")]
        [SerializeField, Min(1f)]
        private float maxPourRate = 256f;

        private PlayerInventory inventory;
        private Hotbar hotbar;
        private PlayerController player;
        private float pressedAt;
        private float owed;
        private PilePour pour;

        /// <summary>Raised after items are dropped, with how many.</summary>
        public event Action<ItemDefinition, int> Dropped;

        /// <summary>Raised when the selected item can't be dropped.</summary>
        public event Action<ItemDefinition> Refused;

        /// <summary>Raised after a drop, with the material it put down and the global samples it filled (clay drying, GL34).</summary>
        public event Action<VoxelMaterial, IReadOnlyList<Vector3Int>> Piled;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            dropAction?.action.Enable();
        }

        private void Update()
        {
            if (dropAction == null || !player.enabled || player.IsCursorFree || worldView == null || !worldView.IsReady)
            {
                return;
            }

            InputAction action = dropAction.action;
            if (action.WasPressedThisFrame())
            {
                // A new pour: one item now, more while it's held, all on this pile.
                pressedAt = Time.time;
                owed = 0f;
                pour = default;
                Drop(1, ref pour);
            }
            else if (action.IsPressed() && Time.time - pressedAt >= holdSeconds)
            {
                float held = Time.time - pressedAt - holdSeconds;
                owed += Mathf.Min(maxPourRate, pourRate * Mathf.Pow(2f, held)) * Time.deltaTime;
                int batch = Mathf.FloorToInt(owed);
                if (batch > 0)
                {
                    owed -= batch;
                    Drop(batch, ref pour);
                }
            }
        }

        /// <summary>Drops up to <paramref name="count"/> of the selected hotbar item into the ground as one pile; returns how many went.</summary>
        public int DropSelected(int count)
        {
            var single = default(PilePour);
            return Drop(count, ref single);
        }

        /// <summary>
        /// Drops one batch of a pour (GL27): up to <paramref name="count"/> of the selected item,
        /// onto the pile <paramref name="into"/> began (a new one when it's <c>default</c>), which
        /// stays where it began wherever the player looks after. Returns how many went.
        /// </summary>
        public int Drop(int count, ref PilePour into)
        {
            int slot = hotbar.Selected;
            ItemStack stack = inventory.Inventory[slot];
            if (stack.IsEmpty)
            {
                return 0;
            }

            MaterialRegistry materials = worldView.Config.Materials;
            VoxelMaterial material = materials != null ? materials.PlacedFrom(stack.Item) : null;
            if (material == null)
            {
                Refused?.Invoke(stack.Item);
                return 0;
            }

            Vector3 point = into.Point;
            if (!into.Started)
            {
                AimPoint(out point);
            }
            PileSettings pile = PileSettings.For(material, new BuildGrid(worldView.Config.BuildCellSize));
            int placed = TerrainPile.Pour(worldView.World, ref into, point, material.Id, Mathf.Min(count, stack.Count), worldView.Config.IsoLevel, pile);
            if (placed == 0)
            {
                return 0;
            }
            inventory.Inventory.RemoveAt(slot, placed);
            Dropped?.Invoke(stack.Item, placed);
            Piled?.Invoke(material, TerrainPile.LastFilled);
            // A pile at the feet, or a big one spreading under them, can bury the player.
            LiftOutOfGround();
            return placed;
        }

        // Where to pile, relative to the world origin: half a voxel in front of the surface
        // the player is looking at, or their feet when nothing is within reach. False for the feet.
        private bool AimPoint(out Vector3 point)
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (worldView.Raycast(new Ray(head.position, head.forward), out WorldHit hit))
            {
                Vector3 hitWorld = worldView.transform.TransformPoint(hit.Point);
                if ((hitWorld - head.position).sqrMagnitude <= reach * reach)
                {
                    point = worldView.transform.InverseTransformPoint(hitWorld - head.forward * worldView.VoxelSize * 0.5f);
                    return true;
                }
            }
            point = worldView.transform.InverseTransformPoint(transform.position);
            return false;
        }

        // A pile at the feet can bury them: step up until the capsule's footprint is clear.
        private void LiftOutOfGround()
        {
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            Vector3 local = worldView.transform.InverseTransformPoint(transform.position);
            Vector3Int feet = world.Grid.WorldToNearestSample(local);
            int height = Mathf.CeilToInt(player.Body.height / world.Grid.VoxelSize);
            // The sample nearest the feet may be the ground itself; the capsule starts above it.
            int start = feet.y + 1;
            int y = start;
            for (int step = 0; step < 64 && Blocked(world, feet.x, y, feet.z, height, iso); step++)
            {
                y++;
            }
            if (y != start)
            {
                Vector3 lifted = world.Grid.SampleToWorld(new Vector3Int(feet.x, y, feet.z));
                lifted.x = local.x;
                lifted.z = local.z;
                player.Teleport(worldView.transform.TransformPoint(lifted) + Vector3.up * 0.05f);
            }
        }

        // Any solid sample in the 3x3 columns under the capsule, from y up its height.
        private static bool Blocked(World world, int x, int y, int z, int height, float iso)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = 0; dy < height; dy++)
                    {
                        float? density = world.GetDensity(new Vector3Int(x + dx, y + dy, z + dz));
                        if (density.HasValue && density.Value >= iso)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
