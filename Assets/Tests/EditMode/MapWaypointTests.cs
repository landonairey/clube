using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>The map waypoint (GL866) and where it sits on the minimap (GL867).</summary>
    public class MapWaypointTests
    {
        [Test]
        public void None_IsNotSet_AndEncodesEmpty()
        {
            Assert.IsFalse(MapWaypoint.None.IsSet);
            Assert.AreEqual("", MapWaypoint.None.Encode());
            Assert.IsFalse(MapWaypoint.Decode("").IsSet);
            Assert.IsFalse(MapWaypoint.Decode(null).IsSet);
        }

        [Test]
        public void Settlement_RoundTrips()
        {
            MapWaypoint back = MapWaypoint.Decode(MapWaypoint.AtSettlement(7, new Vector2(1234.5f, -20.25f)).Encode());
            Assert.IsTrue(back.IsSet);
            Assert.IsFalse(back.IsPin);
            Assert.AreEqual(7, back.Settlement);
            Assert.AreEqual(new Vector2(1234.5f, -20.25f), back.Position);
        }

        [Test]
        public void Pin_RoundTrips()
        {
            MapWaypoint back = MapWaypoint.Decode(MapWaypoint.Pin(new Vector2(0.1f, 4999.9f)).Encode());
            Assert.IsTrue(back.IsPin);
            Assert.AreEqual(-1, back.Settlement);
            Assert.AreEqual(0.1f, back.Position.x);
            Assert.AreEqual(4999.9f, back.Position.y);
        }

        [Test]
        public void Damaged_DecodesAsNone()
        {
            Assert.IsFalse(MapWaypoint.Decode("abc").IsSet);
            Assert.IsFalse(MapWaypoint.Decode("1;2").IsSet);
            Assert.IsFalse(MapWaypoint.Decode("1;x;3").IsSet);
        }

        [Test]
        public void InsideRange_SitsAtItsRealSpot()
        {
            // 24 m north of the player on a 48 m map 130 px from middle to edge: 65 px up.
            Vector2 at = MapWaypoint.OnMinimap(new Vector2(0f, 24f), 48f, 130f, 8f, out bool onRim);
            Assert.IsFalse(onRim);
            Assert.AreEqual(0f, at.x, 1e-4f);
            Assert.AreEqual(-65f, at.y, 1e-4f);
        }

        [Test]
        public void Far_IsHeldOnTheRim_AlongTheBearing()
        {
            // Far to the east: on the right edge, level with the player.
            Vector2 east = MapWaypoint.OnMinimap(new Vector2(1000f, 0f), 48f, 130f, 8f, out bool onRim);
            Assert.IsTrue(onRim);
            Assert.AreEqual(122f, east.x, 1e-3f);
            Assert.AreEqual(0f, east.y, 1e-3f);

            // Far to the south-west: the bottom-left corner, the bearing kept.
            Vector2 southWest = MapWaypoint.OnMinimap(new Vector2(-500f, -500f), 48f, 130f, 8f, out onRim);
            Assert.IsTrue(onRim);
            Assert.AreEqual(-122f, southWest.x, 1e-3f);
            Assert.AreEqual(122f, southWest.y, 1e-3f);

            // North-north-east: the top edge, the direction the same as the offset's.
            Vector2 offset = new Vector2(300f, 900f);
            Vector2 nne = MapWaypoint.OnMinimap(offset, 48f, 130f, 8f, out onRim);
            Assert.IsTrue(onRim);
            Assert.AreEqual(-122f, nne.y, 1e-3f);
            Assert.AreEqual(offset.x / offset.y, nne.x / -nne.y, 1e-4f);
        }
    }
}
