using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The small screen of an opened <see cref="Clam"/> (GL735): its one slot holds a pearl, a black pearl or nothing. Click
    /// the pearl to take it. Closing (Close, E, Escape, or walking away) makes the clam disappear, pearl or not.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class ClamPanel : MonoBehaviour, IGuiDrawer
    {
        private const float CloseDistance = 5f;

        private PlayerInventory inventory;
        private PlayerController player;
        private ItemCatalog catalog;
        private Clam clam;
        private ClamFind find;
        private int openedFrame;
        private string message = "";

        public bool IsOpen => clam != null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            Close();
        }

        public void Open(Clam which, ClamFind contents)
        {
            clam = which;
            find = contents;
            message = "";
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        public void Close()
        {
            if (clam == null)
            {
                return;
            }
            Clam done = clam;
            clam = null;
            if (player != null)
            {
                player.IsInMenu = false;
            }
            done.Finish();
        }

        private void Update()
        {
            if (clam == null)
            {
                return;
            }
            Keyboard keys = Keyboard.current;
            bool pressed = keys != null && (keys.eKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame);
            if ((pressed && Time.frameCount != openedFrame) || (clam.transform.position - transform.position).sqrMagnitude > CloseDistance * CloseDistance)
            {
                Close();
            }
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            if (clam == null)
            {
                return;
            }
            var box = new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f - 90f, 260f, 180f);
            GUI.Box(box, "Clam");
            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 28f, box.width - 24f, box.height - 40f));
            string id = ClamPearls.IdOf(find);
            if (id == null)
            {
                GUILayout.Label("The clam is empty.");
            }
            else
            {
                ItemDefinition item = Item(id);
                string name = item != null ? item.DisplayName : id;
                if (GUILayout.Button(name, GUILayout.Height(48f)))
                {
                    Take(item);
                }
            }
            if (message.Length > 0)
            {
                GUILayout.Label(message);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (E)"))
            {
                Close();
            }
            GUILayout.EndArea();
        }

        private void Take(ItemDefinition item)
        {
            if (item == null)
            {
                message = "No such item in the game.";
                return;
            }
            if (inventory.Inventory.Add(item, 1) == 0)
            {
                find = ClamFind.Empty;
                message = "Took the " + item.DisplayName.ToLowerInvariant() + ".";
            }
            else
            {
                message = "Your pack is full.";
            }
        }

        private ItemDefinition Item(string id)
        {
            if (catalog == null)
            {
                foreach (ItemCatalog candidate in Resources.FindObjectsOfTypeAll<ItemCatalog>())
                {
                    if (candidate.Find(id) != null)
                    {
                        catalog = candidate;
                        break;
                    }
                }
            }
            return catalog != null ? catalog.Find(id) : null;
        }
    }
}
