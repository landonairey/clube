using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Where the terrain's surface is under a point, for things that walk or roll on it without physics (GL615's
    /// residents, GL616's carts): a ray down from a little over a height the caller knows to be near, so somebody under
    /// a tree, in a pit or in a tunnel finds the floor they stand on rather than what is over their head.
    /// </summary>
    public static class GroundProbe
    {
        /// <summary>The ray starts this far over the near height, and gives up this far down.</summary>
        public const float Above = 1.2f, Depth = 8f;

        /// <summary>The surface's height under (x, z), in the world's own metres; false where no terrain is loaded in reach.</summary>
        public static bool TryGround(WorldView view, float x, float z, float near, out float height)
        {
            height = 0f;
            World world = view != null ? view.World : null;
            var ray = new Ray(new Vector3(x, near + Above, z), Vector3.down);
            if (world == null || !world.Raycast(ray, view.Config.MeshSettings, Depth, out WorldHit hit))
            {
                return false;
            }
            height = hit.Point.y;
            return true;
        }
    }
}
