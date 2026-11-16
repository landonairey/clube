using System.Collections.Generic;
using System.Text;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The world map (GL599), a player screen on M: the scene's bounded map (<see cref="MapSession"/>) as a picture of
    /// its terrain, biomes and water (<see cref="MapPicture"/>, drawn once per map), with each side's boundary kind,
    /// the roads by class, the water routes, every settlement's icon (<see cref="MapIcons"/>) and name, and the player.
    /// The wheel zooms on the pointer and a drag pans. The lab layer adds a resource of the survey and, under the
    /// pointer, why a site and its shops were picked (ST7). In a scene with no map session the key does nothing.
    /// </summary>
    public class WorldMapScreen : MonoBehaviour
    {
        private const float MaxZoom = 12f;

        [Tooltip("Opens and closes the map (M).")]
        [SerializeField]
        private InputActionReference toggleAction;

        [Tooltip("Pixels a side of the map's picture.")]
        [SerializeField, Range(256, 2048)]
        private int resolution = 1024;

        [Tooltip("Lab layer: a resource of the survey, and why each site and shop was picked.")]
        [SerializeField]
        private bool labLayer;

        private PlayerController player;
        private bool hasPauseMenu;
        private Texture2D picture;
        private int pictureVersion = -1;
        private Material shapes;
        private List<SurveyResource> resources;
        private float[] resourceAmounts;
        private float resourceMax;
        private int resourceShown = -1, resourceBuilt = -2;
        private float zoom = 1f;
        private Vector2 centre = new Vector2(0.5f, 0.5f);
        private Rect view;
        private int openedFrame = -1;
        private GUIStyle nameStyle, noteStyle, sideStyle;

        public bool IsOpen { get; private set; }

        /// <summary>Whether the lab layer (resources and reasons) is drawn.</summary>
        public bool LabLayer
        {
            get => labLayer;
            set => labLayer = value;
        }

        /// <summary>Seconds the map's picture took to draw, last time.</summary>
        public float LastPictureSeconds { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void Start()
        {
            // With a pause menu in the scene, Esc reaches the map through it (GL204).
            hasPauseMenu = FindFirstObjectByType<PauseMenu>() != null;
        }

        private void OnEnable()
        {
            toggleAction?.action.Enable();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            if (picture != null)
            {
                Destroy(picture);
            }
            if (shapes != null)
            {
                Destroy(shapes);
            }
        }

        private void Update()
        {
            if (PauseMenu.IsPaused || (player != null && !player.enabled))
            {
                return;
            }
            if (IsOpen)
            {
                bool escape = !hasPauseMenu && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                if (escape || (toggleAction != null && toggleAction.action.WasPressedThisFrame() && openedFrame != Time.frameCount))
                {
                    Close();
                }
                return;
            }
            MapSession session = MapSession.Current;
            if (toggleAction == null || !toggleAction.action.WasPressedThisFrame() || session == null || !session.IsReady)
            {
                return;
            }
            // One screen at a time.
            if (player != null && (player.IsInMenu || player.MenuClosedThisFrame))
            {
                return;
            }
            Open();
        }

        /// <summary>Opens the map centred on the whole map; does nothing without a ready map session.</summary>
        public void Open()
        {
            MapSession session = MapSession.Current;
            if (IsOpen || session == null || !session.IsReady)
            {
                return;
            }
            IsOpen = true;
            openedFrame = Time.frameCount;
            if (player != null)
            {
                player.IsInMenu = true;
            }
        }

        /// <summary>Closes the map (M, or Esc).</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            if (player != null)
            {
                player.IsInMenu = false;
            }
        }

        private void EnsurePicture(MapSession session)
        {
            if (picture != null && pictureVersion == session.Version)
            {
                return;
            }
            float start = Time.realtimeSinceStartup;
            if (picture == null || picture.width != resolution)
            {
                if (picture != null)
                {
                    Destroy(picture);
                }
                picture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false)
                {
                    name = "World map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
            }
            picture.SetPixels32(MapPicture.Render(session.Map, session.Survey, resolution));
            picture.Apply(false);
            pictureVersion = session.Version;
            resources = SurveyResource.For(session.Survey);
            resourceBuilt = -2;
            resourceShown = Mathf.Min(resourceShown, resources.Count - 1);
            zoom = 1f;
            centre = new Vector2(0.5f, 0.5f);
            LastPictureSeconds = Time.realtimeSinceStartup - start;
        }

        private void EnsureStyles()
        {
            if (nameStyle != null && nameStyle.fontSize == GameUi.Font(14))
            {
                return;
            }
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = GameUi.Font(14), fontStyle = FontStyle.Bold, wordWrap = false };
            nameStyle.normal.textColor = Color.white;
            sideStyle = new GUIStyle(nameStyle) { alignment = TextAnchor.MiddleCenter, fontSize = GameUi.Font(16) };
            noteStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = GameUi.Font(13), wordWrap = true };
            noteStyle.normal.textColor = Color.white;
        }

        // Map fraction (0..1 each way, y north) to screen pixels and back.
        private Vector2 ToScreen(Vector2 fraction)
        {
            return new Vector2(
                view.center.x + (fraction.x - centre.x) * view.width * zoom,
                view.center.y - (fraction.y - centre.y) * view.height * zoom);
        }

        private Vector2 ToFraction(Vector2 screen)
        {
            return new Vector2(
                centre.x + (screen.x - view.center.x) / (view.width * zoom),
                centre.y - (screen.y - view.center.y) / (view.height * zoom));
        }

        private Vector2 ToScreen(Rect area, Vector2 position)
        {
            return ToScreen(new Vector2((position.x - area.xMin) / area.width, (position.y - area.yMin) / area.height));
        }

        private void ClampCentre()
        {
            float half = 0.5f / zoom;
            centre.x = Mathf.Clamp(centre.x, half, 1f - half);
            centre.y = Mathf.Clamp(centre.y, half, 1f - half);
        }

        private void OnGUI()
        {
            MapSession session = MapSession.Current;
            if (!IsOpen || session == null || !session.IsReady)
            {
                return;
            }
            EnsurePicture(session);
            EnsureStyles();
            GUI.depth = -10;
            Rect area = session.Layout.Bounds;

            float side = Mathf.Min(Screen.height * 0.9f, Screen.width * 0.9f);
            view = new Rect((Screen.width - side) * 0.5f, (Screen.height - side) * 0.5f, side, side);
            HandleInput();

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float span = 1f / zoom;
            GUI.DrawTextureWithTexCoords(view, picture, new Rect(centre.x - span * 0.5f, centre.y - span * 0.5f, span, span));

            if (Event.current.type == EventType.Repaint)
            {
                DrawShapes(session, area);
            }
            DrawSides(session);
            PlannedSettlement hovered = DrawNames(session, area);
            DrawNotes(session, hovered);
        }

        private void HandleInput()
        {
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
            {
                // Zoom about the pointer: the place under it stays under it.
                Vector2 under = ToFraction(e.mousePosition);
                zoom = Mathf.Clamp(zoom * (e.delta.y > 0f ? 1f / 1.25f : 1.25f), 1f, MaxZoom);
                Vector2 now = ToFraction(e.mousePosition);
                centre += under - now;
                ClampCentre();
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                centre.x -= e.delta.x / (view.width * zoom);
                centre.y += e.delta.y / (view.height * zoom);
                ClampCentre();
                e.Use();
            }
        }

        // ---- Shapes: roads, routes, icons, the player, drawn with GL over the picture ----

        private void DrawShapes(MapSession session, Rect area)
        {
            if (shapes == null)
            {
                shapes = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                shapes.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                shapes.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                shapes.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                shapes.SetInt("_ZWrite", 0);
                shapes.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            shapes.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            GL.Begin(GL.TRIANGLES);
            float scale = view.width / 750f;

            if (labLayer)
            {
                DrawResource(session, area);
            }
            foreach (WaterRoute route in session.Plan.WaterRoutes)
            {
                Path(area, route.Points, 1.2f * scale, MapPicture.RouteColour(route.Kind), 6f * scale);
            }
            for (RoadClass roadClass = RoadClass.Footpath; roadClass <= RoadClass.PavedCartPath; roadClass++)
            {
                foreach (PlannedRoad road in session.Plan.Roads)
                {
                    if (road.Class != roadClass)
                    {
                        continue;
                    }
                    float width = MapPicture.RoadWidth(roadClass) * scale * Mathf.Lerp(1f, 2f, (zoom - 1f) / (MaxZoom - 1f));
                    Path(area, road.Points, width, MapPicture.RoadColour(roadClass), 0f);
                    foreach (Clube.Core.RoadBridge bridge in road.Bridges)
                    {
                        Vector2 at = ToScreen(area, bridge.Position);
                        if (view.Contains(at))
                        {
                            Quad(at, width + 2f * scale, MapPicture.Bridge);
                        }
                    }
                }
            }
            foreach (PlannedSettlement settlement in session.Plan.Settlements)
            {
                Vector2 at = ToScreen(area, settlement.Position);
                if (!view.Contains(at))
                {
                    continue;
                }
                float size = IconSize(settlement) * scale;
                Vector2[] icon = MapIcons.Triangles(settlement.Type.Icon);
                Shape(icon, at, size + 2.5f * scale, 0f, Color.black);
                Shape(icon, at, size, 0f, MapPicture.SettlementColour(settlement.Type));
            }
            DrawPlayer(session, area, scale);
            GL.End();
            GL.PopMatrix();
        }

        private static float IconSize(PlannedSettlement settlement)
        {
            return 7f + 1.6f * Mathf.Sqrt(settlement.Type.Parcels);
        }

        private static readonly Vector2[] Arrow =
        {
            new Vector2(0f, 1.2f), new Vector2(-0.8f, -0.9f), new Vector2(0f, -0.4f),
            new Vector2(0f, 1.2f), new Vector2(0f, -0.4f), new Vector2(0.8f, -0.9f),
        };

        private void DrawPlayer(MapSession session, Rect area, float scale)
        {
            Transform who = player != null ? player.transform : transform;
            Vector3 local = session.WorldView.transform.InverseTransformPoint(who.position);
            Vector2 at = ToScreen(area, new Vector2(local.x, local.z));
            if (!view.Contains(at))
            {
                return;
            }
            float yaw = who.eulerAngles.y;
            Shape(Arrow, at, 11f * scale, yaw, Color.black);
            Shape(Arrow, at, 8f * scale, yaw, new Color(1f, 0.25f, 0.2f));
        }

        // The survey's amount of the chosen resource per cell, as squares fading with the amount.
        private void DrawResource(MapSession session, Rect area)
        {
            ResourceSurvey survey = session.Survey;
            if (resourceShown < 0 || resources == null || resourceShown >= resources.Count)
            {
                return;
            }
            int size = survey.Size;
            if (resourceBuilt != resourceShown)
            {
                resourceBuilt = resourceShown;
                resourceAmounts = new float[size * size];
                resourceMax = 0f;
                SurveyResource resource = resources[resourceShown];
                for (int i = 0; i < resourceAmounts.Length; i++)
                {
                    resourceAmounts[i] = resource.Amount(survey, i % size, i / size);
                    resourceMax = Mathf.Max(resourceMax, resourceAmounts[i]);
                }
            }
            if (resourceMax <= 0f)
            {
                return;
            }
            float cell = view.width * zoom / size;
            var colour = new Color(1f, 0.3f, 0.05f);
            for (int i = 0; i < resourceAmounts.Length; i++)
            {
                float amount = resourceAmounts[i] / resourceMax;
                if (amount <= 0.02f)
                {
                    continue;
                }
                Vector2 at = ToScreen(new Vector2((i % size + 0.5f) / size, (i / size + 0.5f) / size));
                if (view.Contains(at))
                {
                    colour.a = Mathf.Lerp(0.25f, 0.9f, amount);
                    Quad(at, Mathf.Max(2f, cell * Mathf.Lerp(0.35f, 0.8f, amount)), colour);
                }
            }
        }

        private void Path(Rect area, List<Vector2> points, float width, Color colour, float dash)
        {
            GL.Color(colour);
            float drawn = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 from = ToScreen(area, points[i - 1]), to = ToScreen(area, points[i]);
                float length = Vector2.Distance(from, to);
                if (length < 0.01f)
                {
                    continue;
                }
                if (dash <= 0f)
                {
                    Segment(from, to, width);
                }
                else
                {
                    // Dashes keep their phase along the whole path.
                    for (float d = -(drawn % (2f * dash)); d < length; d += 2f * dash)
                    {
                        float a = Mathf.Max(0f, d), b = Mathf.Min(length, d + dash);
                        if (b > a)
                        {
                            Segment(Vector2.Lerp(from, to, a / length), Vector2.Lerp(from, to, b / length), width);
                        }
                    }
                }
                drawn += length;
            }
        }

        // A thick line, cut to the map's frame (Liang-Barsky).
        private void Segment(Vector2 from, Vector2 to, float width)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = to - from;
            if (!Clip(-d.x, from.x - view.xMin, ref t0, ref t1) || !Clip(d.x, view.xMax - from.x, ref t0, ref t1)
                || !Clip(-d.y, from.y - view.yMin, ref t0, ref t1) || !Clip(d.y, view.yMax - from.y, ref t0, ref t1))
            {
                return;
            }
            Vector2 a = from + d * t0, b = from + d * t1;
            Vector2 across = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            GL.Vertex3(a.x - across.x, a.y - across.y, 0f);
            GL.Vertex3(a.x + across.x, a.y + across.y, 0f);
            GL.Vertex3(b.x + across.x, b.y + across.y, 0f);
            GL.Vertex3(a.x - across.x, a.y - across.y, 0f);
            GL.Vertex3(b.x + across.x, b.y + across.y, 0f);
            GL.Vertex3(b.x - across.x, b.y - across.y, 0f);
        }

        private static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(p, 0f))
            {
                return q >= 0f;
            }
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                {
                    return false;
                }
                t0 = Mathf.Max(t0, r);
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }
                t1 = Mathf.Min(t1, r);
            }
            return true;
        }

        private static void Quad(Vector2 at, float size, Color colour)
        {
            float h = size * 0.5f;
            GL.Color(colour);
            GL.Vertex3(at.x - h, at.y - h, 0f);
            GL.Vertex3(at.x + h, at.y - h, 0f);
            GL.Vertex3(at.x + h, at.y + h, 0f);
            GL.Vertex3(at.x - h, at.y - h, 0f);
            GL.Vertex3(at.x + h, at.y + h, 0f);
            GL.Vertex3(at.x - h, at.y + h, 0f);
        }

        // Triangles in a (-1..1) square with y up, turned clockwise by yaw (degrees, 0 = north) about the centre.
        private static void Shape(Vector2[] triangles, Vector2 at, float size, float yaw, Color colour)
        {
            float radians = yaw * Mathf.Deg2Rad, sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            GL.Color(colour);
            foreach (Vector2 p in triangles)
            {
                float x = p.x * cos + p.y * sin, y = -p.x * sin + p.y * cos;
                GL.Vertex3(at.x + x * size, at.y - y * size, 0f);
            }
        }

        // ---- Words: the sides' kinds, names, and the lab's notes ----

        private void DrawSides(MapSession session)
        {
            MapLayout layout = session.Layout;
            float w = 340f, h = 26f;
            Side(layout, MapSide.North, new Rect(view.center.x - w * 0.5f, view.yMin + 4f, w, h));
            Side(layout, MapSide.South, new Rect(view.center.x - w * 0.5f, view.yMax - h - 4f, w, h));
            Side(layout, MapSide.West, new Rect(view.xMin + 6f, view.center.y - h * 0.5f, w, h), TextAnchor.MiddleLeft);
            Side(layout, MapSide.East, new Rect(view.xMax - w - 6f, view.center.y - h * 0.5f, w, h), TextAnchor.MiddleRight);
        }

        private void Side(MapLayout layout, MapSide side, Rect rect, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            sideStyle.alignment = anchor;
            GameUi.OutlinedLabel(rect, $"{side}: {layout.Boundary(side).Name}", sideStyle);
        }

        private PlannedSettlement DrawNames(MapSession session, Rect area)
        {
            PlannedSettlement hovered = null;
            float scale = view.width / 750f;
            Vector2 pointer = Event.current.mousePosition;
            float nearest = float.MaxValue;
            foreach (PlannedSettlement settlement in session.Plan.Settlements)
            {
                Vector2 at = ToScreen(area, settlement.Position);
                if (!view.Contains(at))
                {
                    continue;
                }
                float size = IconSize(settlement) * scale;
                float distance = Vector2.Distance(pointer, at);
                if (distance < size * 1.5f && distance < nearest)
                {
                    nearest = distance;
                    hovered = settlement;
                }
                // The smallest places are named once zoomed in, so the whole map stays readable.
                if (settlement.Type.Parcels >= 4f || zoom >= 2f)
                {
                    GameUi.OutlinedLabel(new Rect(at.x - 120f, at.y + size + 1f, 240f, 22f), settlement.Name, nameStyle);
                }
            }
            return hovered;
        }

        private void DrawNotes(MapSession session, PlannedSettlement hovered)
        {
            var hint = new Rect(view.xMin, view.yMax + 2f, view.width, 22f);
            noteStyle.alignment = TextAnchor.UpperCenter;
            GUI.Label(hint, "M or Esc: close  ·  wheel: zoom  ·  drag: pan", noteStyle);
            noteStyle.alignment = TextAnchor.UpperLeft;

            if (labLayer && resources != null)
            {
                // The resource shown, picked from a column of buttons left of the map.
                float x = Mathf.Max(4f, view.xMin - 134f), y = view.yMin;
                if (GUI.Toggle(new Rect(x, y, 130f, 20f), resourceShown < 0, " No resource") && resourceShown >= 0)
                {
                    resourceShown = -1;
                }
                for (int i = 0; i < resources.Count; i++)
                {
                    y += 21f;
                    if (GUI.Toggle(new Rect(x, y, 130f, 20f), resourceShown == i, " " + resources[i].Name) && resourceShown != i)
                    {
                        resourceShown = i;
                    }
                }
            }
            if (hovered == null)
            {
                return;
            }
            var text = new StringBuilder();
            text.Append("<b>").Append(hovered.Name).Append("</b>  ").Append(hovered.Type.Name).Append(", about ").Append(hovered.Population).AppendLine(" people");
            if (labLayer)
            {
                foreach (string reason in hovered.Reasons)
                {
                    text.Append("· ").AppendLine(reason);
                }
                foreach (PlannedShop shop in hovered.Shops)
                {
                    text.Append("Shop: ").Append(shop.Specialty != null ? shop.Specialty.Name : "?");
                    if (!string.IsNullOrEmpty(shop.Reason))
                    {
                        text.Append(" (").Append(shop.Reason).Append(')');
                    }
                    text.AppendLine();
                }
            }
            noteStyle.richText = true;
            var content = new GUIContent(text.ToString().TrimEnd());
            float width = labLayer ? 420f : 280f;
            float height = noteStyle.CalcHeight(content, width - 16f) + 12f;
            Vector2 pointer = Event.current.mousePosition;
            var box = new Rect(Mathf.Min(pointer.x + 18f, Screen.width - width - 4f), Mathf.Min(pointer.y + 18f, Screen.height - height - 4f), width, height);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f), content, noteStyle);
        }
    }
}
