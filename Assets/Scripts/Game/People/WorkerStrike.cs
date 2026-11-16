using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A worker's blow on the terrain (GL615): the player's own, through <see cref="ToolStrike"/> with the worker's tool
    /// and carried damage, so what an NPC digs or chops is a real edit of the world that takes as many hits as it would
    /// take the player. What comes away is counted as carried.
    /// </summary>
    public static class WorkerStrike
    {
        private static readonly List<Vector3Int> Targets = new List<Vector3Int>();
        private static readonly List<ItemDefinition> Collected = new List<ItemDefinition>();

        /// <summary>Strikes a voxel; <paramref name="reach"/> is what the tool reached.</summary>
        public static StrikeResult Hit(Villager worker, Vector3Int voxel, out VoxelReach reach)
        {
            WorldView view = worker.People.View;
            World world = view.World;
            float iso = view.Config.IsoLevel;
            ToolDefinition tool = worker.Tool;
            VoxelMaterial aimed = ToolStrike.AimedMaterial(world, voxel, iso, view.Config.Materials);
            reach = tool != null && tool.ReachesWideOn(aimed) ? tool.ImpactAround(voxel) : VoxelReach.Around(voxel, 1, false);
            if (tool == null)
            {
                return default;
            }
            ToolStrike.FindTargets(world, reach, iso, Targets);
            Collected.Clear();
            StrikeResult result = ToolStrike.Hit(world, Targets, tool, view.Config.Materials, worker.Damage, Collected, iso);
            worker.Carry(Collected.Count);
            return result;
        }
    }
}
