using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL847: a vessel's place on its wandering lane follows its place on the planned route, so the boat in the world stays
    /// beside its map marker.
    /// </summary>
    public class LaneTimetableTests
    {
        private static readonly List<Vector2> Route = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1000f, 0f) };

        // A lane that zigzags 40 m either side of the route: much longer than it.
        private static List<Vector2> Zigzag(float from, float to)
        {
            var lane = new List<Vector2>();
            for (float x = from; x <= to + 0.01f; x += 20f)
            {
                lane.Add(new Vector2(x, ((int)(x / 20f) % 2 == 0) ? 40f : -40f));
            }
            return lane;
        }

        private static Vector2 Along(List<Vector2> points, float distance)
        {
            return WaterLanes.Along(points, distance);
        }

        [Test]
        public void ZigzagLaneStaysBesideTheRoutePlace()
        {
            List<Vector2> lane = Zigzag(0f, 1000f);
            var timetable = new LaneTimetable(lane, Route);
            Assert.Greater(WaterLanes.Length(lane), 4000f, "The test lane must be far longer than the route.");
            for (float d = 0f; d <= 1000f; d += 50f)
            {
                Vector2 boat = Along(lane, timetable.LaneDistance(d));
                Assert.Less(Mathf.Abs(boat.x - d), 25f, $"At {d} m along the route the boat lies at x {boat.x}.");
            }
            // The old way, the same fraction of the lane, lies hundreds of metres from the marker halfway.
            Vector2 old = Along(lane, 0.5f * WaterLanes.Length(lane));
            Assert.Greater(Mathf.Abs(old.x - 500f), 25f, "The zigzag only tests anything if the old way was wrong.");
        }

        [Test]
        public void PartLaneWaitsAtItsEndsAndNeverGoesBack()
        {
            List<Vector2> lane = Zigzag(300f, 700f);
            var timetable = new LaneTimetable(lane, Route);
            Assert.AreEqual(0f, timetable.LaneDistance(100f));
            Assert.AreEqual(WaterLanes.Length(lane), timetable.LaneDistance(900f), 1e-3f);
            float last = -1f;
            for (float d = 0f; d <= 1000f; d += 10f)
            {
                float along = timetable.LaneDistance(d);
                Assert.GreaterOrEqual(along, last);
                last = along;
            }
            Assert.Less(Mathf.Abs(Along(lane, timetable.LaneDistance(500f)).x - 500f), 25f);
        }
    }
}
