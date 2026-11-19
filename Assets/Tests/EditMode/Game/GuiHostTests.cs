using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Clube.Game.Tests
{
    public class GuiHostTests
    {
        private sealed class Recorder : IGuiDrawer
        {
            private readonly List<string> log;
            private readonly string name;

            public Recorder(string name, int depth, List<string> log)
            {
                this.name = name;
                GuiDepth = depth;
                this.log = log;
            }

            public int GuiDepth { get; }

            public void DrawGui()
            {
                log.Add(name);
            }
        }

        // GL781: the host shipped without an OnGUI, so no registered screen ever drew or saw an event (the tutorial's name
        // prompt, Esc, every screen). Unity calls the message by name, so only a lookup by name catches its loss.
        [Test]
        public void HostHasTheOnGuiUnityCalls()
        {
            MethodInfo onGui = typeof(GuiHost).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(onGui, "GuiHost needs an OnGUI message: nothing else makes Unity call the drawers.");
        }

        [Test]
        public void RepaintDrawsBackToFront()
        {
            var log = new List<string>();
            var front = new Recorder("front", 0, log);
            var back = new Recorder("back", -100, log);
            GuiHost.Add(front);
            GuiHost.Add(back);
            try
            {
                GuiHost.DrawAll(true);
                CollectionAssert.AreEqual(new[] { "back", "front" }, log);
            }
            finally
            {
                GuiHost.Remove(front);
                GuiHost.Remove(back);
            }
        }

        [Test]
        public void InputGoesToTheFrontFirst()
        {
            var log = new List<string>();
            var front = new Recorder("front", 0, log);
            var back = new Recorder("back", -100, log);
            GuiHost.Add(back);
            GuiHost.Add(front);
            try
            {
                GuiHost.DrawAll(false);
                CollectionAssert.AreEqual(new[] { "front", "back" }, log);
            }
            finally
            {
                GuiHost.Remove(front);
                GuiHost.Remove(back);
            }
        }

        [Test]
        public void ARemovedDrawerIsNotDrawn()
        {
            var log = new List<string>();
            var one = new Recorder("one", 0, log);
            GuiHost.Add(one);
            GuiHost.Remove(one);
            GuiHost.DrawAll(true);
            CollectionAssert.IsEmpty(log);
        }
    }
}
