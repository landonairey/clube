using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The trade cart in the arrival cutscene (GL583): a <see cref="TradeCartModel"/> with no crates, driven along a straight
    /// line over the ground that ends at the drop-off point, tipped to slide the player out, then driven away. It follows the
    /// terrain under it (a ray into the <see cref="WorldView"/>), so no road is needed.
    /// </summary>
    public class ArrivalCart
    {
        private readonly TradeCartModel model;
        private readonly WorldView worldView;
        private readonly Vector3 stop;
        private readonly Vector3 direction;
        private float lastHeight;

        /// <summary>Builds the cart <paramref name="material"/> is for, standing at <paramref name="stop"/>, heading <paramref name="direction"/> (flat).</summary>
        public ArrivalCart(Material material, WorldView worldView, Vector3 stop, Vector3 direction)
        {
            this.worldView = worldView;
            this.stop = stop;
            this.direction = direction;
            lastHeight = stop.y;
            var root = new GameObject("Arrival cart");
            model = root.AddComponent<TradeCartModel>();
            model.Build(material, null, 477);
            // Nothing should collide with the world or the player's capsule on the way.
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        /// <summary>The bed, where the player lies.</summary>
        public Transform Bed => model.Bed;

        /// <summary>The ground height at a world position, near where it was last asked.</summary>
        public float GroundHeight(Vector3 at)
        {
            var ray = new Ray(new Vector3(at.x, lastHeight + 30f, at.z), Vector3.down);
            if (worldView != null && worldView.Raycast(ray, out Vector3 hit))
            {
                lastHeight = hit.y;
            }
            return lastHeight;
        }

        /// <summary>Puts the axle <paramref name="along"/> metres past the stop (negative before it), the bed tipped by <paramref name="tipDegrees"/>.</summary>
        public void Place(float along, float tipDegrees, bool moving)
        {
            Vector3 axle = On(along);
            Vector3 front = On(along + TradeCartModel.TiltReach);
            Vector3 back = On(along - TradeCartModel.TiltReach);
            Vector3 carterAt = On(along + TradeCartModel.CarterAhead);
            model.PlaceAt(axle, carterAt, front - back, tipDegrees, moving, along);
        }

        /// <summary>Removes the cart.</summary>
        public void Destroy()
        {
            if (model != null)
            {
                Object.Destroy(model.gameObject);
            }
        }

        private Vector3 On(float along)
        {
            Vector3 flat = stop + direction * along;
            return new Vector3(flat.x, GroundHeight(flat), flat.z);
        }
    }
}
