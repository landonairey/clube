using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A pouch of coins lying in the world (GL617): what a struck thief drops of his takings, and the coin half of a
    /// thieves' camp's stash. Using it puts the coins in the player's purse.
    /// </summary>
    public class CoinPouch : MonoBehaviour, IInteractable
    {
        private static readonly Color Leather = new Color(0.45f, 0.3f, 0.16f), Cord = new Color(0.75f, 0.62f, 0.3f);

        private Wallet coins;
        private bool goesWhenEmpty;
        private Renderer shown;

        public string Prompt => $"Take {Wallet.Format(coins != null ? coins.Coins : 0f)} coins";

        /// <summary>
        /// Makes the pouch of a purse; with <paramref name="goesWhenEmpty"/> it is gone once emptied (a dropped one),
        /// otherwise it only hides while the purse is empty (a stash's).
        /// </summary>
        public void Hold(Wallet purse, Material material, bool goesWhenEmpty)
        {
            coins = purse;
            this.goesWhenEmpty = goesWhenEmpty;
            var model = new LowPolyMeshBuilder();
            model.Blob(new Vector3(0f, 0.11f, 0f), new Vector3(0.14f, 0.12f, 0.14f), Quaternion.identity, Leather, 6, 3);
            model.Prism(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.27f, 0f), 0.05f, 0.08f, 6, Leather * 0.85f);
            model.Prism(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.215f, 0f), 0.07f, 0.07f, 6, Cord);
            gameObject.AddComponent<MeshFilter>().sharedMesh = model.Make("Coin pouch");
            shown = gameObject.AddComponent<MeshRenderer>();
            shown.sharedMaterial = material;
            var ball = gameObject.AddComponent<SphereCollider>();
            ball.center = new Vector3(0f, 0.13f, 0f);
            ball.radius = 0.2f;
        }

        public void Interact(PlayerInteractor by)
        {
            PlayerWallet purse = by != null ? by.GetComponentInParent<PlayerWallet>() : null;
            if (purse == null || purse.Wallet == null || coins == null || coins.Coins <= 0f)
            {
                return;
            }
            float all = coins.Coins;
            if (coins.Spend(all))
            {
                purse.Wallet.Earn(all);
            }
        }

        private void Update()
        {
            bool any = coins != null && coins.Coins > 0f;
            if (!any && goesWhenEmpty)
            {
                Destroy(gameObject);
                return;
            }
            if (shown != null && shown.enabled != any)
            {
                shown.enabled = any;
                GetComponent<Collider>().enabled = any;
            }
        }
    }
}
