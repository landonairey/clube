using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The groom's offer at a horse-hire stand (GL728): every other stand of the map, how far it is, and a button to ride
    /// there for <see cref="HorseHire.Fare"/> coins (greyed while the purse is short). Frees the cursor while open; E,
    /// Escape, Close or walking away shuts it. Added to the player at runtime by the first stand used.
    /// </summary>
    public class HorseHirePanel : MonoBehaviour
    {
        private const float Padding = 12f;
        private const float Row = 34f;
        private const float CloseDistance = 6f;

        private MapHorseHire hire;
        private int here = -1;
        private int openedFrame;
        private PlayerController player;
        private PlayerWallet wallet;
        private Vector2 scroll;

        public bool IsOpen => hire != null;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            wallet = GetComponent<PlayerWallet>();
        }

        private void OnDisable()
        {
            Close();
        }

        public void Open(MapHorseHire stands, int stand)
        {
            hire = stands;
            here = stand;
            openedFrame = Time.frameCount;
            scroll = Vector2.zero;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        public void Close()
        {
            if (hire == null)
            {
                return;
            }
            hire = null;
            here = -1;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void Update()
        {
            if (hire == null)
            {
                return;
            }
            Keyboard keys = Keyboard.current;
            bool closePressed = Time.frameCount != openedFrame && keys != null && (keys.eKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame);
            bool tooFar = here < 0 || here >= hire.Stands.Count || !Near();
            if (closePressed || tooFar || (player != null && !player.enabled))
            {
                Close();
            }
        }

        private bool Near()
        {
            Vector2 post = hire.Stands[here].Post;
            Vector3 local = hire.Session.WorldView.transform.InverseTransformPoint(transform.position);
            return (new Vector2(local.x, local.z) - post).sqrMagnitude <= CloseDistance * CloseDistance;
        }

        private void OnGUI()
        {
            GameUi.Apply();
            if (hire == null)
            {
                return;
            }
            int others = Mathf.Max(1, hire.Stands.Count - 1);
            float width = 560f;
            float height = Padding * 2f + 34f + Mathf.Min(others, 8) * Row + 44f;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            InventoryGridGui.Panel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            float coins = wallet != null && wallet.Wallet != null ? wallet.Wallet.Coins : 0f;
            GUI.Label(new Rect(x, y, width - Padding * 2f, 28f),
                $"Horses from {hire.NameOf(here)}: {Wallet.Format(HorseHire.Fare)} coins a ride   (you have {Wallet.Format(coins)})");
            y += 34f;
            var list = new Rect(x, y, width - Padding * 2f, Mathf.Min(others, 8) * Row);
            scroll = GUI.BeginScrollView(list, scroll, new Rect(0f, 0f, list.width - 20f, others * Row));
            float rowY = 0f;
            bool anywhere = false;
            for (int i = 0; i < hire.Stands.Count; i++)
            {
                if (i == here)
                {
                    continue;
                }
                anywhere = true;
                GUI.Label(new Rect(0f, rowY + 3f, list.width - 160f, 28f), $"{hire.NameOf(i)}   {hire.Distance(here, i) / 1000f:0.0} km");
                bool enabled = GUI.enabled;
                GUI.enabled = HorseHire.CanPay(coins);
                if (GameUi.Button(new Rect(list.width - 150f, rowY, 120f, 28f), "Ride there"))
                {
                    int to = i;
                    if (hire.Ride(gameObject, wallet != null ? wallet.Wallet : null, to))
                    {
                        string name = hire.NameOf(to);
                        Close();
                        ToolHint hint = FindAnyObjectByType<ToolHint>();
                        if (hint != null)
                        {
                            hint.Show($"You ride to {name}");
                        }
                        GUI.enabled = enabled;
                        GUI.EndScrollView();
                        return;
                    }
                }
                GUI.enabled = enabled;
                rowY += Row;
            }
            if (!anywhere)
            {
                GUI.Label(new Rect(0f, 3f, list.width, 28f), "No other city keeps horses.");
            }
            GUI.EndScrollView();
            if (GameUi.Button(new Rect(panel.xMax - Padding - 110f, panel.yMax - Padding - 30f, 110f, 28f), "Close"))
            {
                Close();
            }
        }
    }
}
