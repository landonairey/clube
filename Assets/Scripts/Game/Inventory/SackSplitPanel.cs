using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The box that opens on picking up a merged pile of sacks (GL837): how many of the pile's sacks to take, and a warning
    /// when they won't all fit in the inventory (what doesn't fit stays in the pile). On the player, added by
    /// <see cref="DroppedItems"/>; a player screen, so it sets <see cref="PlayerController.IsInMenu"/>.
    /// </summary>
    public class SackSplitPanel : MonoBehaviour, IGuiDrawer
    {
        private const float Width = 460f;
        private const float Height = 300f;

        private PlayerInventory inventory;
        private PlayerController player;
        private DroppedItem pile;
        private DroppedItems owner;
        private int amount;
        private int openedFrame;

        public bool IsOpen => pile != null;

        int IGuiDrawer.GuiDepth => 0;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            owner = GetComponent<DroppedItems>();
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            Close();
        }

        public void Open(DroppedItem sacks)
        {
            pile = sacks;
            amount = sacks.Sacks;
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        public void Close()
        {
            if (pile == null)
            {
                return;
            }
            pile = null;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        // How many of the first `sacks` sacks would go in the pack: tried on a scratch copy of it, whole sacks at a time.
        private int SacksThatFit(int sacks)
        {
            ItemStack one = pile.Stack;
            var scratch = new Inventory(inventory.Inventory.SlotCount);
            for (int i = 0; i < scratch.SlotCount; i++)
            {
                ItemStack held = inventory.Inventory[i];
                if (!held.IsEmpty)
                {
                    scratch.Add(held.Item, held.Count, held.Contents, held.Wear, held.Quality, held.Part);
                }
            }
            int fit = 0;
            for (int i = 0; i < sacks; i++)
            {
                if (scratch.Add(one.Item, one.Count, one.Contents, one.Wear, one.Quality, one.Part) > 0)
                {
                    break;
                }
                fit++;
            }
            return fit;
        }

        void IGuiDrawer.DrawGui()
        {
            if (pile == null)
            {
                return;
            }
            if (pile.Sacks < 1 || (pile.transform.position - transform.position).sqrMagnitude > 36f)
            {
                Close();
                return;
            }
            GameUi.Apply();
            amount = Mathf.Clamp(amount, 1, pile.Sacks);
            Keyboard keys = Keyboard.current;
            if (Time.frameCount > openedFrame && keys != null && (keys.escapeKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame))
            {
                Close();
                return;
            }

            var box = new Rect((Screen.width - Width) * 0.5f, (Screen.height - Height) * 0.5f, Width, Height);
            InventoryGridGui.Panel(box);
            float x = box.x + 20f, y = box.y + 14f, w = box.width - 40f;
            var title = new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(18), fontStyle = FontStyle.Bold };
            var label = new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(14), wordWrap = true };
            string what = pile.Stack.Count > 1 ? $"{pile.Stack.Item.DisplayName} x{pile.Stack.Count}" : pile.Stack.Item.DisplayName;
            GUI.Label(new Rect(x, y, w, 34f), $"{pile.Sacks} sacks of {what}", title);
            GUI.Label(new Rect(x, y + 40f, w, 30f), $"Take {amount} of {pile.Sacks} sacks ({amount * pile.Stack.Count} items)", label);
            amount = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x, y + 80f, w, 24f), amount, 1, pile.Sacks));
            float bx = x;
            foreach ((string text, int step) in new[] { ("-10", -10), ("-1", -1), ("+1", 1), ("+10", 10), ("All", int.MaxValue) })
            {
                if (GameUi.Button(new Rect(bx, y + 110f, 76f, 34f), text))
                {
                    amount = step == int.MaxValue ? pile.Sacks : Mathf.Clamp(amount + step, 1, pile.Sacks);
                }
                bx += 84f;
            }
            int fit = SacksThatFit(amount);
            if (fit < amount)
            {
                var warn = new GUIStyle(label);
                warn.normal.textColor = new Color(1f, 0.55f, 0.3f);
                GUI.Label(new Rect(x, y + 150f, w, 60f), fit == 0
                    ? "Warning: this won't fit in your inventory at all."
                    : $"Warning: only {fit} of these {amount} sacks fit in your inventory; the rest stay on the ground.", warn);
            }
            if (GameUi.Button(new Rect(x, box.yMax - 62f, 200f, 40f), "Take"))
            {
                int taken = owner != null ? owner.TakeSacks(pile, amount, inventory) : 0;
                if (taken < amount)
                {
                    inventory.NotifyFull(pile != null ? pile.Stack.Item : null);
                }
                Close();
            }
            if (GameUi.Button(new Rect(box.xMax - 220f, box.yMax - 62f, 200f, 40f), "Cancel"))
            {
                Close();
            }
        }
    }
}
