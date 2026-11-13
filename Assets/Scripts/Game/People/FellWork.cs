using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Felling (GL615): the worker walks out to the nearest standing tree outside the settlement and chops through its
    /// trunk with the duty's tool; the cut tree falls the way the player's does (<see cref="TreeFeller"/>), away from
    /// the worker, and lies there until it is cut up in turn. Then the next tree.
    /// </summary>
    public sealed class FellWork : IDutyWork
    {
        private const float StandOff = 0.9f;

        private Vector3Int cut;
        private bool hasCut;

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            SettlementPeople people = worker.People;
            World world = people.View.World;
            float iso = people.View.Config.IsoLevel;
            if (people.Trees == null)
            {
                return false;
            }
            var from = new Vector2(worker.At.x, worker.At.z);
            if (hasCut)
            {
                // The same trunk, wherever wood is left of it at chopping height.
                Vector3 at = world.Grid.SampleToWorld(cut);
                hasCut = TreeFinder.WoodNear(world, people.Trees, new Vector2(at.x, at.z), people.Ground, 0.8f, iso, null, out cut);
            }
            if (!hasCut)
            {
                Rect town = worker.Site.Bounds;
                float edge = Mathf.Max(town.width, town.height) * 0.5f;
                hasCut = TreeFinder.FindTrunk(world, people.View.Config.MeshSettings, people.Trees, town.center, edge + 4f, edge + worker.Duty.Range,
                    people.Ground, spot => worker.MayWorkAt(spot, 3f), worker.Random, 16, iso, out cut);
            }
            if (!hasCut)
            {
                return false;
            }
            face = world.Grid.SampleToWorld(cut);
            Vector2 away = from - new Vector2(face.x, face.z);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.right;
            stand = new Vector3(face.x + away.x * StandOff, face.y - TreeFinder.ChopHeight, face.z + away.y * StandOff);
            return true;
        }

        public void Stroke(Villager worker)
        {
            if (!hasCut)
            {
                return;
            }
            WorkerStrike.Hit(worker, cut, out VoxelReach reach);
            TreeFeller feller = worker.People.Feller;
            if (feller != null)
            {
                Vector3 at = worker.People.View.World.Grid.SampleToWorld(cut);
                float dx = at.x - worker.At.x, dz = at.z - worker.At.z;
                Vector3Int fall = Mathf.Abs(dx) >= Mathf.Abs(dz) ? new Vector3Int(dx >= 0f ? 1 : -1, 0, 0) : new Vector3Int(0, 0, dz >= 0f ? 1 : -1);
                feller.TryFell(reach.Box, fall);
            }
        }
    }
}
