using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The small screen of an open <see cref="Clam"/> (GL735, GL808): its one slot holds a pearl, a black pearl or nothing.
    /// E (or clicking the pearl) closes it and takes what is inside; the clam then vanishes in bubbles. The Close button
    /// (also Escape, or walking away) shuts the lid and leaves the clam where it is, with its pearl still in it.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class ClamPanel : MonoBehaviour, IGuiDrawer
    {
        private const float CloseDistance = 5f;

        private PlayerInventory inventory;
        private PlayerController player;
        private PlayerWaterSounds water;
        private ItemCatalog catalog;
        private Clam clam;
        private int openedFrame;
        private string message = "";

        public bool IsOpen => clam != null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            player = GetComponent<PlayerController>();
            water = GetComponent<PlayerWaterSounds>();
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

        public void Open(Clam which)
        {
            clam = which;
            message = "";
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        /// <summary>The Close button: shuts the lid and keeps whatever is inside in the clam.</summary>
        public void Close()
        {
            Clam done = Release();
            if (done != null)
            {
                done.CloseKeeping();
            }
        }

        /// <summary>
        /// E: takes what is inside and closes, and the clam vanishes. An empty clam vanishes too (nothing is left to come
        /// back for). With a full pack the panel stays open and says so.
        /// </summary>
        public void TakeAndClose()
        {
            if (clam == null)
            {
                return;
            }
            string id = ClamPearls.IdOf(clam.Contents);
            if (id != null)
            {
                ItemDefinition item = Item(id);
                if (item == null)
                {
                    message = "No such item in the game.";
                    return;
                }
                if (inventory.Inventory.Add(item, 1) != 0)
                {
                    message = "Your pack is full.";
                    return;
                }
            }
            Clam done = Release();
            done.Vanish(water != null ? water.DropletMaterial : null, water != null ? water.Sounds : null);
        }

        private Clam Release()
        {
            if (clam == null)
            {
                return null;
            }
            Clam done = clam;
            clam = null;
            if (player != null)
            {
                player.IsInMenu = false;
            }
            return done;
        }

        private void Update()
        {
            if (clam == null)
            {
                return;
            }
            Keyboard keys = Keyboard.current;
            bool fresh = Time.frameCount != openedFrame;
            if (fresh && keys != null && keys.eKey.wasPressedThisFrame)
            {
                TakeAndClose();
            }
            else if ((fresh && keys != null && keys.escapeKey.wasPressedThisFrame)
                || (clam.transform.position - transform.position).sqrMagnitude > CloseDistance * CloseDistance)
            {
                Close();
            }
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            if (clam == null)
            {
                return;
            }
            var box = new Rect(Screen.width * 0.5f - 130f, Screen.height * 0.5f - 95f, 260f, 190f);
            GUI.Box(box, "Clam");
            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 28f, box.width - 24f, box.height - 40f));
            string id = ClamPearls.IdOf(clam.Contents);
            if (id == null)
            {
                GUILayout.Label("The clam is empty.");
            }
            else
            {
                ItemDefinition item = Item(id);
                var content = new GUIContent(item != null ? item.DisplayName : id, item != null ? item.Icon : null);
                if (GUILayout.Button(content, GUILayout.Height(48f)))
                {
                    TakeAndClose();
                    GUILayout.EndArea();
                    return;
                }
            }
            if (message.Length > 0)
            {
                GUILayout.Label(message);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(id != null ? "Take and close (E)" : "Done (E)"))
            {
                TakeAndClose();
            }
            else if (GUILayout.Button("Close"))
            {
                Close();
            }
            GUILayout.EndArea();
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
