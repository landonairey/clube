using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// An item lying in the world where the player dropped it (GL610, GL611): a tool as its own model, or a canvas bag
    /// holding a stack. A rigid body, so it falls, tumbles and can roll; E picks it up (<see cref="PlayerInteractor"/>),
    /// and what doesn't fit in the inventory stays. Made and kept by <see cref="DroppedItems"/>.
    /// </summary>
    public class DroppedItem : MonoBehaviour, IInteractable
    {
        /// <summary>What it holds: one tool with its wear and quality, or a bag's stack.</summary>
        public ItemStack Stack { get; set; }

        /// <summary>True for a canvas bag (GL611), which more of the same stack can be poured into.</summary>
        public bool IsBag { get; private set; }

        /// <summary>How many identical sacks this one stands for (GL837: a pile of over 10 merges into one); 1 for a single sack.</summary>
        public int Sacks { get; set; } = 1;

        public Rigidbody Body { get; private set; }

        /// <summary>Where it last lay still: where it's put back if it ever falls out of the world.</summary>
        public Vector3 RestPosition { get; set; }

        /// <summary>Held still, off the physics, while the ground under it isn't there (far from the player, or loading).</summary>
        public bool Frozen
        {
            get => Body.isKinematic;
            set => Body.isKinematic = value;
        }

        private DroppedItems owner;

        public string Prompt => Sacks > 1 ? $"Pick up {Sacks} sacks of {Stack.Item.DisplayName}" : Stack.Count > 1 ? $"Pick up {Stack.Item.DisplayName} x{Stack.Count}" : Stack.Count == 0 ? $"Pick up {Stack.Item.DisplayName} x{Stack.Part:0.##}" : $"Pick up {Stack.Item.DisplayName}";

        public void Set(DroppedItems owner, ItemStack stack, bool isBag, Rigidbody body)
        {
            this.owner = owner;
            Stack = stack;
            IsBag = isBag;
            Body = body;
            RestPosition = transform.position;
        }

        /// <summary>More of the same stack into this bag (a pour, GL611).</summary>
        public void Add(int count)
        {
            Stack = Stack.WithCount(Stack.Count + count);
        }

        /// <summary>More of the same stack into this bag by amount, a fraction included (GL708: pouring the last 0.4 of a stack).</summary>
        public void AddAmount(float amount)
        {
            float total = Stack.Amount + amount;
            int whole = Mathf.FloorToInt(total + ItemStack.PartEpsilon);
            Stack = new ItemStack(Stack.Item, whole, Stack.Contents, Stack.Wear, Stack.Quality, Mathf.Max(0f, total - whole));
        }

        public void Interact(PlayerInteractor by)
        {
            PlayerInventory inventory = by.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }
            int left = inventory.Inventory.Add(Stack.Item, Stack.Count, Stack.Contents, Stack.Wear, Stack.Quality, Stack.Part);
            int taken = Stack.Count - left;
            if (taken > 0)
            {
                inventory.Notify(Stack.Item, taken);
            }
            if (left > 0)
            {
                inventory.NotifyFull(Stack.Item);
                Stack = new ItemStack(Stack.Item, left, Stack.Contents, Stack.Wear, Stack.Quality, Stack.Part);
                return;
            }
            owner.Remove(this);
        }
    }
}
