using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// An item the player works the ground with (TL1, GL1): the hand, the pickaxe, later the
    /// shovel, trowel, rake and the rest of TL2. A tool hits the samples in its
    /// <see cref="Impact"/> shape, adding its <see cref="Power"/> to each one's damage;
    /// a sample breaks once its damage reaches its material's hardness (<see cref="ToolStrike"/>).
    /// </summary>
    /// <remarks>
    /// First pass: one power for every material. How well each tool suits each kind of
    /// material (the rake only on soft ground, the pickaxe best on rock) is TL4.
    /// </remarks>
    [CreateAssetMenu(fileName = "Tool", menuName = "Clube/Tool")]
    public class ToolDefinition : ItemDefinition
    {
        [Tooltip("Which samples around the aimed one each hit reaches.")]
        [SerializeField]
        private ToolImpact impact = ToolImpact.Corner;

        [Tooltip("Damage each hit adds to every sample it reaches, in hardness units (dirt = 1).")]
        [SerializeField, Min(0.01f)]
        private float power = 1f;

        [Tooltip("Hits per second while the use button is held.")]
        [SerializeField, Range(0.5f, 10f)]
        private float hitsPerSecond = 2f;

        /// <summary>Makes a tool in code (tests); the game's tools are assets.</summary>
        public static ToolDefinition Create(string id, string displayName, ToolImpact impact, float power, float hitsPerSecond = 2f)
        {
            ToolDefinition tool = Create<ToolDefinition>(id, displayName);
            tool.impact = impact;
            tool.power = Mathf.Max(0.01f, power);
            tool.hitsPerSecond = hitsPerSecond;
            return tool;
        }

        public ToolImpact Impact => impact;

        public float Power => power;

        public float HitsPerSecond => hitsPerSecond;
    }
}
