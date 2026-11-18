using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The docks of the map's sea ports (GL726): each settlement on a sea route gets the dock its type's
    /// <see cref="DockDesign"/> row gives (a foot dock for a village or outpost, a pier with a head for a town, a larger
    /// harbour for a city, grand stone docks for the capital), laid where the settlement's road down to the water ends
    /// (GL753: <see cref="PlannedRoad.ToDock"/>, built into the ground like any road; <see cref="Docks.Lay"/>). A dock is in
    /// the world while the viewer is near; <see cref="MapBoats"/> moors its vessels at the berths.
    /// </summary>
    /// <remarks>Added to the World by <see cref="MapBoats"/> when it starts, so no scene needs it set up.</remarks>
    public class SettlementDocks : MonoBehaviour
    {
        private const float ShowRange = 480f;

        // Metres of water a dock's end needs: a sea trader's draught and some under its keel.
        private const float NeedDepth = 1.6f;

        // Metres within which the ramp is laid again, on the voxel ground by then loaded.
        private const float NearRange = 90f;

        private readonly Dictionary<int, Vector2> roadEnds = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, DockLayout> laid = new Dictionary<int, DockLayout>();
        private readonly Dictionary<int, Built> built = new Dictionary<int, Built>();
        private readonly List<int> ports = new List<int>();
        private MapSession session;
        private Material material;
        private Transform root;
        private int version = -1;

        /// <summary>Docks in the world now.</summary>
        public int Showing => built.Count;

        public void Bind(MapSession mapSession, Material lowPoly)
        {
            session = mapSession;
            material = lowPoly;
        }

        /// <summary>The height of a dock's deck: the sea and the row's rise over it.</summary>
        public float DeckHeight(DockLayout dock)
        {
            return session.Map.Water.SeaLevel + dock.Design.DeckRise;
        }

        /// <summary>A settlement's dock, laid out on the first ask; null when it has none (no road down to the water).</summary>
        public DockLayout DockOf(int settlement)
        {
            if (session == null || !session.IsReady || session.Plan == null || settlement < 0)
            {
                return null;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            if (laid.TryGetValue(settlement, out DockLayout dock))
            {
                return dock;
            }
            laid[settlement] = null;
            WorldPlan plan = session.Plan;
            PlannedSettlement port = plan.Settlements[settlement];
            DockDesign design = DockDesign.For(plan.Rules.Docks, port.Type);
            Vector2? harbour = plan.HarbourOf(settlement);
            if (design == null || harbour == null || !roadEnds.TryGetValue(settlement, out Vector2 roadEnd))
            {
                return null;
            }
            float sea = session.Map.Water.SeaLevel;
            dock = Docks.Lay(design, new[] { roadEnd }, harbour.Value, at => DepthAt(at, sea), NeedDepth);
            laid[settlement] = dock;
            return dock;
        }

        // Metres of sea over the ground at a place; 0 or less on land or in fresh water.
        private float DepthAt(Vector2 at, float sea)
        {
            if (!WaterLanes.TrySurfaceAt(session.Map.Water, at, out float surface) || Mathf.Abs(surface - sea) > 0.3f)
            {
                return 0f;
            }
            return surface - session.Map.Height(at.x, at.y);
        }

        private void Update()
        {
            if (session == null || !session.IsReady || session.Plan == null || material == null)
            {
                return;
            }
            if (session.Version != version)
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
            foreach (int port in ports)
            {
                float distance = (session.Plan.Settlements[port].Position - viewer).magnitude;
                bool have = built.TryGetValue(port, out Built made);
                if (!have && distance < ShowRange)
                {
                    DockLayout dock = DockOf(port);
                    if (dock != null)
                    {
                        built[port] = Build(port, dock);
                    }
                }
                else if (have && distance > ShowRange + 60f)
                {
                    Remove(made);
                    built.Remove(port);
                }
                else if (have && !made.LaidNear && distance < NearRange)
                {
                    // The voxel ground is loaded by now: the ramp and the way up are laid again on it.
                    Remove(made);
                    built[port] = Build(port, made.Dock);
                    built[port].LaidNear = true;
                }
            }
        }

        private Built Build(int port, DockLayout dock)
        {
            float deck = DeckHeight(dock);
            float rootGround = Ground(dock.Root);
            var body = new GameObject($"{dock.Design.Name} of {session.Plan.Settlements[port].Name}");
            body.transform.SetParent(root, false);
            body.transform.localPosition = new Vector3(dock.Root.x, deck, dock.Root.y);
            body.transform.localRotation = Quaternion.LookRotation(new Vector3(dock.Out.x, 0f, dock.Out.y));
            float floor = session.Map.Height(dock.Tip.x, dock.Tip.y);
            Mesh mesh = DockMeshes.Build(dock, rootGround - deck, Mathf.Clamp(deck - floor + 0.5f, 1.5f, 12f));
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = material;
            body.AddComponent<MeshCollider>().sharedMesh = mesh;
            return new Built { Body = body, Dock = dock, Mesh = mesh };
        }

        // Takes a dock out of the world, with the meshes made for it.
        private static void Remove(Built made)
        {
            foreach (MeshFilter filter in made.Body.GetComponentsInChildren<MeshFilter>())
            {
                Destroy(filter.sharedMesh);
            }
            Destroy(made.Body);
        }

        private float Ground(Vector2 at)
        {
            float height = session.Map.Height(at.x, at.y);
            return GroundProbe.TryGround(session.WorldView, at.x, at.y, height + 4f, out float ground) ? ground : height;
        }

        private void StartOver()
        {
            version = session.Version;
            foreach (Built made in built.Values)
            {
                Remove(made);
            }
            built.Clear();
            laid.Clear();
            ports.Clear();
            roadEnds.Clear();
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Docks").transform;
            root.SetParent(session.WorldView.transform, false);
            foreach (PlannedRoad road in session.Plan.Roads)
            {
                if (road.ToDock && road.Points.Count >= 2)
                {
                    ports.Add(road.From);
                    roadEnds[road.From] = road.Points[road.Points.Count - 1];
                }
            }
        }

        private sealed class Built
        {
            public GameObject Body;
            public DockLayout Dock;
            public Mesh Mesh;
            public bool LaidNear;
        }
    }
}
