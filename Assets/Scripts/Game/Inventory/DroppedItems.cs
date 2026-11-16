using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The items lying dropped in the world (GL610, GL611): makes each one (its model or a bag, a convex collider and a
    /// rigid body, so it falls, tumbles and rolls as Skyrim's dropped items do), keeps them, and saves them in the
    /// save's named values. An item only moves while the player is near, where the ground has its colliders: beyond
    /// that it is held still, and one that still falls out of the world is put back where it last lay. On the player,
    /// added by <see cref="ItemDropper"/>; the models use the held tool's material (Clube/Held Item).
    /// </summary>
    public class DroppedItems : MonoBehaviour, ISavedValues
    {
        private const string SaveKey = "dropped.items";

        // Metres from the player within which a dropped item moves; the terrain's colliders reach further.
        private const float ActiveRadius = 30f;

        // How far under where it last lay an item may fall before it's put back.
        private const float LostDepth = 40f;

        // Seconds between checks that a sleeping item still has ground under it (the ground can be dug away).
        private const float GroundCheckSeconds = 0.5f;

        private readonly List<DroppedItem> items = new List<DroppedItem>();
        private readonly Dictionary<(ItemDefinition, bool), Mesh> meshes = new Dictionary<(ItemDefinition, bool), Mesh>();
        private readonly RaycastHit[] hits = new RaycastHit[6];
        private Material material;
        private CharacterController body;
        private float nextGroundCheck;

        /// <summary>The items lying in the world now.</summary>
        public IReadOnlyList<DroppedItem> Items => items;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            HeldToolView held = GetComponent<HeldToolView>();
            material = held != null ? held.Material : null;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes.Values)
            {
                Destroy(mesh);
            }
        }

        /// <summary>
        /// Puts a stack into the world at a place, moving as given. <paramref name="bagged"/>: in a canvas bag (GL611)
        /// instead of as its own model; an item with no model of its own is always bagged.
        /// </summary>
        public DroppedItem Spawn(ItemStack stack, bool bagged, Vector3 position, Quaternion rotation, Vector3 velocity = default, Vector3 spin = default, bool frozen = false)
        {
            if (stack.IsEmpty)
            {
                return null;
            }
            bool isBag = bagged || !DroppedItemMeshes.HasModel(stack.Item);
            var go = new GameObject(isBag ? $"Dropped bag ({stack.Item.DisplayName})" : $"Dropped {stack.Item.DisplayName}");
            go.transform.SetPositionAndRotation(position, rotation);
            Mesh mesh = MeshFor(stack.Item, isBag);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var hull = go.AddComponent<MeshCollider>();
            hull.convex = true;
            hull.sharedMesh = mesh;
            if (body != null)
            {
                // The player walks through what lies about, never trips on it or stands on it.
                Physics.IgnoreCollision(hull, body);
            }
            var rigid = go.AddComponent<Rigidbody>();
            rigid.mass = isBag ? 4f : 1.5f;
            rigid.angularDamping = isBag ? 2f : 0.35f;
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (isBag)
            {
                // Heavy at the bottom, so a bag lands sitting up more often than not.
                rigid.centerOfMass = new Vector3(0f, 0.07f, 0f);
            }
            rigid.isKinematic = frozen;
            if (!frozen)
            {
                rigid.linearVelocity = velocity;
                rigid.angularVelocity = spin;
            }
            var item = go.AddComponent<DroppedItem>();
            item.Set(this, stack, isBag, rigid);
            items.Add(item);
            return item;
        }

        /// <summary>Takes an item out of the world (picked up).</summary>
        public void Remove(DroppedItem item)
        {
            if (items.Remove(item) && item != null)
            {
                Destroy(item.gameObject);
            }
        }

        private Mesh MeshFor(ItemDefinition item, bool isBag)
        {
            // Every bag is the same mesh.
            (ItemDefinition, bool) key = (isBag ? null : item, isBag);
            if (!meshes.TryGetValue(key, out Mesh mesh))
            {
                mesh = DroppedItemMeshes.For(item, isBag);
                meshes.Add(key, mesh);
            }
            return mesh;
        }

        private void FixedUpdate()
        {
            bool checkGround = Time.time >= nextGroundCheck;
            if (checkGround)
            {
                nextGroundCheck = Time.time + GroundCheckSeconds;
            }
            Vector3 centre = transform.position;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                DroppedItem item = items[i];
                if (item == null)
                {
                    items.RemoveAt(i);
                    continue;
                }
                Vector3 at = item.transform.position;
                bool near = (at - centre).sqrMagnitude < ActiveRadius * ActiveRadius;
                if (item.Frozen)
                {
                    // Let go once the player is near and there is ground to land on.
                    if (near && checkGround && GroundUnder(item, 8f))
                    {
                        item.Frozen = false;
                    }
                }
                else if (!near)
                {
                    item.Frozen = true;
                }
                else if (at.y < item.RestPosition.y - LostDepth)
                {
                    // Through the ground somehow: back where it last lay, held until there's ground again.
                    item.Frozen = true;
                    item.transform.position = item.RestPosition + Vector3.up * 0.3f;
                }
                else if (item.Body.IsSleeping())
                {
                    item.RestPosition = at;
                    // The ground it sleeps on may have been dug away: a sleeping body doesn't notice by itself.
                    if (checkGround && !GroundUnder(item, 0.6f))
                    {
                        item.Body.WakeUp();
                    }
                }
            }
        }

        // Anything solid, other than a dropped item, within a distance under an item.
        private bool GroundUnder(DroppedItem item, float within)
        {
            Vector3 from = item.Body.worldCenterOfMass + Vector3.up * 0.3f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, hits, within + 0.3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.GetComponent<DroppedItem>() == null && !hits[i].collider.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
            return false;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            var saved = new List<DroppedSave>(items.Count);
            foreach (DroppedItem item in items)
            {
                if (item != null)
                {
                    saved.Add(new DroppedSave
                    {
                        Stack = StackSaving.Capture(item.Stack),
                        // One that is falling as the game is saved comes back where it last lay.
                        Position = item.Frozen || item.Body.IsSleeping() ? item.transform.position : item.RestPosition,
                        Rotation = item.transform.rotation,
                    });
                }
            }
            values[SaveKey] = DroppedItemSaving.Write(saved);
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            foreach (DroppedItem item in items)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
            items.Clear();
            if (!values.TryGetValue(SaveKey, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            GameSessionHost host = FindAnyObjectByType<GameSessionHost>();
            if (host == null || host.Catalog == null)
            {
                return;
            }
            PlayerSaveState state = GetComponent<PlayerSaveState>();
            var lookup = new ItemLookup(host.Catalog, state != null ? state.Ores : null);
            MaterialRegistry materials = host.Materials;
            foreach (DroppedSave saved in DroppedItemSaving.Read(text))
            {
                ItemStack stack = StackSaving.Restore(saved.Stack, lookup.Find, materials);
                if (!stack.IsEmpty)
                {
                    // Held still until the ground under it has loaded.
                    Spawn(stack, stack.Item.DropsAs != DropForm.Model, saved.Position, saved.Rotation, frozen: true);
                }
            }
        }
    }
}
