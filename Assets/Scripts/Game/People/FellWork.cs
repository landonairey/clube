using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Felling (GL615): the worker walks out to the nearest standing tree outside the settlement and chops through its
    /// trunk with the duty's tool; the cut tree falls the way the player's does (<see cref="TreeFeller"/>), away from
    /// the worker, and lies where it lands. Then the next tree, further out as the nearer ones go.
    /// </summary>
    public sealed class FellWork : IDutyWork
    {
        private const float StandOff = 0.9f;
        private const int SpotsASeek = 40;

        private readonly List<Vector2> spots = new List<Vector2>();
        private bool listed;
        private int cursor;
        private Vector3Int cut;
        private bool hasCut;

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            SettlementPeople people = worker.People;
            World world = people.View.World;
            float iso = people.View.Config.IsoLevel;
            if (people.Trees == null || people.View.Trees == null)
            {
                return false;
            }
            if (!listed)
            {
                listed = true;
                Rect town = worker.Site.Bounds;
                float edge = Mathf.Max(town.width, town.height) * 0.5f;
                TreeFinder.SpotsNear(people.View.Trees, town.center, 0f, edge + worker.Duty.Range, spots);
            }
            hasCut = TreeFinder.FindTrunk(world, people.Trees, spots, ref cursor, SpotsASeek, people.Ground, spot => worker.MayWorkAt(spot, 3f), iso, out cut);
            if (!hasCut)
            {
                return false;
            }
            face = world.Grid.SampleToWorld(cut);
            Vector2 away = new Vector2(worker.At.x, worker.At.z) - new Vector2(face.x, face.z);
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
