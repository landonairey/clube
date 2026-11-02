using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The hand or tool the player holds in the right hand, drawn in the lower right of the view (GL269), and its swing: a
    /// pick or hammer drops straight down onto the aim, stops there and recoils back; a hatchet or hand axe does the same at
    /// an angle across the trunk; a shovel thrusts and scoops; a chisel taps; a bare hand swipes across. A swing plays on
    /// every use (<see cref="PlayerToolUser.Struck"/>, a hit that took nothing, a picked bush), shorter than the cooldown
    /// (GL270). Between swings it sways gently with breathing (quicker after running) and with the player's movement, which
    /// it trails a little. Hidden while a
    /// placeable is held or a screen is open. At dawn a note says tools are quicker (the early bird's bonus).
    /// </summary>
    public class HeldToolView : MonoBehaviour
    {
        // A swing's keyframes: time 0-1, then the offset from rest (position, metres in the camera's frame) and the turn
        // (degrees, x then y then z) at that time.
        private struct Key
        {
            public float Time;
            public Vector3 Offset;
            public Vector3 Turn;

            public Key(float time, Vector3 offset, Vector3 turn)
            {
                Time = time;
                Offset = offset;
                Turn = turn;
            }
        }

        // A pick or hammer: raised high, dropped straight down onto the aim, stopped there a moment by the ground, then
        // knocked back towards the player before settling.
        private static readonly Key[] Strike =
        {
            new Key(0f, Vector3.zero, Vector3.zero),
            new Key(0.28f, new Vector3(-0.02f, 0.14f, -0.05f), new Vector3(-58f, 0f, 0f)),
            new Key(0.42f, new Vector3(-0.07f, -0.06f, 0.16f), new Vector3(62f, -4f, 0f)),
            new Key(0.52f, new Vector3(-0.07f, -0.06f, 0.16f), new Vector3(60f, -4f, 0f)),
            new Key(0.66f, new Vector3(-0.03f, 0.03f, -0.04f), new Vector3(-14f, 0f, 0f)),
            new Key(1f, Vector3.zero, Vector3.zero),
        };

        // A hatchet or hand axe: the same drop and recoil, but coming in at an angle across the trunk.
        private static readonly Key[] Chop =
        {
            new Key(0f, Vector3.zero, Vector3.zero),
            new Key(0.28f, new Vector3(0.05f, 0.12f, -0.05f), new Vector3(-48f, 20f, -38f)),
            new Key(0.42f, new Vector3(-0.12f, -0.04f, 0.15f), new Vector3(50f, -22f, 30f)),
            new Key(0.52f, new Vector3(-0.12f, -0.04f, 0.15f), new Vector3(48f, -22f, 30f)),
            new Key(0.66f, new Vector3(-0.02f, 0.03f, -0.04f), new Vector3(-12f, 6f, -8f)),
            new Key(1f, Vector3.zero, Vector3.zero),
        };

        private static readonly Key[] Dig =
        {
            new Key(0f, Vector3.zero, Vector3.zero),
            new Key(0.35f, new Vector3(-0.03f, -0.08f, 0.16f), new Vector3(35f, 0f, 0f)),
            new Key(0.6f, new Vector3(-0.02f, 0.02f, 0.08f), new Vector3(-25f, 0f, 5f)),
            new Key(1f, Vector3.zero, Vector3.zero),
        };

        private static readonly Key[] Tap =
        {
            new Key(0f, Vector3.zero, Vector3.zero),
            new Key(0.3f, new Vector3(0f, 0.02f, -0.02f), new Vector3(-12f, 0f, 0f)),
            new Key(0.45f, new Vector3(-0.01f, -0.03f, 0.07f), new Vector3(15f, 0f, 0f)),
            new Key(1f, Vector3.zero, Vector3.zero),
        };

        // A bare hand: a swipe from the right across in front, low over the ground, then back.
        private static readonly Key[] Grab =
        {
            new Key(0f, Vector3.zero, Vector3.zero),
            new Key(0.22f, new Vector3(0.06f, -0.02f, 0.06f), new Vector3(10f, 25f, -15f)),
            new Key(0.5f, new Vector3(-0.26f, -0.08f, 0.16f), new Vector3(35f, -45f, 20f)),
            new Key(0.62f, new Vector3(-0.28f, -0.07f, 0.14f), new Vector3(30f, -50f, 22f)),
            new Key(1f, Vector3.zero, Vector3.zero),
        };

        [Tooltip("The Clube/Held Item material.")]
        [SerializeField]
        private Material material;

        [Tooltip("Where the held item rests in the camera's frame: right, down, forward (metres).")]
        [SerializeField]
        private Vector3 restPosition = new Vector3(0.24f, -0.26f, 0.52f);

        [Tooltip("How it's turned at rest (degrees).")]
        [SerializeField]
        private Vector3 restTurn = new Vector3(-8f, -14f, 6f);

        [Tooltip("Seconds a swing takes.")]
        [SerializeField, Range(0.15f, 0.8f)]
        private float swingSeconds = 0.42f;

        private readonly Dictionary<ItemDefinition, Mesh> meshes = new Dictionary<ItemDefinition, Mesh>();
        private PlayerToolUser toolUser;
        private PlayerController player;
        private CharacterController body;
        private Camera view;
        private GameObject held;
        private MeshFilter filter;
        private Material instance;
        private ItemDefinition shown;
        private Hotbar hotbar;
        private bool shownSet;
        private Key[] swing;
        private float swingTime = 1f;
        private float bobPhase;
        private float breathPhase;
        private float exertion;
        private Vector3 lag;
        private GUIStyle noteStyle;

        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>True while a swing plays.</summary>
        public bool Swinging => swingTime < 1f;

        private void Awake()
        {
            toolUser = GetComponent<PlayerToolUser>();
            player = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
            view = GetComponentInChildren<Camera>(true);
            hotbar = GetComponent<Hotbar>();
        }

        private void OnEnable()
        {
            if (toolUser != null)
            {
                toolUser.Struck += OnStruck;
                toolUser.Blocked += OnBlocked;
                toolUser.BushPicked += OnBushPicked;
            }
        }

        private void OnDisable()
        {
            if (toolUser != null)
            {
                toolUser.Struck -= OnStruck;
                toolUser.Blocked -= OnBlocked;
                toolUser.BushPicked -= OnBushPicked;
            }
            if (held != null)
            {
                held.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes.Values)
            {
                Destroy(mesh);
            }
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private void OnStruck(StrikeResult result)
        {
            Swing(SwingFor(toolUser.Current));
        }

        private void OnBlocked(string why)
        {
            Swing(SwingFor(toolUser.Current));
        }

        private void OnBushPicked(Bush bush)
        {
            Swing(Grab);
        }

        private void Swing(Key[] keys)
        {
            swing = keys;
            swingTime = 0f;
        }

        private static Key[] SwingFor(ToolDefinition tool)
        {
            switch (HeldToolMeshes.ShapeOf(tool))
            {
                case HeldShape.Pick:
                case HeldShape.Hammer:
                case HeldShape.Spear:
                    return Strike;
                case HeldShape.Hatchet:
                case HeldShape.HandAxe:
                    return Chop;
                case HeldShape.Shovel:
                    return Dig;
                case HeldShape.Chisel:
                    return Tap;
                default:
                    return Grab;
            }
        }

        private void LateUpdate()
        {
            if (view == null || material == null || toolUser == null)
            {
                return;
            }
            EnsureHeld();
            ToolDefinition tool = toolUser.Current;
            bool visible = tool != null && player != null && player.enabled && !player.IsInMenu;
            held.SetActive(visible);
            if (!visible)
            {
                return;
            }
            // A torch selected is held in place of the hand (GL287).
            ItemDefinition item = hotbar != null && hotbar.SelectedStack.Item is TorchDefinition torch ? torch : tool;
            if (!shownSet || item != shown)
            {
                shown = item;
                shownSet = true;
                filter.sharedMesh = MeshFor(item);
            }

            // The swing, eased between its keyframes.
            Vector3 offset = Vector3.zero, turn = Vector3.zero;
            if (swing != null && swingTime < 1f)
            {
                swingTime = Mathf.Min(1f, swingTime + Time.deltaTime / swingSeconds);
                Sample(swing, swingTime, out offset, out turn);
            }

            // Sway: breathing (slow when still, quicker and deeper after running), the step's bob while moving, and a
            // lag against the way the player moves, so the tool trails a little behind.
            Vector3 velocity = body != null ? body.velocity : Vector3.zero;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            float pace = Mathf.Clamp01(speed / 5f);
            exertion = Mathf.MoveTowards(exertion, pace, Time.deltaTime * (pace > exertion ? 0.4f : 0.08f));
            breathPhase += Time.deltaTime * TwoPi * Mathf.Lerp(0.22f, 0.5f, exertion);
            bobPhase += Time.deltaTime * Mathf.Lerp(0f, 10f, pace);
            float breath = Mathf.Sin(breathPhase) * Mathf.Lerp(0.004f, 0.009f, exertion);
            float step = Mathf.Lerp(0f, 0.013f, pace);
            offset += new Vector3(Mathf.Cos(bobPhase * 0.5f) * step * 0.7f, breath + Mathf.Sin(bobPhase) * step, 0f);
            Vector3 local = view.transform.InverseTransformDirection(velocity);
            lag = Vector3.Lerp(lag, local, 1f - Mathf.Exp(-6f * Time.deltaTime));
            offset -= new Vector3(lag.x, lag.y, lag.z * 0.5f) * 0.006f;
            turn += new Vector3(-lag.y * 0.8f + breath * 120f, 0f, lag.x * 1.5f);

            Transform t = held.transform;
            t.localPosition = restPosition + offset;
            t.localRotation = Quaternion.Euler(restTurn + turn);
        }

        private static void Sample(Key[] keys, float time, out Vector3 offset, out Vector3 turn)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                if (time <= keys[i].Time)
                {
                    Key a = keys[i - 1], b = keys[i];
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a.Time, b.Time, time));
                    offset = Vector3.Lerp(a.Offset, b.Offset, t);
                    turn = Vector3.Lerp(a.Turn, b.Turn, t);
                    return;
                }
            }
            offset = keys[keys.Length - 1].Offset;
            turn = keys[keys.Length - 1].Turn;
        }

        private void EnsureHeld()
        {
            if (held != null)
            {
                return;
            }
            instance = new Material(material) { name = material.name + " (held)" };
            held = new GameObject("Held tool");
            held.transform.SetParent(view.transform, false);
            filter = held.AddComponent<MeshFilter>();
            var renderer = held.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private Mesh MeshFor(ItemDefinition item)
        {
            if (!meshes.TryGetValue(item, out Mesh mesh))
            {
                mesh = item is TorchDefinition torch ? HeldToolMeshes.BuildTorch(torch.IsLit) : HeldToolMeshes.Build(item as ToolDefinition);
                meshes.Add(item, mesh);
            }
            return mesh;
        }

        private void OnGUI()
        {
            if (toolUser == null || player == null || !player.enabled || player.IsInMenu || !toolUser.EarlyBird)
            {
                return;
            }
            noteStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.85f, 0.45f) },
            };
            GUI.Label(new Rect(Screen.width * 0.5f - 160f, Screen.height - 96f, 320f, 20f), $"Early bird: tools 25% quicker until {Mathf.FloorToInt(ToolCooldown.DawnTo):00}:{Mathf.RoundToInt(ToolCooldown.DawnTo % 1f * 60f):00}", noteStyle);
        }
    }
}
