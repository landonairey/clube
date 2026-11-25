using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A sherpa's big pack of goods (GL868: "their backpacks that they are wearing"): a tall frame pack hung on the
    /// figure's back, higher than the head, with a bedroll on top and a pot at the side. It is a real container
    /// (<see cref="StorageChest"/>) with its own collider standing out behind the figure's, so E aimed at a sherpa's back
    /// opens the pack and aimed at the sherpa talks to them.
    /// </summary>
    public static class SherpaPack
    {
        public const string Name = "Sherpa's pack";

        private static readonly Color Canvas = new Color(0.55f, 0.47f, 0.32f);
        private static readonly Color Strap = new Color(0.3f, 0.2f, 0.12f);
        private static readonly Color Frame = new Color(0.42f, 0.3f, 0.18f);
        private static readonly Color Roll = new Color(0.36f, 0.42f, 0.5f);
        private static readonly Color Pot = new Color(0.32f, 0.32f, 0.34f);

        // Torso-space centre and size of the pack's body.
        private static readonly Vector3 Centre = new Vector3(0f, 0.42f, -0.34f);
        private static readonly Vector3 Size = new Vector3(0.5f, 0.74f, 0.32f);

        private static Mesh mesh;

        /// <summary>Hangs a pack on <paramref name="torso"/> and returns its container (<paramref name="quality"/> sets its slots).</summary>
        public static StorageChest Build(Transform torso, Material material, float quality)
        {
            var pack = new GameObject(Name);
            pack.transform.SetParent(torso, false);
            pack.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : mesh = Make();
            pack.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = pack.AddComponent<BoxCollider>();
            box.center = Centre + new Vector3(0f, 0.04f, -0.02f);
            box.size = Size + new Vector3(0.06f, 0.14f, 0.06f);
            StorageChest chest = pack.AddComponent<StorageChest>();
            chest.Rename(Name);
            chest.MadeOf(quality);
            return chest;
        }

        private static Mesh Make()
        {
            var model = new LowPolyMeshBuilder();
            model.Box(Centre, Size, Quaternion.identity, Canvas);
            // A flap over the top, a pocket on the back.
            model.Box(Centre + new Vector3(0f, Size.y * 0.5f - 0.06f, -0.02f), new Vector3(Size.x + 0.03f, 0.14f, Size.z + 0.04f), Quaternion.identity,
                new Color(Canvas.r * 0.85f, Canvas.g * 0.85f, Canvas.b * 0.85f));
            model.Box(Centre + new Vector3(0f, -0.1f, -Size.z * 0.5f - 0.05f), new Vector3(0.34f, 0.3f, 0.1f), Quaternion.identity, new Color(0.5f, 0.43f, 0.29f));
            // The wooden frame up the sides and the straps over the shoulders.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (Size.x * 0.5f + 0.02f);
                model.Prism(new Vector3(x, Centre.y - Size.y * 0.5f - 0.06f, Centre.z + 0.1f), new Vector3(x, Centre.y + Size.y * 0.5f + 0.08f, Centre.z + 0.1f),
                    0.025f, 0.025f, 5, Frame);
                model.Box(new Vector3(side * 0.12f, 0.33f, 0.125f), new Vector3(0.045f, 0.26f, 0.02f), Quaternion.identity, Strap);
                model.Box(new Vector3(side * 0.12f, 0.47f, -0.03f), new Vector3(0.045f, 0.02f, 0.3f), Quaternion.identity, Strap);
            }
            // A bedroll across the top and a pot hung at one side.
            float top = Centre.y + Size.y * 0.5f + 0.09f;
            model.Prism(new Vector3(-0.3f, top, Centre.z), new Vector3(0.3f, top, Centre.z), 0.09f, 0.09f, 7, Roll);
            model.Prism(new Vector3(Size.x * 0.5f + 0.09f, Centre.y - 0.22f, Centre.z), new Vector3(Size.x * 0.5f + 0.09f, Centre.y - 0.08f, Centre.z),
                0.08f, 0.09f, 7, Pot);
            return model.Make(Name);
        }
    }
}
