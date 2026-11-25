using System.Collections.Generic;
using Clube.Core;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class ReedRegrowScheduleTests
    {
        [Test]
        public void APickedReed_ComesBackWithinOneToThreeDays()
        {
            foreach (float roll in new[] { 0f, 0.5f, 1f })
            {
                var schedule = new ReedRegrowSchedule();
                var cell = new Vector2Int(4, -7);
                schedule.Picked(cell, 10d, roll);
                Assert.IsTrue(schedule.TryGetDue(cell, out double day));
                Assert.GreaterOrEqual(day, 11d - 1e-9);
                Assert.LessOrEqual(day, 13d + 1e-9);

                var due = new List<Vector2Int>();
                schedule.TakeDue(10.9d, due);
                Assert.AreEqual(0, due.Count, "Not before a day has passed.");
                schedule.TakeDue(13d, due);
                Assert.AreEqual(1, due.Count, "Back by three days at the latest.");
                Assert.AreEqual(0, schedule.Count);
            }
        }

        [Test]
        public void TheSchedule_SurvivesSaveAndLoad()
        {
            var schedule = new ReedRegrowSchedule();
            schedule.Picked(new Vector2Int(1, 2), 5.25d, 0.3f);
            schedule.Picked(new Vector2Int(-3, 9), 6.5d, 0.9f);
            var loaded = new ReedRegrowSchedule();
            loaded.Decode(schedule.Encode());
            Assert.AreEqual(2, loaded.Count);
            schedule.TryGetDue(new Vector2Int(-3, 9), out double expected);
            Assert.IsTrue(loaded.TryGetDue(new Vector2Int(-3, 9), out double day));
            Assert.AreEqual(expected, day, 1e-9);
            loaded.Decode("garbage;1,2");
            Assert.AreEqual(0, loaded.Count);
        }
    }
}
