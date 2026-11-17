using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// GL718: hands every settlement of the bounded map to the far view (<see cref="DistantBuildingView"/>) as low-poly
    /// stand-ins made from its building plan (<see cref="MapSettlements.LaidSite"/>, <see cref="BuildingStandIns"/>), so
    /// settlements show from far away. Settlements are laid out a few milliseconds a frame, nearest the camera first.
    /// </summary>
    public class MapStandIns : MonoBehaviour
    {
        [SerializeField]
        private MapSession session;

        [SerializeField]
        private MapSettlements settlements;

        [SerializeField]
        private DistantBuildingView view;

        [Tooltip("Milliseconds a frame settlements may take to be laid out for their stand-ins.")]
        [SerializeField, Range(0.5f, 20f)]
        private float budgetMilliseconds = 4f;

        private readonly List<int> queue = new List<int>();
        private readonly List<StandInPart> parts = new List<StandInPart>();
        private int version = -1;

        /// <summary>Settlements still to be handed to the view.</summary>
        public int Pending => queue.Count;

        private void Update()
        {
            if (session == null || settlements == null || view == null || !session.IsReady || session.Plan == null)
            {
                return;
            }
            if (session.Version != version)
            {
                StartOver();
            }
            float started = Time.realtimeSinceStartup;
            float voxel = session.WorldView.World.Grid.VoxelSize;
            while (queue.Count > 0 && (Time.realtimeSinceStartup - started) * 1000f < budgetMilliseconds)
            {
                int index = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                SettlementSite site = settlements.LaidSite(index);
                if (site == null)
                {
                    continue;
                }
                parts.Clear();
                BuildingStandIns.Collect(site, voxel, parts);
                Vector2 middle = site.Settlement.Position;
                float floor = site.Buildings.Count > 0 ? site.Buildings[0].Pad : 0f;
                view.SetSettlement(index, new Vector3(middle.x, floor, middle.y), parts);
            }
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.Clear();
            }
            version = -1;
        }

        // Every settlement queued, the farthest first in the list so the nearest is taken first from its end.
        private void StartOver()
        {
            version = session.Version;
            view.Clear();
            queue.Clear();
            Camera camera = Camera.main;
            Vector3 eye = camera != null ? session.WorldView.transform.InverseTransformPoint(camera.transform.position) : Vector3.zero;
            var from = new Vector2(eye.x, eye.z);
            IReadOnlyList<PlannedSettlement> planned = session.Plan.Settlements;
            for (int i = 0; i < planned.Count; i++)
            {
                queue.Add(i);
            }
            queue.Sort((a, b) => (planned[b].Position - from).sqrMagnitude.CompareTo((planned[a].Position - from).sqrMagnitude));
        }
    }
}
