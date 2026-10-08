using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The player's interaction (GL9, first pass of I3): finds the <see cref="IInteractable"/>
    /// under the crosshair within reach, shows its prompt ("E  Use furnace"), and uses it on
    /// Interact. While something is targeted, the player's tools leave the ground behind it
    /// alone (<see cref="PlayerToolUser"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Uses what's targeted (E).")]
        [SerializeField]
        private InputActionReference interactAction;

        [Tooltip("How far from the head things can be used, in metres.")]
        [SerializeField, Min(0f)]
        private float reach = 3f;

        private readonly RaycastHit[] hits = new RaycastHit[8];
        private PlayerController player;
        private GUIStyle promptStyle;

        /// <summary>What's under the crosshair and within reach this frame, or null.</summary>
        public IInteractable Target { get; private set; }

        public float Reach => reach;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            interactAction?.action.Enable();
        }

        private void OnDisable()
        {
            Target = null;
        }

        private void Update()
        {
            Target = null;
            // On the frame a screen closed, its E mustn't reopen what's still under the crosshair.
            if (!player.enabled || player.IsCursorFree || player.MenuClosedThisFrame)
            {
                return;
            }

            Transform head = player.Head != null ? player.Head : transform;
            // The player's own capsule isn't something to use; skip it.
            int count = Physics.RaycastNonAlloc(head.position, head.forward, hits, reach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                {
                    continue;
                }
                nearest = hit.distance;
                Target = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (Target != null && interactAction != null && interactAction.action.WasPressedThisFrame())
            {
                Target.Interact(this);
            }
        }

        private void OnGUI()
        {
            if (Target == null || !player.enabled || player.IsCursorFree)
            {
                return;
            }
            promptStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 24f, Screen.width, 24f), $"E  {Target.Prompt}", promptStyle);
        }
    }
}
