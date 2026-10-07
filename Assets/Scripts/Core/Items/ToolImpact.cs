using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Which density samples ("corners") a tool hits around the one it's aimed at (TL1, GL1).
    /// Shapes are in samples, not metres, so a tool's reach on the ground follows the voxel
    /// size (M25).
    /// </summary>
    public enum ToolImpact
    {
        /// <summary>The aimed sample only: the hand, later the trowel.</summary>
        Corner,

        /// <summary>The aimed sample and its six face neighbours: the pickaxe.</summary>
        Plus,
    }

    /// <summary>The sample offsets of each <see cref="ToolImpact"/> shape.</summary>
    public static class ToolImpacts
    {
        private static readonly Vector3Int[] Corner = { Vector3Int.zero };

        private static readonly Vector3Int[] Plus =
        {
            Vector3Int.zero,
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>Offsets from the aimed sample, the aimed one (zero) first.</summary>
        public static IReadOnlyList<Vector3Int> Offsets(ToolImpact impact)
        {
            return impact == ToolImpact.Plus ? Plus : Corner;
        }
    }
}
