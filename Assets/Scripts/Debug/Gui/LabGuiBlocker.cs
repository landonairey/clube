using System.Collections.Generic;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Screen areas covered by lab GUI panels, so click tools (voxel selection, the
    /// terrain brush) ignore clicks meant for a panel. Panels report their rectangle
    /// from <c>OnGUI</c> and clear it when hidden; tools ask before acting on a click.
    /// </summary>
    public static class LabGuiBlocker
    {
        private static readonly Dictionary<Object, Rect> Areas = new Dictionary<Object, Rect>();

        /// <summary>Records the area a panel covers, in GUI coordinates (origin top left), or clears it with null.</summary>
        public static void SetArea(Object owner, Rect? guiRect)
        {
            if (guiRect.HasValue)
            {
                Areas[owner] = guiRect.Value;
            }
            else
            {
                Areas.Remove(owner);
            }
        }

        /// <summary>True when a screen point (pixels, origin bottom left, as the Input System reports it) is over a panel.</summary>
        public static bool IsOverGui(Vector2 screenPoint)
        {
            var guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            foreach (KeyValuePair<Object, Rect> area in Areas)
            {
                if (area.Key != null && area.Value.Contains(guiPoint))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
