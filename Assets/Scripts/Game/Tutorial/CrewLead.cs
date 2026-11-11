using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The Mining Corp's crew lead in the opening tutorial (GL479): a miner's figure (<see cref="KeeperFigure"/>) at the
    /// quarry's rim that Interact talks to, who later walks off towards the town and is gone.
    /// </summary>
    public class CrewLead : MonoBehaviour, IInteractable
    {
        private const float WalkSpeed = 1.4f;
        private const float GoneAfter = 25f;

        private KeeperFigure figure;
        private Vector3 leavingTo;
        private float walked;

        /// <summary>The tutorial that answers when the player talks to the lead.</summary>
        public MiningCorpTutorial Tutorial { get; set; }

        /// <summary>True once the lead has set off.</summary>
        public bool IsLeaving { get; private set; }

        public string Prompt => Tutorial != null ? Tutorial.TalkPrompt : "Talk to the crew lead";

        /// <summary>Builds the lead's figure standing at a point, facing another.</summary>
        public static CrewLead Build(Vector3 at, Vector3 lookAt, Material material, MiningCorpTutorial tutorial)
        {
            var go = new GameObject("Crew lead");
            go.transform.position = at;
            Vector3 flat = Vector3.ProjectOnPlane(lookAt - at, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                go.transform.rotation = Quaternion.LookRotation(flat);
            }
            CrewLead lead = go.AddComponent<CrewLead>();
            lead.Tutorial = tutorial;
            lead.figure = go.AddComponent<KeeperFigure>();
            lead.figure.Build(ShopKind.Mining, material, 479);
            lead.figure.Pose = KeeperPose.Stand;
            return lead;
        }

        public void Interact(PlayerInteractor by)
        {
            if (Tutorial != null && !IsLeaving)
            {
                Tutorial.TalkToLead();
            }
        }

        /// <summary>Turns to face a point (world space) while standing.</summary>
        public void Face(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (!IsLeaving && flat.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 3f);
            }
        }

        /// <summary>
        /// Stands at a spot (world space) facing a point, whether he was standing about or had walked off (GL567: he turns up
        /// beside a player who is being thrown out).
        /// </summary>
        public void Appear(Vector3 at, Vector3 lookAt)
        {
            IsLeaving = false;
            walked = 0f;
            gameObject.SetActive(true);
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                at.y = hit.point.y;
            }
            transform.position = at;
            figure.Pose = KeeperPose.Stand;
            foreach (Collider body in GetComponentsInChildren<Collider>(true))
            {
                body.enabled = true;
            }
            Vector3 flat = Vector3.ProjectOnPlane(lookAt - at, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.LookRotation(flat);
            }
        }

        /// <summary>Sets off walking towards a point (world space).</summary>
        public void Leave(Vector3 towards)
        {
            IsLeaving = true;
            leavingTo = towards;
            figure.Pose = KeeperPose.Walk;
            // Nothing more to say, and the ground ray below mustn't land on the lead's own body.
            foreach (Collider body in GetComponentsInChildren<Collider>())
            {
                body.enabled = false;
            }
        }

        private void Update()
        {
            if (!IsLeaving)
            {
                return;
            }
            Vector3 flat = Vector3.ProjectOnPlane(leavingTo - transform.position, Vector3.up);
            if (flat.magnitude < 1f || walked > GoneAfter * WalkSpeed)
            {
                gameObject.SetActive(false);
                return;
            }
            transform.rotation = Quaternion.LookRotation(flat);
            float step = WalkSpeed * Time.deltaTime;
            walked += step;
            Vector3 next = transform.position + flat.normalized * step;
            // Follow the ground: a ray down from head height.
            if (Physics.Raycast(next + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                next.y = hit.point.y;
            }
            transform.position = next;
        }
    }
}
