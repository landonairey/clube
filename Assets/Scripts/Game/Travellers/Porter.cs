using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// GL726: the unloading and loading a cart's carter or a vessel's deckhand does at a stop. They walk back and forth
    /// between the vehicle and where the goods go (the shop's door, the dock), one trip to a crate, carrying a sack one
    /// way: from the vehicle while unloading, to it while loading.
    /// </summary>
    public static class Porter
    {
        private const string GoodsName = "Carried goods";
        private static Mesh sack;

        /// <summary>
        /// Puts <paramref name="figure"/> (world space) on its way between <paramref name="vehicle"/> and
        /// <paramref name="drop"/>, <paramref name="progress"/> through a part of the stop with <paramref name="trips"/>
        /// trips in it; <paramref name="unloading"/> carries towards the drop, else back to the vehicle.
        /// </summary>
        public static void Carry(FigureRig figure, Vector3 vehicle, Vector3 drop, float progress, int trips, bool unloading, Material material)
        {
            float t = Mathf.Clamp01(progress) * Mathf.Max(1, trips);
            float f = t - Mathf.Floor(t);
            if (progress >= 1f)
            {
                f = 0.999f;
            }
            bool outward = f < 0.5f;
            float u = outward ? f * 2f : (1f - f) * 2f;
            // A moment's pause at each end, to set the sack down or take it up.
            float walk = Mathf.InverseLerp(0.08f, 0.92f, u);
            Transform body = figure.transform;
            body.position = Vector3.Lerp(vehicle, drop, walk);
            Vector3 heading = (outward ? drop - vehicle : vehicle - drop);
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f)
            {
                body.rotation = Quaternion.LookRotation(heading);
            }
            figure.Pose = walk > 0f && walk < 1f ? FigureAction.Walk : FigureAction.Stand;
            Show(figure, material, outward == unloading);
        }

        /// <summary>Takes the sack out of the figure's hands.</summary>
        public static void PutDown(FigureRig figure)
        {
            Transform goods = figure != null && figure.Hand != null ? figure.Hand.Find(GoodsName) : null;
            if (goods != null)
            {
                goods.gameObject.SetActive(false);
            }
        }

        private static void Show(FigureRig figure, Material material, bool carrying)
        {
            if (figure.Hand == null)
            {
                return;
            }
            Transform goods = figure.Hand.Find(GoodsName);
            if (goods == null)
            {
                if (!carrying)
                {
                    return;
                }
                if (sack == null)
                {
                    var model = new LowPolyMeshBuilder();
                    model.Blob(new Vector3(0f, -0.12f, 0.06f), new Vector3(0.18f, 0.22f, 0.15f), Quaternion.identity, new Color(0.62f, 0.52f, 0.36f));
                    model.Prism(new Vector3(0f, 0.08f, 0.06f), new Vector3(0f, 0.14f, 0.06f), 0.05f, 0.03f, 5, new Color(0.45f, 0.36f, 0.24f));
                    sack = model.Make("Carried sack");
                }
                var made = new GameObject(GoodsName);
                made.transform.SetParent(figure.Hand, false);
                made.AddComponent<MeshFilter>().sharedMesh = sack;
                made.AddComponent<MeshRenderer>().sharedMaterial = material;
                goods = made.transform;
            }
            goods.gameObject.SetActive(carrying);
        }
    }
}
