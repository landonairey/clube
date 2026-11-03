using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The bucket in the hand (GL319). With a bucket selected on the hotbar, Attack scoops the water aimed at, as much as
    /// the bucket has room for, out of the voxel data; a press of Place pours <see cref="BucketDefinition.PourAmount"/>
    /// units where the view meets the ground (or the water), and holding Place keeps pouring until the bucket is empty.
    /// The water taken or poured goes through <see cref="WaterScoop"/> and wakes the flow, so a poured puddle runs off
    /// downhill and a scooped lake fills back from its source.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerInventory), typeof(Hotbar))]
    public sealed class PlayerBucket : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Scoops (Attack).")]
        [SerializeField]
        private InputActionReference scoopAction;

        [Tooltip("Pours (Place): a press pours a little, holding pours on.")]
        [SerializeField]
        private InputActionReference pourAction;

        [Tooltip("How far from the head the bucket reaches, in metres.")]
        [SerializeField, Min(0.5f)]
        private float reach = 4f;

        [Tooltip("Seconds Place must be held before it pours on.")]
        [SerializeField, Min(0f)]
        private float holdDelay = 0.3f;

        private PlayerController player;
        private PlayerInventory inventory;
        private Hotbar hotbar;
        private WaterView water;
        private readonly List<Vector3Int> changed = new List<Vector3Int>();
        private float pourHeld;
        private float pourCarry;

        // How far across a pour spreads at once, in metres; the flow carries it on from there.
        private const float PourReach = 3f;

        /// <summary>Raised with the units after a scoop (positive) or a pour (negative).</summary>
        public event System.Action<int> Moved;

        /// <summary>True while a bucket is selected: the hands are holding it, not a tool.</summary>
        public bool Holding => hotbar != null && hotbar.SelectedStack.Item is BucketDefinition;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
            if (worldView == null)
            {
                worldView = FindAnyObjectByType<WorldView>();
            }
        }

        private void OnEnable()
        {
            scoopAction?.action.Enable();
            pourAction?.action.Enable();
        }

        private void Update()
        {
            if (!Holding || !player.enabled || player.IsCursorFree || worldView == null || worldView.World == null
                || inventory.Inventory == null)
            {
                pourHeld = 0f;
                return;
            }
            water ??= worldView.GetComponent<WaterView>();
            var bucket = (BucketDefinition)hotbar.SelectedStack.Item;
            if (scoopAction != null && scoopAction.action.WasPressedThisFrame())
            {
                Scoop(bucket);
            }
            if (pourAction == null)
            {
                return;
            }
            if (pourAction.action.WasPressedThisFrame())
            {
                pourHeld = 0f;
                pourCarry = 0f;
                Pour(bucket, bucket.PourAmount);
            }
            else if (pourAction.action.IsPressed())
            {
                pourHeld += Time.deltaTime;
                if (pourHeld >= holdDelay)
                {
                    pourCarry += bucket.PourRate * Time.deltaTime;
                    int units = Mathf.FloorToInt(pourCarry);
                    if (units > 0)
                    {
                        pourCarry -= units;
                        Pour(bucket, units);
                    }
                }
            }
        }

        private void Scoop(BucketDefinition bucket)
        {
            ItemStack stack = hotbar.SelectedStack;
            int room = bucket.Capacity - bucket.WaterIn(stack);
            if (room <= 0 || !Aim(out Vector3 waterAt, out _, true))
            {
                return;
            }
            changed.Clear();
            int taken = WaterScoop.Take(worldView.World, waterAt, bucket.ScoopRadius, room, changed);
            if (taken <= 0)
            {
                return;
            }
            Store(bucket, stack, bucket.WaterIn(stack) + taken);
            Wake();
            Moved?.Invoke(taken);
        }

        private void Pour(BucketDefinition bucket, int units)
        {
            ItemStack stack = hotbar.SelectedStack;
            units = Mathf.Min(units, bucket.WaterIn(stack));
            if (units <= 0 || !Aim(out _, out Vector3 pourAt, false))
            {
                return;
            }
            changed.Clear();
            int poured = WaterScoop.Pour(worldView.World, pourAt, PourReach, units, worldView.Config.IsoLevel, changed);
            if (poured <= 0)
            {
                return;
            }
            Store(bucket, stack, bucket.WaterIn(stack) - poured);
            Wake();
            Moved?.Invoke(-poured);
        }

        private void Store(BucketDefinition bucket, ItemStack stack, int units)
        {
            inventory.Inventory.Set(hotbar.SelectedSlot, bucket.WithWater(stack, units));
        }

        private void Wake()
        {
            LiveWater live = water != null ? water.Live : null;
            if (live == null)
            {
                return;
            }
            foreach (Vector3Int sample in changed)
            {
                live.Wake(sample);
            }
        }

        // Walks the view ray in short steps: where it first enters water (for a scoop), and the open point just before it
        // meets the ground or water (for a pour; the end of reach when it meets nothing). False for a scoop with no water
        // in reach.
        private bool Aim(out Vector3 waterAt, out Vector3 pourAt, bool needWater)
        {
            Transform head = player.Head != null ? player.Head : transform;
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            Vector3 origin = head.position, direction = head.forward;
            float step = world.Grid.VoxelSize * 0.4f;
            waterAt = pourAt = origin + direction * reach;
            Vector3 last = origin;
            for (float t = 0.3f; t <= reach; t += step)
            {
                Vector3 point = origin + direction * t;
                Vector3 local = worldView.transform.InverseTransformPoint(point);
                if (WaterQuery.IsUnderwater(world, local))
                {
                    waterAt = local;
                    pourAt = worldView.transform.InverseTransformPoint(last);
                    return true;
                }
                float? density = world.GetDensity(world.Grid.WorldToNearestSample(local));
                if (density.HasValue && density.Value >= iso)
                {
                    pourAt = worldView.transform.InverseTransformPoint(last);
                    return !needWater;
                }
                last = point;
            }
            pourAt = worldView.transform.InverseTransformPoint(pourAt);
            return !needWater;
        }
    }
}
