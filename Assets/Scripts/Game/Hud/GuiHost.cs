using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>Something the game draws with IMGUI: a player screen, or a notice that shows now and then (GL777).</summary>
    public interface IGuiDrawer
    {
        /// <summary>Like <c>GUI.depth</c>: drawn behind drawers with a lower depth. Most are 0.</summary>
        int GuiDepth { get; }

        /// <summary>Draws (or handles the event of) this frame's IMGUI pass; it returns at once when there is nothing to show.</summary>
        void DrawGui();
    }

    /// <summary>
    /// The game's single <c>OnGUI</c> (GL777). Unity calls <c>OnGUI</c> on every component that has one, several times a frame
    /// (layout, repaint, each input event), so some forty of them cost a frame's time even when every screen was closed. Screens
    /// register as <see cref="IGuiDrawer"/>s while enabled and this draws them in depth order (and hands events to the front
    /// one first, as Unity does); the always-on HUD is on a canvas (<see cref="HudCanvas"/>) and doesn't come here.
    /// </summary>
    public sealed class GuiHost : MonoBehaviour
    {
        private static readonly List<IGuiDrawer> Drawers = new List<IGuiDrawer>();
        private static GuiHost instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Drawers.Clear();
            instance = null;
        }

        // Made before the first scene loads: creating it from a drawer's OnEnable, mid-load, crashed the IL2CPP player.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance == null)
            {
                var go = new GameObject("GuiHost");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GuiHost>();
            }
        }

        /// <summary>Registers a drawer (from its <c>OnEnable</c>).</summary>
        public static void Add(IGuiDrawer drawer)
        {
            if (Drawers.Contains(drawer))
            {
                return;
            }
            // Keeps the list ordered back to front, and stable for equal depths.
            int at = Drawers.Count;
            while (at > 0 && Drawers[at - 1].GuiDepth < drawer.GuiDepth)
            {
                at--;
            }
            Drawers.Insert(at, drawer);
        }

        /// <summary>Unregisters a drawer (from its <c>OnDisable</c>).</summary>
        public static void Remove(IGuiDrawer drawer)
        {
            Drawers.Remove(drawer);
        }

        // Unity calls this, not an interface method: until GL781 the host had no OnGUI at all, so no screen ever drew or got input.
        private void OnGUI()
        {
            GameUi.Apply();
            DrawAll(Event.current.type == EventType.Repaint);
        }

        /// <summary>Draws every registered drawer: back to front on a repaint; front first for any other event, so input goes to the screen on top.</summary>
        public static void DrawAll(bool repaint)
        {
            if (repaint)
            {
                for (int i = 0; i < Drawers.Count; i++)
                {
                    Draw(Drawers[i]);
                }
                return;
            }
            // Input goes to the drawer in front first.
            for (int i = Drawers.Count - 1; i >= 0; i--)
            {
                if (i < Drawers.Count)
                {
                    Draw(Drawers[i]);
                }
            }
        }

        private static void Draw(IGuiDrawer drawer)
        {
            if (drawer is Object unity && unity == null)
            {
                return;
            }
            drawer.DrawGui();
        }
    }
}
