using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The screen for trading at a <see cref="MerchantTable"/> (GL14, first pass of TR4):
    /// every item the merchant buys, its price, how many the player has, and buttons to sell
    /// one or all of them; the player's coins at the bottom. Frees the cursor while open;
    /// Interact (E) closes it, as does walking away.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerWallet))]
    public class MerchantPanel : MonoBehaviour
    {
        private const float Width = 420f;
        private const float RowHeight = 26f;

        [Tooltip("Closes the panel (E).")]
        [SerializeField]
        private InputActionReference closeAction;

        [Tooltip("The panel closes when the player is this far from the table, in metres.")]
        [SerializeField, Min(1f)]
        private float closeDistance = 5f;

        private PlayerInventory inventory;
        private PlayerWallet wallet;
        private PlayerController player;
        private int openedFrame;

        /// <summary>The table being used, or null when closed.</summary>
        public MerchantTable Current { get; private set; }

        public bool IsOpen => Current != null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            wallet = GetComponent<PlayerWallet>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            closeAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
        }

        public void Open(MerchantTable table)
        {
            Current = table;
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsCursorFree = true;
            }
        }

        public void Close()
        {
            if (Current == null)
            {
                return;
            }
            Current = null;
            if (player != null)
            {
                player.IsCursorFree = false;
            }
        }

        /// <summary>Sells up to <paramref name="count"/> of an item to the open table; returns how many sold.</summary>
        public int Sell(ItemDefinition item, int count)
        {
            if (Current == null || Current.Prices == null)
            {
                return 0;
            }
            return Current.Prices.Sell(item, count, inventory.Inventory, wallet.Wallet);
        }

        private void Update()
        {
            if (Current == null)
            {
                return;
            }
            // The press that opened the panel mustn't close it in the same frame.
            bool closePressed = Time.frameCount != openedFrame && closeAction != null && closeAction.action.WasPressedThisFrame();
            bool tooFar = (Current.transform.position - transform.position).sqrMagnitude > closeDistance * closeDistance;
            if (closePressed || tooFar || (player != null && !player.enabled))
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (Current == null || Current.Prices == null)
            {
                return;
            }

            Inventory items = inventory.Inventory;
            int rows = Current.Prices.Prices.Count;
            float height = 100f + RowHeight * rows;
            var area = new Rect((Screen.width - Width) * 0.5f, (Screen.height - height) * 0.5f, Width, height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
            GUILayout.Label($"{Current.DisplayName} buys   (E to close)");
            GUILayout.Space(4f);

            foreach (ItemPrice price in Current.Prices.Prices)
            {
                if (price.Item == null)
                {
                    continue;
                }
                int have = items.Count(price.Item);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{price.Item.DisplayName}: {price.Coins} each (you have {have})", GUILayout.Width(250f));
                GUI.enabled = have > 0;
                if (GUILayout.Button("Sell 1", GUILayout.Height(RowHeight)))
                {
                    Sell(price.Item, 1);
                }
                if (GUILayout.Button($"Sell all", GUILayout.Height(RowHeight)))
                {
                    Sell(price.Item, have);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Your coins: {wallet.Wallet.Coins}");
            GUILayout.EndArea();
        }
    }
}
