using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Carries the player with what it stands on when that is <see cref="Rideable"/> (GL727: "ride on carts and boats if
    /// they jump on them"). After everything has moved for the frame it finds the collider under the player's feet; the
    /// next frame it moves and turns the player by however far that collider moved and turned since, so the player keeps
    /// its place on the deck or bed and can still walk about on it. A jump keeps the ride while the player is over it
    /// (within <see cref="StayReach"/>), so it lands where it took off; stepping off or the collider going away (a vehicle
    /// past the map's free ground, GL725) ends it.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController))]
    public class PlatformRider : MonoBehaviour
    {
        /// <summary>Metres under the feet a rideable collider is stepped onto.</summary>
        public const float StepOn = 0.3f;

        /// <summary>Metres under the feet a ride already begun is kept (a jump on a moving deck).</summary>
        public const float StayReach = 3f;

        // More than this many metres in one frame is a teleport or a respawn, not a ride.
        private const float MostShift = 5f;

        // Metres a standing rider is pressed onto the deck with the carry, to stay grounded.
        private const float GroundPress = 0.05f;

        private PlayerController player;
        private Transform platform;
        private Matrix4x4 lastToLocal;
        private float lastYaw;

        /// <summary>What the player rides now; null when on the ground.</summary>
        public Transform Platform => platform;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            platform = null;
        }

        private void LateUpdate()
        {
            CharacterController body = player.Body;
            // Held (climbing aboard, PlayerBoarding): whatever holds the player moves it.
            if (!player.enabled || player.Held || body == null || !body.enabled)
            {
                platform = null;
                return;
            }
            if (platform != null && platform.gameObject.activeInHierarchy)
            {
                Vector3 position = transform.position;
                Vector3 target = platform.localToWorldMatrix.MultiplyPoint3x4(lastToLocal.MultiplyPoint3x4(position));
                Vector3 shift = target - position;
                if (shift.sqrMagnitude > 1e-10f && shift.sqrMagnitude < MostShift * MostShift)
                {
                    // The deck moved by its transform since physics last saw it: bring its collider up to date first.
                    Physics.SyncTransforms();
                    // This move is the controller's last of the frame, so it sets isGrounded for the next: a player standing
                    // on the deck is pressed onto it as well, or a rising deck would read as being in the air (walking then
                    // only steers, and a held Jump doesn't jump).
                    bool standing = body.isGrounded;
                    body.Move(standing ? shift + Vector3.down * GroundPress : shift);
                }
                float turn = Mathf.DeltaAngle(lastYaw, platform.eulerAngles.y);
                if (Mathf.Abs(turn) > 1e-4f && Mathf.Abs(turn) < 45f)
                {
                    player.TurnBy(turn);
                }
            }
            platform = Under(body, platform != null && platform.gameObject.activeInHierarchy ? platform : null);
            if (platform != null)
            {
                lastToLocal = platform.worldToLocalMatrix;
                lastYaw = platform.eulerAngles.y;
            }
        }

        // The rideable collider under the feet: within a step of them, or further for a ride already begun.
        private Transform Under(CharacterController body, Transform riding)
        {
            float radius = body.radius * 0.9f;
            Vector3 from = transform.TransformPoint(body.center);
            float reach = body.height * 0.5f - radius + body.skinWidth + (riding != null ? StayReach : StepOn);
            if (!Physics.SphereCast(from, radius, Vector3.down, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                return null;
            }
            return hit.collider.GetComponentInParent<Rideable>() != null ? hit.collider.transform : null;
        }
    }
}
