using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The map's horse-hire stands (GL728: "an NPC with a Horse tied up to a post next to the Town Signs outside the
    /// Capital and each City offering a fast travel option between them for 250c"): where they are comes from the plan
    /// (<see cref="HorseHire.Plan"/>); a stand is built (<see cref="HorseHireStand"/>) once the viewer is near and its
    /// ground has loaded. <see cref="Ride"/> takes the fare and sets the rider down at the other stand once the ground
    /// there has loaded (<see cref="PlayerSpawn.SetDownOver"/>). <see cref="MapRoads"/> adds it at runtime, so no scene
    /// changes.
    /// </summary>
    public class MapHorseHire : MonoBehaviour
    {
        private const float BuildRange = 150f;

        private MapSession session;
        private Material material;
        private List<HorseStand> stands = new List<HorseStand>();
        private HorseHireStand[] built = System.Array.Empty<HorseHireStand>();
        private Transform root;
        private int version = -1;

        /// <summary>The stands of the map, in settlement order; empty until the roads are laid.</summary>
        public IReadOnlyList<HorseStand> Stands => stands;

        /// <summary>Stands built in the world so far.</summary>
        public int Built { get; private set; }

        public MapSession Session => session;

        public void Setup(MapSession mapSession, Material standMaterial)
        {
            session = mapSession;
            material = standMaterial;
        }

        /// <summary>A stand's settlement's name.</summary>
        public string NameOf(int stand)
        {
            return session.Plan.Settlements[stands[stand].Settlement].Name;
        }

        /// <summary>Metres between two stands, as the crow flies.</summary>
        public float Distance(int from, int to)
        {
            return (stands[from].Post - stands[to].Post).magnitude;
        }

        /// <summary>
        /// Takes the fare from <paramref name="purse"/> and sends the player to stand <paramref name="to"/>: it is set down
        /// beside the post there once the ground has loaded. False, and nothing taken, when it can't pay or can't go.
        /// </summary>
        public bool Ride(GameObject player, Wallet purse, int to)
        {
            if (session == null || to < 0 || to >= stands.Count || purse == null || !HorseHire.CanPay(purse.Coins))
            {
                return false;
            }
            PlayerSpawn spawn = player.GetComponent<PlayerSpawn>();
            if (spawn == null || !purse.Spend(HorseHire.Fare))
            {
                return false;
            }
            Vector2 arrival = stands[to].Arrival;
            float height = session.Map.Height(arrival.x, arrival.y);
            spawn.SetDownOver(session.WorldView.transform.TransformPoint(new Vector3(arrival.x, height, arrival.y)));
            return true;
        }

        private void Update()
        {
            if (session == null || !session.IsReady || session.Plan == null || material == null)
            {
                return;
            }
            // The stands go by the settlements' edge signposts, which come with the roads on the real ground.
            if (session.Version != version || (stands.Count == 0 && session.Plan.Signposts.Count > 0))
            {
                StartOver();
            }
            Camera view = Camera.main;
            if (view == null)
            {
                return;
            }
            Vector3 local = session.WorldView.transform.InverseTransformPoint(view.transform.position);
            var viewer = new Vector2(local.x, local.z);
            World world = session.WorldView.World;
            for (int i = 0; i < stands.Count; i++)
            {
                HorseStand stand = stands[i];
                if (built[i] != null || (stand.Post - viewer).sqrMagnitude > BuildRange * BuildRange)
                {
                    continue;
                }
                float natural = session.Map.Height(stand.Post.x, stand.Post.y);
                if (!world.IsLoaded(world.Grid.WorldToChunk(new Vector3(stand.Post.x, natural, stand.Post.y))))
                {
                    continue;
                }
                float ground = GroundProbe.TryGround(session.WorldView, stand.Post.x, stand.Post.y, natural + 3f, out float found) ? found : natural;
                var made = new GameObject($"Horse hire ({NameOf(i)})");
                made.transform.SetParent(root, false);
                made.transform.localPosition = new Vector3(stand.Post.x, ground, stand.Post.y);
                // z along the road out of the settlement, x away from the road.
                Vector3 forward = new Vector3(stand.Outward.x, 0f, stand.Outward.y);
                Vector3 away = new Vector3(stand.Roadside.x, 0f, stand.Roadside.y);
                if (Vector3.Dot(Vector3.Cross(Vector3.up, forward), away) < 0f)
                {
                    forward = -forward;
                }
                made.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
                built[i] = made.AddComponent<HorseHireStand>();
                built[i].Build(this, i, NameOf(i), material, session.Plan.Seed + stand.Settlement * 389);
                Built++;
            }
        }

        private void StartOver()
        {
            version = session.Version;
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Horse hire").transform;
            root.SetParent(session.WorldView.transform, false);
            stands = HorseHire.Plan(session.Plan);
            built = new HorseHireStand[stands.Count];
            Built = 0;
        }
    }
}
