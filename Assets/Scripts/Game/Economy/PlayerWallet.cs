using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The player's coins (GL12, GL14): a <see cref="Wallet"/>, shown as a coin count above
    /// the right end of the hotbar while the player is walking.
    /// </summary>
    public class PlayerWallet : MonoBehaviour
    {
        [Tooltip("Coins the player starts with.")]
        [SerializeField, Min(0)]
        private int startingCoins;

        private PlayerController player;
        private GUIStyle style;

        public Wallet Wallet { get; private set; }

        private void Awake()
        {
            Wallet = new Wallet(startingCoins);
            player = GetComponent<PlayerController>();
        }

        private void OnGUI()
        {
            if (player != null && !player.enabled)
            {
                return;
            }
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 18, fontStyle = FontStyle.Bold };
            // Above the right end of the 9-slot hotbar (56 px slots, 4 px gaps).
            float hotbarRight = Screen.width * 0.5f + (9 * 56f + 8 * 4f) * 0.5f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(hotbarRight - 200f, Screen.height - 56f - 12f - 26f, 200f, 24f), $"{Wallet.Coins} coins", style);
            GUI.color = previous;
        }
    }
}
