using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Buying land from a settlement's Merchant (GL863–GL865). Opened from the trade screen's Purchase Parcel button: the
    /// player's camera looks straight down on the town, centred on the Merchant's shop and showing 4 x 4 parcels (the mouse
    /// wheel steps to 6 x 6 and 8 x 8 and back). The parcel grid is drawn over the ground: the settlement's own parcels grey,
    /// the player's green, the one under the mouse yellow with its price (<see cref="ParcelPrices"/>). A click buys it; Buy
    /// settlement buys the whole town at its type's price. These sales never raise the Merchant's disposition. Return gives
    /// the camera and controls back. Time keeps passing.
    /// </summary>
    [RequireComponent(typeof(PlayerParcels), typeof(PlayerWallet))]
    public class ParcelPurchaseView : MonoBehaviour, IGuiDrawer
    {
        private static readonly int[] Views = { 4, 6, 8 };

        private static readonly Color TownColour = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        private static readonly Color OwnedColour = new Color(0.2f, 0.95f, 0.3f, 0.45f);
        private static readonly Color HoverColour = new Color(1f, 0.9f, 0.1f, 0.5f);
        private static readonly Color ClosedColour = new Color(0.1f, 0.05f, 0.05f, 0.45f);
        private static readonly Color LineColour = new Color(0f, 0f, 0f, 0.7f);

        private const float PanelWidth = 440f;
        private const float Padding = 12f;
        private const float RowHeight = 40f;

        [Tooltip("How high over the Merchant's shop the camera looks down from, in metres.")]
        [SerializeField, Min(20f)]
        private float cameraHeight = 220f;

        private PlayerParcels parcels;
        private PlayerWallet wallet;
        private PlayerController player;
        private readonly TopDownCameraHold view = new TopDownCameraHold();
        private readonly HashSet<Vector2Int> town = new HashSet<Vector2Int>();
        private List<Vector2Int> townList;
        private SettlementLand land;
        private string merchant;
        private int disposition;
        private Vector3 centre;
        private int zoom;
        private Vector2Int? hovered;
        private bool settlementHovered;
        private Rect panel;
        private string message;
        private float messageUntil;
        private Texture2D white;

        /// <summary>True while the land view is open.</summary>
        public bool IsOpen => land != null;

        /// <summary>Parcels across the view: 4, 6 or 8.</summary>
        public int ParcelsAcross => Views[zoom];

        /// <summary>The settlement's whole land price with the Merchant's disposition.</summary>
        public int SettlementPrice => land != null ? ParcelPrices.WithDisposition(land.SalePrice, disposition) : 0;

        private void Awake()
        {
            parcels = GetComponent<PlayerParcels>();
            wallet = GetComponent<PlayerWallet>();
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            Return();
        }

        /// <summary>
        /// Opens the view over a settlement's land. <paramref name="merchantDisposition"/> is the Merchant's disposition as their
        /// trade screen showed it; <paramref name="shop"/> is where they stand (world space), the view's centre.
        /// </summary>
        public bool Open(SettlementLand settlementLand, string merchantName, int merchantDisposition, Vector3 shop)
        {
            if (settlementLand == null || parcels.Parcels == null || IsOpen)
            {
                return false;
            }
            Camera eyes = GetComponentInChildren<Camera>();
            if (!view.Take(eyes))
            {
                return false;
            }
            land = settlementLand;
            merchant = merchantName;
            disposition = merchantDisposition;
            centre = shop;
            zoom = 0;
            townList = land.ParcelsIn(parcels.Parcels);
            town.Clear();
            town.UnionWith(townList);
            hovered = null;
            message = null;
            if (player != null)
            {
                player.IsInMenu = true;
            }
            Aim();
            return true;
        }

        /// <summary>Closes the view: the camera and controls go back to normal play.</summary>
        public void Return()
        {
            if (!IsOpen)
            {
                return;
            }
            view.Release();
            land = null;
            townList = null;
            town.Clear();
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        /// <summary>Steps the view one size out (+1) or in (-1), between 4, 6 and 8 parcels across.</summary>
        public void Step(int direction)
        {
            zoom = Mathf.Clamp(zoom + direction, 0, Views.Length - 1);
            Aim();
        }

        /// <summary>
        /// The price of a parcel from this Merchant, with what it is: 0 and a reason for one that can't be bought (the town's,
        /// already the player's, or not for sale).
        /// </summary>
        public int PriceOf(Vector2Int parcel, out string why)
        {
            LandParcels lands = parcels.Parcels;
            why = null;
            if (lands.IsOwned(parcel))
            {
                why = "Your land";
                return 0;
            }
            if (town.Contains(parcel))
            {
                why = "Settlement land: use Buy settlement";
                return 0;
            }
            if (lands.IsLocked(parcel) || !lands.IsInSaleArea(parcel))
            {
                why = "Not for sale";
                return 0;
            }
            return ParcelPrices.PriceOf(parcel, townList, disposition);
        }

        /// <summary>Buys a parcel at <see cref="PriceOf"/>; false, saying why, when it can't be bought.</summary>
        public bool Buy(Vector2Int parcel)
        {
            int price = PriceOf(parcel, out string why);
            if (why != null)
            {
                Say(why);
                return false;
            }
            if (!parcels.BuyAt(parcel, price))
            {
                Say($"Not enough coins: it costs {price}c, you have {Wallet.Format(wallet.Wallet.Coins)}");
                return false;
            }
            Say($"Bought for {price}c");
            return true;
        }

        /// <summary>Buys the whole settlement's land at <see cref="SettlementPrice"/>; false, saying why, when it can't.</summary>
        public bool BuySettlement()
        {
            if (land.SalePrice <= 0)
            {
                Say("This settlement isn't for sale");
                return false;
            }
            int price = SettlementPrice;
            if (!parcels.BuyAll(townList, price))
            {
                bool allOwned = townList.TrueForAll(parcels.Parcels.IsOwned);
                Say(allOwned ? "You own it already" : $"Not enough coins: it costs {price}c, you have {Wallet.Format(wallet.Wallet.Coins)}");
                return false;
            }
            Say($"Bought {land.Name} for {price}c");
            return true;
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (scroll != 0f)
            {
                // Wheel back (down) shows more land, forward shows less.
                Step(scroll < 0f ? 1 : -1);
            }
            else
            {
                Aim();
            }
        }

        private void Aim()
        {
            if (!IsOpen)
            {
                return;
            }
            Transform world = parcels.WorldView != null ? parcels.WorldView.transform : null;
            Vector3 up = world != null ? world.up : Vector3.up;
            Vector3 north = world != null ? world.forward : Vector3.forward;
            view.Look(centre, up, north, ParcelsAcross * parcels.Parcels.Size, cameraHeight);
        }

        private void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            if (!IsOpen || view.Camera == null)
            {
                return;
            }
            GameUi.Apply();
            Event e = Event.current;
            panel = new Rect(16f, 16f, PanelWidth, Padding * 2f + 34f * 4f + RowHeight * 2f + 12f);
            bool overPanel = panel.Contains(e.mousePosition);
            hovered = overPanel ? (Vector2Int?)null : ParcelUnder(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                DrawGrid();
                DrawHoverLabel(e.mousePosition);
            }
            DrawPanel();
            if (e.type == EventType.MouseDown && e.button == 0 && hovered.HasValue)
            {
                Buy(hovered.Value);
                e.Use();
            }
        }

        // The parcel under a GUI point, through the top-down camera onto the level of the Merchant's shop.
        private Vector2Int? ParcelUnder(Vector2 guiPoint)
        {
            Camera camera = view.Camera;
            Vector2 screen = GUIUtility.GUIToScreenPoint(guiPoint);
            Ray ray = camera.ScreenPointToRay(new Vector3(screen.x, Screen.height - screen.y, 0f));
            var ground = new Plane(camera.transform.forward * -1f, centre);
            if (!ground.Raycast(ray, out float distance))
            {
                return null;
            }
            return parcels.Parcels.ParcelAt(parcels.ToLocal(ray.GetPoint(distance)));
        }

        // A parcel's square on the screen, in GUI coordinates.
        private Rect ScreenRect(Vector2Int parcel, float level)
        {
            Rect area = parcels.Parcels.AreaOf(parcel);
            Vector2 a = ToGui(new Vector3(area.xMin, level, area.yMin));
            Vector2 b = ToGui(new Vector3(area.xMax, level, area.yMax));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private Vector2 ToGui(Vector3 local)
        {
            Vector3 screen = view.Camera.WorldToScreenPoint(parcels.ToWorld(local));
            return GUIUtility.ScreenToGUIPoint(new Vector2(screen.x, Screen.height - screen.y));
        }

        private void DrawGrid()
        {
            LandParcels lands = parcels.Parcels;
            float level = parcels.ToLocal(centre).y;
            Vector2Int middle = lands.ParcelAt(parcels.ToLocal(centre));
            // The screen is wider than tall: cover its width too.
            int reach = Mathf.CeilToInt(ParcelsAcross * 0.5f * Mathf.Max(1f, view.Camera.aspect)) + 1;
            Color before = GUI.color;
            for (int z = middle.y - reach; z <= middle.y + reach; z++)
            {
                for (int x = middle.x - reach; x <= middle.x + reach; x++)
                {
                    var parcel = new Vector2Int(x, z);
                    Rect square = ScreenRect(parcel, level);
                    Color? fill = FillOf(parcel);
                    if (fill.HasValue)
                    {
                        Fill(square, fill.Value);
                    }
                    Outline(square, 2f);
                }
            }
            GUI.color = before;
        }

        private Color? FillOf(Vector2Int parcel)
        {
            LandParcels lands = parcels.Parcels;
            bool inTown = town.Contains(parcel);
            if ((settlementHovered && inTown) || (hovered == parcel && !inTown && !lands.IsOwned(parcel)))
            {
                return HoverColour;
            }
            if (lands.IsOwned(parcel))
            {
                return OwnedColour;
            }
            if (inTown)
            {
                return TownColour;
            }
            if (lands.IsLocked(parcel) || !lands.IsInSaleArea(parcel))
            {
                return ClosedColour;
            }
            return null;
        }

        private void Fill(Rect rect, Color colour)
        {
            GUI.color = colour;
            GUI.DrawTexture(rect, White());
        }

        private void Outline(Rect rect, float width)
        {
            GUI.color = LineColour;
            Texture2D tex = White();
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width * 0.5f), tex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - width * 0.5f, rect.width, width * 0.5f), tex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, width * 0.5f, rect.height), tex);
            GUI.DrawTexture(new Rect(rect.xMax - width * 0.5f, rect.y, width * 0.5f, rect.height), tex);
        }

        private Texture2D White()
        {
            if (white == null)
            {
                white = Texture2D.whiteTexture;
            }
            return white;
        }

        // The price (or why not) by the mouse.
        private void DrawHoverLabel(Vector2 mouse)
        {
            if (!hovered.HasValue)
            {
                return;
            }
            int price = PriceOf(hovered.Value, out string why);
            string text = why ?? $"{price}c  (click to buy)";
            var style = new GUIStyle(GUI.skin.label) { fontSize = GameUi.Font(18), fontStyle = FontStyle.Bold };
            style.normal.textColor = why == null ? new Color(1f, 0.9f, 0.3f) : Color.white;
            GUI.color = Color.white;
            GameUi.OutlinedLabel(new Rect(mouse.x + 18f, mouse.y - 8f, 420f, 32f), text, style);
        }

        private void DrawPanel()
        {
            InventoryGridGui.SolidPanel(panel);
            float x = panel.x + Padding;
            float y = panel.y + Padding;
            float width = panel.width - Padding * 2f;
            GUI.Label(new Rect(x, y, width, 30f), $"{land.Name} ({land.TypeName}): land from {merchant}");
            y += 34f;
            GUI.Label(new Rect(x, y, width, 30f), $"Your coins: {Wallet.Format(wallet.Wallet.Coins)}");
            y += 34f;
            GUI.Label(new Rect(x, y, width, 30f), $"Disposition {disposition}: prices × {1f + disposition / 100f:0.##}");
            y += 34f;

            var settlementButton = new Rect(x, y, width, RowHeight);
            if (Event.current.type == EventType.Repaint)
            {
                settlementHovered = settlementButton.Contains(Event.current.mousePosition);
            }
            string buyText = land.SalePrice > 0 ? $"Buy settlement: {SettlementPrice}c" : "Settlement not for sale";
            if (GameUi.Button(settlementButton, buyText))
            {
                BuySettlement();
            }
            y += RowHeight + 6f;

            string note = Time.unscaledTime < messageUntil ? message : $"Mouse wheel: {ParcelsAcross} x {ParcelsAcross} parcels (4, 6, 8)";
            GUI.Label(new Rect(x, y, width, 30f), note);
            y += 34f;
            if (GameUi.Button(new Rect(x, y, width, RowHeight), "Return"))
            {
                Return();
            }
        }
    }
}
