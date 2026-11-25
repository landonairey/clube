using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Claims the stump or boulder under the crosshair on the first right click (prototype, backlog 2026-11-24 item 8):
    /// when <see cref="PlayerInteractor"/> targets an <see cref="UnclaimedWorksite"/>, Place claims it instead of doing
    /// anything else, and a notice says so. Until then the stump's station and the boulder's bashing are not offered.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class WorksiteClaimer : MonoBehaviour, IGuiDrawer
    {
        private InputActionReference placeAction;
        private PlayerController player;
        private PlayerInteractor interactor;
        private PlayerToolUser tools;
        private int claimedFrame = -1;
        private string notice;
        private float noticeUntil;

        /// <summary>
        /// True when Place would claim something this frame: an unclaimed stump or boulder is targeted and no tool is in
        /// hand (an empty hand or a non-tool item, such as a stone). With a pick, axe or other tool, its own use comes first
        /// (backlog 2026-11-24b item 1).
        /// </summary>
        public bool CanClaim => interactor != null && interactor.enabled && interactor.Target is UnclaimedWorksite
            && (tools == null || !tools.HoldsTool);

        /// <summary>True on the frame a claim was made, so the same right click does nothing else.</summary>
        public bool ClaimedThisFrame => claimedFrame == Time.frameCount;

        /// <summary>The Place (right click) action, given by <see cref="BoulderBashing"/>, which adds this component.</summary>
        public void Init(InputActionReference place)
        {
            placeAction = place;
            placeAction?.action.Enable();
        }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            interactor = GetComponent<PlayerInteractor>();
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
        }

        private void Update()
        {
            if (placeAction == null || !player.enabled || player.IsCursorFree || !placeAction.action.WasPressedThisFrame())
            {
                return;
            }
            if (CanClaim && interactor.Target is UnclaimedWorksite site)
            {
                site.Claim();
                claimedFrame = Time.frameCount;
                notice = $"You've claimed this {site.Name}: E to use it";
                noticeUntil = Time.time + 3f;
            }
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            GameUi.Apply();
            if (notice == null || Time.time >= noticeUntil)
            {
                return;
            }
            var area = new Rect(Screen.width * 0.5f - 220f, 100f, 440f, 30f);
            InventoryGridGui.Panel(area);
            GUI.Label(area, notice, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
