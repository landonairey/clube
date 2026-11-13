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
    /// inside. What an item drops as is its definition's (<see cref="ItemDefinition.DropsAs"/>): a tool is thrown
    /// down as its model (GL610), large equipment and anything no material is placed from (buns, ingots, hides) as a
    /// canvas bag (GL611), which holding Drop pours more of the stack into (<see cref="DroppedItems"/>).
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
        private PlayerParcels parcels;
        private float pressedAt;
        private float owed;
        private PilePour pour;
        private DroppedItems dropped;
        // The bag this press of Drop began (GL611): holding the key pours more of the stack into it.
        private DroppedItem pourBag;

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
            parcels = GetComponent<PlayerParcels>();
            // Added here, not in the scenes: every player that can drop keeps (and saves) what it dropped.
            dropped = GetComponent<DroppedItems>();
            if (dropped == null)
            {
                dropped = gameObject.AddComponent<DroppedItems>();
            }
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
                pourBag = null;
                Drop(1, ref pour, exact: true);
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
        /// <param name="exact">A tap (GL394): one item goes exactly on the aimed sample when there's one in reach, not onto a pile.</param>
        public int Drop(int count, ref PilePour into, bool exact = false)
        {
            int slot = hotbar.SelectedSlot;
            if (slot < 0)
            {
                return 0;
            }
            ItemStack stack = inventory.Inventory[slot];
            if (stack.IsEmpty)
            {
                return 0;
            }

            MaterialRegistry materials = worldView.Config.Materials;
            VoxelMaterial material = materials != null ? materials.PlacedFrom(stack.Item) : null;
            DropForm form = stack.Item.DropsAs;
            if (form != DropForm.Pile || material == null)
            {
                // Not into the ground: thrown down as its model (GL610), or in a bag (GL611).
                return DropLoose(slot, stack, count, bagged: form != DropForm.Model, tap: exact);
            }

            Vector3 point = into.Point;
            if (!into.Started)
            {
                AimPoint(out point);
            }
            // Nothing goes onto locked land (GL42).
            if (parcels != null && !parcels.CanEdit(worldView.transform.TransformPoint(point)))
            {
                Refused?.Invoke(stack.Item);
                return 0;
            }
            var grid = new BuildGrid(worldView.Config.BuildCellSize);
            int wanted = Mathf.Min(count, stack.Count);
            int placed;
            if (material.BuildsSquare)
            {
                // Boards go down one square voxel at a time (GL411), never as a rounded pile.
                placed = exact && !into.Started && wanted >= 1 && PlaceBlockAtAim(material) ? 1 : 0;
            }
            else if (exact && !into.Started && wanted >= 1 && PlaceAtAim(stack, material, materials))
            {
                placed = 1;
            }
            else if (IsMixed(stack, material))
            {
                placed = PourMixed(stack.Contents, materials, material, grid, ref into, point, wanted);
            }
            else
            {
                placed = TerrainPile.Pour(worldView.World, ref into, point, material.Id, wanted, worldView.Config.IsoLevel, PileSettings.For(material, grid));
                if (placed > 0)
                {
                    Piled?.Invoke(material, TerrainPile.LastFilled);
                }
            }
            if (placed == 0)
            {
                return 0;
            }
            inventory.Inventory.RemoveAt(slot, placed);
            Dropped?.Invoke(stack.Item, placed);
            // A pile at the feet, or a big one spreading under them, can bury the player.
            LiftOutOfGround();
            return placed;
        }

        // An item that doesn't go into the ground. A tap throws one down in front of the player: its model, tumbling
        // (GL610), or a bag (GL611). Held, the key pours more of the same stack into the bag that tap began; models
        // go one to a tap.
        private int DropLoose(int slot, ItemStack stack, int count, bool bagged, bool tap)
        {
            if (!tap)
            {
                if (pourBag == null || pourBag.Stack.Item != stack.Item)
                {
                    return 0;
                }
                int more = Mathf.Min(count, stack.Count);
                pourBag.Add(more);
                inventory.Inventory.RemoveAt(slot, more);
                Dropped?.Invoke(stack.Item, more);
                return more;
            }
            if (stack.Count < 1)
            {
                return 0;
            }
            Transform head = player.Head != null ? player.Head : transform;
            // From just in front of the chest, or short of a wall that close.
            Vector3 toward = (head.forward - head.up * 0.35f).normalized;
            float distance = Physics.Raycast(head.position, toward, out RaycastHit wall, 0.75f, ~0, QueryTriggerInteraction.Ignore) && !wall.collider.transform.IsChildOf(transform) && wall.collider.GetComponent<DroppedItem>() == null
                ? Mathf.Max(0.1f, wall.distance - 0.25f)
                : 0.6f;
            Vector3 from = head.position + toward * distance;
            Vector3 velocity = player.Body.velocity + head.forward * 2.2f + Vector3.up * 0.8f;
            var one = new ItemStack(stack.Item, 1, stack.Contents, stack.Wear, stack.Quality);
            DroppedItem item;
            if (bagged)
            {
                item = dropped.Spawn(one, true, from, Quaternion.Euler(0f, head.eulerAngles.y + 180f, 0f), velocity);
            }
            else
            {
                // Handle across the view, with a tumble.
                Quaternion turn = head.rotation * Quaternion.Euler(0f, 0f, 80f);
                item = dropped.Spawn(one, false, from, turn, velocity, head.TransformDirection(new Vector3(3f, 1f, -5f)));
            }
            if (item == null)
            {
                return 0;
            }
            pourBag = item.IsBag ? item : null;
            inventory.Inventory.RemoveAt(slot, 1);
            Dropped?.Invoke(stack.Item, 1);
            return 1;
        }

        // One sample, exactly on the aimed spot (GL394), of the stack's material (a mixed stack's picked at random by share).
        private bool PlaceAtAim(ItemStack stack, VoxelMaterial material, MaterialRegistry materials)
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (!worldView.Raycast(new Ray(head.position, head.forward), out WorldHit hit))
            {
                return false;
            }
            Vector3 hitWorld = worldView.transform.TransformPoint(hit.Point);
            if ((hitWorld - head.position).sqrMagnitude > reach * reach)
            {
                return false;
            }
            if (IsMixed(stack, material))
            {
                VoxelMaterial part = Pick(stack.Contents, UnityEngine.Random.value) ?? material;
                material = materials.PlacedFrom(part.FinalDrop) ?? part;
            }
            Vector3 toward = worldView.transform.InverseTransformDirection(-head.forward);
            if (!TerrainPile.PlaceOne(worldView.World, hit.Point, toward, material.Id, worldView.Config.IsoLevel))
            {
                return false;
            }
            Piled?.Invoke(material, TerrainPile.LastFilled);
            return true;
        }

        // A square block of a board material (GL411) in the open voxel in front of the aimed face, within reach.
        private bool PlaceBlockAtAim(VoxelMaterial material)
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (!worldView.Raycast(new Ray(head.position, head.forward), out WorldHit hit))
            {
                return false;
            }
            Vector3 hitWorld = worldView.transform.TransformPoint(hit.Point);
            if ((hitWorld - head.position).sqrMagnitude > reach * reach)
            {
                return false;
            }
            float size = worldView.World.Grid.VoxelSize;
            // Half a voxel back towards the player from the face hit: the voxel the block fills.
            Vector3 toward = worldView.transform.InverseTransformDirection(-head.forward).normalized;
            Vector3 inside = hit.Point + toward * (size * 0.5f);
            Vector3Int voxel = Vector3Int.FloorToInt(inside / size);
            return SquareVoxels.Place(worldView.World, voxel, material.Id, worldView.Config.IsoLevel);
        }

        // A gathered stack holding more than its own material (GL120: grass with some dirt). Ore keeps
        // its grade in its contents, not other ground, so it always goes down as the ore.
        private static bool IsMixed(ItemStack stack, VoxelMaterial material)
        {
            return material.Category != VoxelMaterialCategory.Ore && stack.Contents != null && stack.Contents.Parts.Count > 1;
        }

        // A mixed stack goes down as its mix: each sample a material picked at random by its share, put
        // down as that material is when dropped (mined stone as loose stone).
        private int PourMixed(Composition contents, MaterialRegistry materials, VoxelMaterial fallback, BuildGrid grid, ref PilePour into, Vector3 point, int count)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                VoxelMaterial part = Pick(contents, UnityEngine.Random.value) ?? fallback;
                VoxelMaterial material = materials.PlacedFrom(part.FinalDrop) ?? part;
                if (TerrainPile.Pour(worldView.World, ref into, point, material.Id, 1, worldView.Config.IsoLevel, PileSettings.For(material, grid)) == 0)
                {
                    break;
                }
                placed++;
                Piled?.Invoke(material, TerrainPile.LastFilled);
            }
            return placed;
        }

        // The part of the contents a roll in [0, 1) lands on, by fraction.
        private static VoxelMaterial Pick(Composition contents, float roll)
        {
            float total = 0f;
            foreach (Content part in contents.Parts)
            {
                total += part.Fraction;
            }
            float at = roll * total;
            VoxelMaterial last = null;
            foreach (Content part in contents.Parts)
            {
                last = part.Material;
                if (at < part.Fraction)
                {
                    return part.Material;
                }
                at -= part.Fraction;
            }
            return last;
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
