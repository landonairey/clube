using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The docks of the map's sea ports (GL726): each settlement on a sea route gets the dock its type's
    /// <see cref="DockDesign"/> row gives (a foot dock for a village or outpost, a pier with a head for a town, a larger
    /// harbour for a city, grand stone docks for the capital), laid on the shore nearest its buildings
    /// (<see cref="Docks.Lay"/>), with a way up to the nearest building (paved for the capital). A dock is in the world
    /// while the viewer is near; <see cref="MapBoats"/> moors its vessels at the berths.
    /// </summary>
    /// <remarks>Added to the World by <see cref="MapBoats"/> when it starts, so no scene needs it set up.</remarks>
    public class SettlementDocks : MonoBehaviour
    {
        private const float ShowRange = 480f;

        // Metres of water a dock's end needs: a sea trader's draught and some under its keel.
        private const float NeedDepth = 1.6f;

        // Metres within which the way up is laid again, on the voxel ground by then loaded.
        private const float NearRange = 90f;

        private static readonly Color PathDirt = new Color(0.47f, 0.4f, 0.3f);
        private static readonly Color PathStone = new Color(0.55f, 0.54f, 0.5f);
        private static readonly Color PathStoneDark = new Color(0.45f, 0.44f, 0.41f);

        private readonly List<DockDesign> designs = DockDesign.Defaults();
        private readonly Dictionary<int, DockLayout> laid = new Dictionary<int, DockLayout>();
        private readonly Dictionary<int, Built> built = new Dictionary<int, Built>();
        private readonly List<int> ports = new List<int>();
        private MapSession session;
        private MapSettlements settlements;
        private Material material;
        private Transform root;
        private int version = -1;

        /// <summary>The kinds of dock; change a row to change every dock of that size.</summary>
        public List<DockDesign> Designs => designs;

        /// <summary>Docks in the world now.</summary>
        public int Showing => built.Count;

        public void Bind(MapSession mapSession, MapSettlements mapSettlements, Material lowPoly)
        {
            session = mapSession;
            settlements = mapSettlements;
            material = lowPoly;
        }

        /// <summary>The height of a dock's deck: the sea and the row's rise over it.</summary>
        public float DeckHeight(DockLayout dock)
        {
            return session.Map.Water.SeaLevel + dock.Design.DeckRise;
        }

        /// <summary>A settlement's dock, laid out on the first ask; null when it has none (no sea route, no shore near, a camp).</summary>
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
            DockDesign design = DockDesign.For(designs, port.Type);
            Vector2? harbour = HarbourOf(settlement);
            SettlementSite site = settlements != null ? settlements.LaidSite(settlement) : null;
            if (design == null || harbour == null || site == null || site.Buildings.Count == 0)
            {
                return null;
            }
            var fronts = new List<Vector2>();
            foreach (SettlementBuilding building in site.Buildings)
            {
                fronts.Add(building.ToWorld(new Vector2(0f, building.Depth * 0.5f + 1.4f)));
            }
            float sea = session.Map.Water.SeaLevel;
            dock = Docks.Lay(design, fronts, harbour.Value, at => DepthAt(at, sea), NeedDepth);
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

        // Where a settlement's sea routes leave from: the first point of a route from it, the last of one to it.
        private Vector2? HarbourOf(int settlement)
        {
            foreach (WaterRoute route in session.Plan.WaterRoutes)
            {
                if (route.Kind != WaterRouteKind.Sea || route.Points.Count == 0)
                {
                    continue;
                }
                if (route.From == settlement)
                {
                    return route.Points[0];
                }
                if (route.To == settlement)
                {
                    return route.Points[route.Points.Count - 1];
                }
            }
            return null;
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
            BuildPath(body.transform, dock, rootGround);
            return new Built { Body = body, Dock = dock, Mesh = mesh };
        }

        // The way up from the nearest building to the pier's root: flags for the stone docks, a trodden path otherwise.
        private void BuildPath(Transform parent, DockLayout dock, float rootGround)
        {
            Vector2 from = dock.PathFrom, to = dock.Root;
            float length = Vector2.Distance(from, to);
            if (length < 0.5f)
            {
                return;
            }
            Vector2 along = (to - from) / length;
            var model = new LowPolyMeshBuilder();
            bool stone = dock.Design.Stone;
            float step = stone ? 1f : 1.5f;
            Vector3 origin = parent.localPosition;
            Quaternion toDock = Quaternion.Inverse(parent.localRotation);
            int k = 0;
            for (float d = 0f; d < length; d += step, k++)
            {
                Vector2 at = from + along * Mathf.Min(d + step * 0.5f, length);
                float y = d + step >= length ? rootGround : Ground(at);
                Vector3 centre = toDock * (new Vector3(at.x, y + 0.04f, at.y) - origin);
                Quaternion turn = toDock * Quaternion.LookRotation(new Vector3(along.x, 0f, along.y));
                Color colour = stone ? (k % 2 == 0 ? PathStone : PathStoneDark) : PathDirt;
                model.Box(centre, new Vector3(dock.Design.PathWidth, 0.12f, step * (stone ? 0.94f : 1.05f)), turn, colour);
            }
            var path = new GameObject(stone ? "Paved way" : "Path");
            path.transform.SetParent(parent, false);
            path.AddComponent<MeshFilter>().sharedMesh = model.Make(path.name);
            path.AddComponent<MeshRenderer>().sharedMaterial = material;
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
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = new GameObject("Docks").transform;
            root.SetParent(session.WorldView.transform, false);
            WorldPlan plan = session.Plan;
            foreach (PlannedSettlement settlement in plan.Settlements)
            {
                if (HarbourOf(settlement.Index) != null && DockDesign.For(designs, settlement.Type) != null)
                {
                    ports.Add(settlement.Index);
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
