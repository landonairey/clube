using Clube.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Clube.Game
{
    /// <summary>
    /// The player's coins (GL12, GL14): a <see cref="Wallet"/>, shown as a coin count above
    /// the right end of the hotbar while the player is walking. Drawn on the HUD canvas (GL777).
    /// </summary>
    public class PlayerWallet : MonoBehaviour
    {
        [Tooltip("Coins the player starts with.")]
        [SerializeField, Min(0)]
        private int startingCoins;

        private PlayerController player;
        private Text text;
        private float shownCoins = -1f;

        public Wallet Wallet { get; private set; }

        private void Awake()
        {
            // Plus the tutorial's wages (GL514), carried over from its scene.
            Wallet = new Wallet(startingCoins + GameSession.TakeCarriedCoins());
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            HudCanvas.Resized += Layout;
        }

        private void OnDisable()
        {
            HudCanvas.Resized -= Layout;
        }

        private void OnDestroy()
        {
            if (text != null)
            {
                Destroy(text.gameObject);
            }
        }

        private void LateUpdate()
        {
            bool visible = player == null || player.enabled;
            if (text == null)
            {
                text = HudCanvas.Label(HudCanvas.Root, "Coins", GameUi.Font(18), FontStyle.Bold, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f));
                Layout();
            }
            HudCanvas.SetShown(text.gameObject, visible);
            if (visible && shownCoins != Wallet.Coins)
            {
                shownCoins = Wallet.Coins;
                text.text = $"{Wallet.Format(Wallet.Coins)} coins";
            }
        }

        private void Layout()
        {
            if (text == null)
            {
                return;
            }
            // Above the right end of the 9-slot hotbar.
            float hotbarRight = Screen.width * 0.5f + (9 * GameUi.SlotSize + 8 * GameUi.SlotGap) * 0.5f;
            HudCanvas.Place(text.rectTransform, hotbarRight - 260f, Screen.height - GameUi.SlotSize - 12f - 34f, 260f, 32f);
        }
    }
}
