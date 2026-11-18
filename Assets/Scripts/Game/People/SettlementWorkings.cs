using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Every settlement on the map at work, loaded or not (GL730). How far each has got is worked out from the clock
    /// (<see cref="SettlementWorks"/>): nothing of it is written while its ground is away. As the chunks round a
    /// settlement load, the work done so far is put into them: its first claimed trees felled and taken away (their
    /// stumps left), its pits and tunnels dug out to the volume worked. What it has put in is saved as each
    /// settlement's progress, so a save does not do it twice; the clock is the game's own.
    /// </summary>
    /// <remarks>
    /// Near the viewer while the settlement's people are about, the work is theirs to do on screen and is left, so a
    /// tree does not vanish in front of the player. Land the player owns is never touched.
    /// </remarks>
    public sealed class SettlementWorkings : MonoBehaviour, ISavedValues
    {
        private const string KeyPrefix = "works.";

        // Settlements this far from the viewer (metres from their middle) are looked at: the loaded world, plus the
        // furthest a felling duty reaches out of a town.
        private const float LookRange = 600f;

        // Metres from the viewer within which a lived-in settlement's work is left to its people.
        private const float LeaveNear = 60f;

        // The most trees taken and voxels dug at one look, so a long absence is caught up over a few frames.
        private const int TreesAtOnce = 4, VoxelsAtOnce = 3000;

        // Milliseconds a frame for working out a settlement's trees and digs: more while the load curtain is up (GL762).
        private const float PrepareMilliseconds = 2f, PrepareBehindCurtainMilliseconds = 30f;

        private sealed class Progress
        {
            public SettlementWorks Works;
            public readonly List<bool> TreesDone = new List<bool>();
            public readonly Dictionary<DigSite, Cursor> Digs = new Dictionary<DigSite, Cursor>();
        }

        // Where a dig site's applied work has got to: the dig, the voxel in its plan, and the ground voxels applied.
        private sealed class Cursor
        {
            public int Dig, Voxel;
            public long Counted;
        }

        private readonly Dictionary<int, Progress> progress = new Dictionary<int, Progress>();
        private readonly Dictionary<string, string> restored = new Dictionary<string, string>();
        private SettlementPeople people;
        private PlayerParcels parcels;
        private WorldPlan plan;
        private float nextLook;
        // The settlement whose trees and digs are being worked out, a slice a frame (GL762).
        private SlicedWork preparing;
        private readonly FrameAllowance allowance = new FrameAllowance();

        /// <summary>Trees taken away and voxels dug out by the work done while nobody watched, this session.</summary>
        public int TreesTaken { get; private set; }

        public long VoxelsDug { get; private set; }

        private void Awake()
        {
            people = GetComponent<SettlementPeople>();
        }

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("Settlement workings");
            if (people == null || !people.IsRunning)
            {
                return;
            }
            // GL770: laying out one dig can take many frames' budget: the frames after it pay that back.
            bool hurry = people.View.Hurry;
            bool mayPrepare = allowance.MayWork(PrepareMilliseconds, Time.frameCount) && FrameBudget.HasRoom;
            if (preparing != null && (hurry || mayPrepare))
            {
                float started = Time.realtimeSinceStartup;
                using FrameCost.Scope prepareCost = FrameCost.Of("Settlement workings: prepare");
                bool done = preparing.Run(hurry ? PrepareBehindCurtainMilliseconds : PrepareMilliseconds);
                if (!hurry)
                {
                    allowance.Spend((Time.realtimeSinceStartup - started) * 1000f);
                }
                if (done)
                {
                    StopPreparing();
                    nextLook = 0f;
                }
            }
            if (Time.unscaledTime < nextLook)
            {
                return;
            }
            nextLook = Time.unscaledTime + 0.5f;
            if (!ReferenceEquals(plan, people.Plan))
            {
                plan = people.Plan;
                progress.Clear();
                StopPreparing();
                parcels = FindAnyObjectByType<PlayerParcels>();
            }
            double now = people.Now;
            int treeBudget = TreesAtOnce, voxelBudget = VoxelsAtOnce;
            IReadOnlyList<PlannedSettlement> settlements = people.Plan.Settlements;
            using (FrameCost.Of("Settlement workings: choose next"))
            {
                PrepareNearest(settlements);
            }
            using FrameCost.Scope applyCost = FrameCost.Of("Settlement workings: trees and digs");
            for (int i = 0; i < settlements.Count; i++)
            {
                if ((settlements[i].Position - people.Viewer).sqrMagnitude > LookRange * LookRange)
                {
                    continue;
                }
                Progress made = ProgressOf(i);
                if (made == null)
                {
                    continue;
                }
                bool lived = people.PeopleOf(i).Count > 0;
                ApplyTrees(made, now, lived, ref treeBudget);
                foreach (DigSite site in made.Works.Sites)
                {
                    ApplyDig(made, site, now, lived, ref voxelBudget);
                }
            }
        }

        private void OnDestroy()
        {
            StopPreparing();
        }

        private void StopPreparing()
        {
            preparing?.Dispose();
            preparing = null;
        }

        // Starts on the nearest settlement in reach whose trees and digs are not worked out yet: its people are the first seen.
        private void PrepareNearest(IReadOnlyList<PlannedSettlement> settlements)
        {
            if (preparing != null)
            {
                return;
            }
            SettlementWorks nearest = null;
            float best = LookRange * LookRange;
            for (int i = 0; i < settlements.Count; i++)
            {
                float distance = (settlements[i].Position - people.Viewer).sqrMagnitude;
                if (distance > best)
                {
                    continue;
                }
                SettlementWorks works = people.WorksOf(i);
                if (works != null && !works.IsPrepared)
                {
                    nearest = works;
                    best = distance;
                }
            }
            if (nearest != null)
            {
                preparing = new SlicedWork(nearest.PrepareSteps());
            }
        }

        private Progress ProgressOf(int settlement)
        {
            if (progress.TryGetValue(settlement, out Progress made))
            {
                return made;
            }
            SettlementWorks works = people.WorksOf(settlement);
            if (works == null || !works.IsPrepared)
            {
                return null;
            }
            made = new Progress { Works = works };
            progress[settlement] = made;
            if (restored.TryGetValue(KeyPrefix + settlement, out string saved))
            {
                Restore(made, saved);
            }
            return made;
        }

        // The first trees due that are not yet down: each taken away whole once every chunk it reaches is loaded.
        private void ApplyTrees(Progress made, double now, bool lived, ref int budget)
        {
            TreeMaterials trees = people.Trees;
            if (trees == null)
            {
                return;
            }
            int due = made.Works.TreesBy(now, people.DutyHours, people.Rates);
            IReadOnlyList<Vector2> spots = made.Works.Trees;
            World world = people.View.World;
            float iso = people.View.Config.IsoLevel;
            TreeRoom room = TreeRoom.Of(people.View.Config.Terrain.Trees);
            while (made.TreesDone.Count < due)
            {
                made.TreesDone.Add(false);
            }
            for (int i = 0; i < due && budget > 0; i++)
            {
                if (made.TreesDone[i])
                {
                    continue;
                }
                Vector2 spot = spots[i];
                if ((lived && (spot - people.Viewer).sqrMagnitude < LeaveNear * LeaveNear) || !Loaded(world, spot, room))
                {
                    continue;
                }
                made.TreesDone[i] = true;
                if (Owned(new Vector3(spot.x, 0f, spot.y)))
                {
                    continue;
                }
                // No wood there: the people on screen felled it already.
                if (TreeFinder.WoodNear(world, trees, spot, people.Ground, 1.5f, iso, out Vector3Int trunk)
                    && TreeFelling.ClearStanding(world, trunk, trees, iso) > 0)
                {
                    TreesTaken++;
                    budget--;
                }
            }
        }

        // A site's voxels in digging order, ground voxels counted, dug out up to the volume due.
        private void ApplyDig(Progress made, DigSite site, double now, bool lived, ref int budget)
        {
            if (!made.Digs.TryGetValue(site, out Cursor cursor))
            {
                cursor = new Cursor();
                made.Digs[site] = cursor;
            }
            long due = made.Works.VoxelsBy(site, now, people.DutyHours, people.Rates);
            World world = people.View.World;
            float iso = people.View.Config.IsoLevel;
            while (cursor.Counted < due && budget > 0 && site.TryGet(cursor.Dig, out Dig dig))
            {
                if (cursor.Voxel >= dig.Plan.Voxels.Count)
                {
                    cursor.Dig++;
                    cursor.Voxel = 0;
                    continue;
                }
                Vector3Int voxel = dig.Plan.Voxels[cursor.Voxel];
                if (dig.Ground[cursor.Voxel])
                {
                    float? density = world.GetDensity(voxel);
                    Vector3 at = world.Grid.SampleToWorld(voxel);
                    if (!density.HasValue || (lived && (new Vector2(at.x, at.z) - people.Viewer).sqrMagnitude < LeaveNear * LeaveNear))
                    {
                        return;
                    }
                    if (density.Value >= iso && !Owned(at))
                    {
                        world.SetDensity(voxel, 0f);
                        VoxelsDug++;
                    }
                    cursor.Counted++;
                    budget--;
                }
                cursor.Voxel++;
            }
        }

        // True when every chunk a tree at a spot may reach is loaded (GL652: a tree cut with part of it away leaves that part hanging).
        private bool Loaded(World world, Vector2 spot, TreeRoom room)
        {
            WorldGrid grid = world.Grid;
            float ground = people.Ground(spot.x, spot.y);
            float spread = Mathf.Max(1f, room.Spread);
            if (!world.IsLoaded(grid.WorldToChunk(new Vector3(spot.x, ground, spot.y))))
            {
                return false;
            }
            Vector3Int min = grid.WorldToChunk(new Vector3(spot.x - spread, Mathf.Max(0f, ground - 1f), spot.y - spread));
            Vector3Int max = grid.WorldToChunk(new Vector3(spot.x + spread, ground + Mathf.Max(2f, room.Height), spot.y + spread));
            for (int y = Mathf.Max(0, min.y); y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!world.IsLoaded(new Vector3Int(x, y, z)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        // True on land the player owns (the world's own metres).
        private bool Owned(Vector3 local)
        {
            return parcels != null && parcels.Parcels != null && parcels.Parcels.IsOwnedAt(local);
        }

        // Saved as "trees|site=counted,site=counted": the trees as runs of done ones ("0-11.14.16-20"), each site by its place in the works' list.
        private static string Save(Progress made)
        {
            var text = new StringBuilder();
            int run = -1;
            for (int i = 0; i <= made.TreesDone.Count; i++)
            {
                bool done = i < made.TreesDone.Count && made.TreesDone[i];
                if (done && run < 0)
                {
                    run = i;
                }
                else if (!done && run >= 0)
                {
                    if (text.Length > 0)
                    {
                        text.Append('.');
                    }
                    text.Append(run.ToString(CultureInfo.InvariantCulture));
                    if (i - 1 > run)
                    {
                        text.Append('-').Append((i - 1).ToString(CultureInfo.InvariantCulture));
                    }
                    run = -1;
                }
            }
            text.Append('|');
            IReadOnlyList<DigSite> sites = made.Works.Sites;
            for (int s = 0; s < sites.Count; s++)
            {
                if (made.Digs.TryGetValue(sites[s], out Cursor cursor) && cursor.Counted > 0)
                {
                    text.Append(s.ToString(CultureInfo.InvariantCulture)).Append('=').Append(cursor.Counted.ToString(CultureInfo.InvariantCulture)).Append(',');
                }
            }
            return text.ToString();
        }

        private static void Restore(Progress made, string saved)
        {
            string[] halves = saved.Split('|');
            foreach (string part in halves[0].Split('.'))
            {
                if (part.Length == 0)
                {
                    continue;
                }
                string[] ends = part.Split('-');
                if (!int.TryParse(ends[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int from))
                {
                    continue;
                }
                int to = from;
                if (ends.Length > 1 && !int.TryParse(ends[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out to))
                {
                    continue;
                }
                for (int i = from; i <= to && i < 1000000; i++)
                {
                    while (made.TreesDone.Count <= i)
                    {
                        made.TreesDone.Add(false);
                    }
                    made.TreesDone[i] = true;
                }
            }
            if (halves.Length < 2)
            {
                return;
            }
            IReadOnlyList<DigSite> sites = made.Works.Sites;
            foreach (string pair in halves[1].Split(','))
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2 && int.TryParse(kv[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) && s < sites.Count
                    && long.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long counted))
                {
                    made.Digs[sites[s]] = Skip(sites[s], counted);
                }
            }
        }

        // A cursor moved past so many ground voxels of a site without digging: what an earlier session put in.
        private static Cursor Skip(DigSite site, long counted)
        {
            var cursor = new Cursor();
            while (cursor.Counted < counted && site.TryGet(cursor.Dig, out Dig dig))
            {
                if (cursor.Voxel == 0 && cursor.Counted + dig.GroundVoxels <= counted)
                {
                    cursor.Counted += dig.GroundVoxels;
                    cursor.Dig++;
                    continue;
                }
                if (cursor.Voxel >= dig.Ground.Length)
                {
                    cursor.Dig++;
                    cursor.Voxel = 0;
                    continue;
                }
                if (dig.Ground[cursor.Voxel])
                {
                    cursor.Counted++;
                }
                cursor.Voxel++;
            }
            return cursor;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            // Settlements not looked at this session keep what an earlier one saved.
            foreach (KeyValuePair<string, string> pair in restored)
            {
                values[pair.Key] = pair.Value;
            }
            foreach (KeyValuePair<int, Progress> made in progress)
            {
                string saved = Save(made.Value);
                if (saved != "|")
                {
                    values[KeyPrefix + made.Key] = saved;
                }
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            restored.Clear();
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (pair.Key.StartsWith(KeyPrefix, System.StringComparison.Ordinal))
                {
                    restored[pair.Key] = pair.Value;
                }
            }
            progress.Clear();
        }
    }
}
