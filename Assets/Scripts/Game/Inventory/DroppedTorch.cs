using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A lit torch lying where it was dropped (GL683): it keeps its flame and light, goes on burning by game time from what
    /// it had already burned (<see cref="ItemStack.Wear"/>), and is gone once it has burned out. Added to the
    /// <see cref="DroppedItem"/> by <see cref="DroppedItems"/>; picked up, it is the lit torch with the burning it has left.
    /// </summary>
    public class DroppedTorch : MonoBehaviour
    {
        private DroppedItem item;
        private DroppedItems owner;
        private TorchFlame fire;
        private FlameCluster flame;
        private GameClockTicks ticks;

        /// <summary>Sets the torch up: <paramref name="flameMaterial"/> is the held material, whose vertex alpha 0 glows.</summary>
        public void Set(DroppedItems owner, DroppedItem item, Material flameMaterial)
        {
            this.owner = owner;
            this.item = item;
            ticks = new GameClockTicks(FindAnyObjectByType<GameClock>());
            // The torch head of the held model (HeldToolMeshes.BuildTorch), the same spot as the flame in the hand.
            var head = new Vector3(0.006f, 0.405f, 0.035f);
            fire = new TorchFlame(transform, head, 14f, 4f, null);
            flame = flameMaterial != null ? new FlameCluster(transform, head, 0.085f, flameMaterial) : null;
        }

        private void Update()
        {
            if (item == null || !(item.Stack.Item is TorchDefinition torch) || !torch.IsLit)
            {
                return;
            }
            ItemStack stack = item.Stack;
            int burned = stack.Wear + ticks.WholeSeconds();
            if (burned >= torch.LifeSeconds)
            {
                owner.Remove(item);
                return;
            }
            item.Stack = new ItemStack(torch, 1, stack.Contents, burned, stack.Quality);
            float left = torch.Left(item.Stack);
            fire.Flicker(Mathf.Lerp(0.55f, 1f, left * 4f));
            flame?.Animate(Mathf.Lerp(0.55f, 1f, left * 4f));
        }
    }
}
