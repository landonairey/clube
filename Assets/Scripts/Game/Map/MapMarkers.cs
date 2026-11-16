using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Marks the map's plan on the ground until the settlements are built (GL601): a tall post with a banner in the
    /// type's colour and the name at every settlement site, and short posts in the road class's colour along the
    /// roads near the viewer. Markers only: no colliders, nothing written into the terrain.
    /// </summary>
    public class MapMarkers : MonoBehaviour
    {
        [SerializeField]
        private MapSession session;

        [Tooltip("An unlit material whose _BaseColor is tinted per marker; a plain unlit one is made when empty.")]
        [SerializeField]
        private Material markerMaterial;

        [Tooltip("Road posts are shown within this many metres of the viewer.")]
        [SerializeField, Range(40f, 600f)]
        private float roadPostRange = 220f;

        [Tooltip("Metres between road posts.")]
        [SerializeField, Range(4f, 40f)]
        private float roadPostSpacing = 12f;

        [Tooltip("Height of a settlement's post in metres.")]
        [SerializeField, Range(3f, 30f)]
        private float settlementPostHeight = 12f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly List<Transform> labels = new List<Transform>();
        private readonly List<GameObject> roadPosts = new List<GameObject>();
        private Transform settlementRoot, roadRoot;
        private MaterialPropertyBlock block;
        private Mesh cube;
        private Vector3 postsCentre;
        private bool havePosts;
        private int version = -1;

        /// <summary>Whether the markers are drawn.</summary>
        public bool Show
        {
            get => show;
            set
            {
                show = value;
                if (settlementRoot != null)
                {
                    settlementRoot.gameObject.SetActive(show);
                    roadRoot.gameObject.SetActive(show);
                }
            }
        }

        [SerializeField]
        private bool show = true;

        /// <summary>Settlement posts standing now.</summary>
        public int SettlementCount => labels.Count;

        /// <summary>Road posts shown round the viewer now.</summary>
        public int RoadPostCount { get; private set; }

        private void LateUpdate()
        {
            if (session == null || !session.IsReady)
            {
                return;
            }
            Camera view = Camera.main;
            if (session.Version != version)
            {
                version = session.Version;
                BuildSettlements();
                havePosts = false;
            }
            if (!show || view == null)
            {
                return;
            }
            Vector3 eye = view.transform.position;
            if (!havePosts || (eye - postsCentre).sqrMagnitude > 20f * 20f)
            {
                PlaceRoadPosts(eye);
            }
            // Names face the viewer.
            foreach (Transform label in labels)
            {
                Vector3 away = label.position - eye;
                away.y = 0f;
                if (away.sqrMagnitude < 2000f * 2000f && away.sqrMagnitude > 0.01f)
                {
                    label.rotation = Quaternion.LookRotation(away);
                }
            }
        }

        private void EnsureParts()
        {
            if (settlementRoot != null)
            {
                return;
            }
            block = new MaterialPropertyBlock();
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            if (markerMaterial == null)
            {
                markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Map marker" };
            }
            settlementRoot = new GameObject("Settlement markers").transform;
            settlementRoot.SetParent(transform, false);
            roadRoot = new GameObject("Road markers").transform;
            roadRoot.SetParent(transform, false);
            settlementRoot.gameObject.SetActive(show);
            roadRoot.gameObject.SetActive(show);
        }

        private GameObject Box(Transform parent, string boxName, Vector3 centre, Vector3 size, Color colour)
        {
            var go = new GameObject(boxName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = markerMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(meshRenderer, colour);
            return go;
        }

        private void Tint(Renderer target, Color colour)
        {
            block.SetColor(BaseColor, colour);
            target.SetPropertyBlock(block);
        }

        private void BuildSettlements()
        {
            EnsureParts();
            for (int i = settlementRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(settlementRoot.GetChild(i).gameObject);
            }
            labels.Clear();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (PlannedSettlement settlement in session.Plan.Settlements)
            {
                // Taller posts for larger places, so a capital stands out from a camp.
                float height = settlementPostHeight * Mathf.Lerp(0.6f, 1.6f, Mathf.Sqrt(settlement.Type.Parcels) / 5f);
                float ground = session.Map.Height(settlement.Position.x, settlement.Position.y);
                var site = new GameObject(settlement.Name).transform;
                site.SetParent(settlementRoot, false);
                site.localPosition = new Vector3(settlement.Position.x, ground, settlement.Position.y);
                Color colour = MapPicture.SettlementColour(settlement.Type);
                Box(site, "Post", new Vector3(0f, height * 0.5f - 1f, 0f), new Vector3(0.35f, height + 2f, 0.35f), new Color(0.3f, 0.2f, 0.12f));
                Box(site, "Banner", new Vector3(0.9f, height - 0.8f, 0f), new Vector3(1.6f, 1.2f, 0.08f), colour);

                var label = new GameObject("Name");
                label.transform.SetParent(site, false);
                label.transform.localPosition = new Vector3(0f, height + 1.4f, 0f);
                var text = label.AddComponent<TextMesh>();
                text.font = font;
                text.text = $"{settlement.Name}\n<size=40>{settlement.Type.Name}</size>";
                text.richText = true;
                text.fontSize = 64;
                text.characterSize = 0.16f;
                text.anchor = TextAnchor.LowerCenter;
                text.alignment = TextAlignment.Center;
                text.color = Color.white;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                labels.Add(label.transform);
            }
        }

        // Posts every few metres along each road within range of the viewer, reusing the posts from last time.
        private void PlaceRoadPosts(Vector3 eye)
        {
            EnsureParts();
            postsCentre = eye;
            havePosts = true;
            Vector3 local = session.WorldView.transform.InverseTransformPoint(eye);
            var centre = new Vector2(local.x, local.z);
            float rangeSquared = roadPostRange * roadPostRange;
            int used = 0;
            foreach (PlannedRoad road in session.Plan.Roads)
            {
                Color colour = MapPicture.RoadColour(road.Class);
                float postHeight = 1.2f;
                // Distance along the road so far, so posts keep their places as the viewer moves.
                float along = 0f;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 from = road.Points[i - 1], to = road.Points[i];
                    float length = Vector2.Distance(from, to);
                    Vector2 nearest = from + (to - from) * Mathf.Clamp01(Vector2.Dot(centre - from, to - from) / Mathf.Max(0.001f, length * length));
                    if ((nearest - centre).sqrMagnitude <= rangeSquared)
                    {
                        float first = Mathf.Ceil(along / roadPostSpacing) * roadPostSpacing - along;
                        for (float d = first; d < length; d += roadPostSpacing)
                        {
                            Vector2 at = Vector2.Lerp(from, to, d / length);
                            if ((at - centre).sqrMagnitude > rangeSquared)
                            {
                                continue;
                            }
                            GameObject post = used < roadPosts.Count
                                ? roadPosts[used]
                                : Box(roadRoot, "Road post", Vector3.zero, Vector3.one, colour);
                            if (used >= roadPosts.Count)
                            {
                                roadPosts.Add(post);
                            }
                            used++;
                            post.SetActive(true);
                            post.transform.localPosition = new Vector3(at.x, session.Map.Height(at.x, at.y) + postHeight * 0.5f - 0.2f, at.y);
                            // GL620: a narrow dirt path, wider cart paths; the paved one in cobble grey.
                            float postWidth = 0.18f * MapPicture.RoadWidth(road.Class);
                            post.transform.localScale = new Vector3(postWidth, postHeight + 0.4f, postWidth);
                            Tint(post.GetComponent<MeshRenderer>(), colour);
                        }
                    }
                    along += length;
                }
            }
            for (int i = used; i < roadPosts.Count; i++)
            {
                roadPosts[i].SetActive(false);
            }
            RoadPostCount = used;
        }
    }
}
