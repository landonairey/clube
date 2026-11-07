using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What the player gets from bushes (GL237, GL238): a bush picked with Attack (<see cref="PlayerToolUser.BushPicked"/>)
    /// or broken by digging the ground under it near the player (<see cref="BushScatter.Broken"/>) gives its leaves
    /// and a stick or two (<see cref="BushGrowth.DropsOf"/>), handed over like anything a tool collects. While the view
    /// is on a bush in reach, a line under the dot says it can be picked.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class PlayerBushes : MonoBehaviour
    {
        [Tooltip("The item a bush's leaves come as.")]
        [SerializeField]
        private ItemDefinition leaves;

        [Tooltip("What the leaves are made of (the Leaves material), so they stack with leaves cut from trees.")]
        [SerializeField]
        private VoxelMaterial leafMaterial;

        [Tooltip("The item a bush's twigs come as.")]
        [SerializeField]
        private ItemDefinition stick;

        [Tooltip("A bush broken by digging gives its drops when it was this close to the player (m).")]
        [SerializeField, Min(0f)]
        private float brokenReach = 8f;

        private PlayerToolUser tools;
        private BushScatter scatter;
        private GUIStyle style;
        private GUIStyle shadow;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            tools.BushPicked += Give;
        }

        private void OnDisable()
        {
            tools.BushPicked -= Give;
            if (scatter != null)
            {
                scatter.Broken -= OnBroken;
                scatter = null;
            }
        }

        private void Update()
        {
            // The world's bushes, once the tools have a world.
            if (scatter == null && tools.WorldView != null)
            {
                scatter = tools.WorldView.GetComponent<BushScatter>();
                if (scatter != null)
                {
                    scatter.Broken += OnBroken;
                }
            }
        }

        private void OnBroken(Bush bush)
        {
            if ((bush.Centre - transform.position).sqrMagnitude <= brokenReach * brokenReach)
            {
                Give(bush);
            }
        }

        private void Give(Bush bush)
        {
            BushScatter bushes = scatter != null ? scatter : tools.WorldView != null ? tools.WorldView.GetComponent<BushScatter>() : null;
            if (bushes == null)
            {
                return;
            }
            bushes.Growth.DropsOf(bushes.Seed, bush.Cell, bush.Size, out int leafCount, out int stickCount);
            if (leaves != null && leafCount > 0)
            {
                Composition contents = leafMaterial != null ? Composition.From(new Content(leafMaterial, 1f)) : null;
                tools.Deliver(new ItemStack(leaves, leafCount, contents));
            }
            if (stick != null && stickCount > 0)
            {
                tools.Deliver(new ItemStack(stick, stickCount));
            }
        }

        private void OnGUI()
        {
            GameUi.Apply();
            if (!tools.isActiveAndEnabled || !tools.HasBush)
            {
                return;
            }
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.85f, 1f, 0.7f) } };
            shadow ??= new GUIStyle(style) { normal = { textColor = Color.black } };
            string text = $"{Describe(tools.AimedBush.Size)}: left click to pick";
            var area = new Rect(0f, Screen.height * 0.5f + 24f, Screen.width, 24f);
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, shadow);
            GUI.Label(area, text, style);
        }

        private static string Describe(BushSize size)
        {
            switch (size)
            {
                case BushSize.Small:
                    return "Small bush";
                case BushSize.Medium:
                    return "Bush";
                default:
                    return "Large bush";
            }
        }
    }
}
