using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A sleeping roll laid on the ground (GL290), bought from the merchant and placed like a station: Interact opens the
    /// player's <see cref="RestScreen"/> to rest for some hours. Once laid, it tilts to the average slope of the ground under
    /// its four corners (GL369), so it lies on a hillside instead of floating level over it.
    /// </summary>
    public class SleepingRoll : MonoBehaviour, IInteractable
    {
        [Tooltip("Steepest it tilts, in degrees; steeper ground still shows a gap.")]
        [SerializeField, Range(0f, 60f)]
        private float maxTilt = 35f;

        public string Prompt => "Rest";

        public void Interact(PlayerInteractor by)
        {
            RestScreen rest = by.GetComponent<RestScreen>();
            if (rest != null && !rest.IsOpen)
            {
                rest.Open(this);
            }
        }

        private void Start()
        {
            LieOnGround();
        }

        // The ground under the roll's four corners (its footprint, from its renderers), fitted with a plane: the roll turns to
        // that plane's normal, keeping its heading, and sits at the corners' average height.
        private void LieOnGround()
        {
            WorldView world = FindFirstObjectByType<WorldView>();
            if (world == null || !world.IsReady)
            {
                return;
            }
            Bounds local = LocalBounds();
            Vector3 right = transform.right * local.extents.x;
            Vector3 forward = transform.forward * local.extents.z;
            Vector3 centre = transform.TransformPoint(new Vector3(local.center.x, 0f, local.center.z));
            Vector3[] corners = { centre - right - forward, centre + right - forward, centre + right + forward, centre - right + forward };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 from = corners[i] + Vector3.up * 1.5f;
                if (!world.Raycast(new Ray(from, Vector3.down), out Vector3 ground) || (from - ground).sqrMagnitude > 9f)
                {
                    return;
                }
                corners[i] = ground;
            }
            Vector3 across = (corners[1] + corners[2]) - (corners[0] + corners[3]);
            Vector3 along = (corners[2] + corners[3]) - (corners[0] + corners[1]);
            Vector3 normal = Vector3.Cross(along, across).normalized;
            if (normal.y < 0f)
            {
                normal = -normal;
            }
            if (Vector3.Angle(Vector3.up, normal) > maxTilt)
            {
                normal = Vector3.Slerp(Vector3.up, normal, maxTilt / Vector3.Angle(Vector3.up, normal));
            }
            Quaternion heading = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * heading;
            Vector3 average = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            transform.position = new Vector3(transform.position.x, average.y, transform.position.z);
        }

        // The roll's renderers' bounds in its own space (it's placed level, so the world bounds turned back are close enough).
        private Bounds LocalBounds()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(0.8f, 0.1f, 2f));
            bool first = true;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }
                Bounds mesh = filter.sharedMesh.bounds;
                Vector3 min = transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.min));
                Vector3 max = transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.max));
                var part = new Bounds((min + max) * 0.5f, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z)));
                if (first)
                {
                    bounds = part;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(part);
                }
            }
            return bounds;
        }
    }
}
