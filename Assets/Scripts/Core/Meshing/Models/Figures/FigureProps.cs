using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The things a figure holds (GL608), each built once: modelled in the hand's frame, where -y runs along the forearm
    /// and +z is forward out of the fist (<see cref="FigureSkeleton.HandSocket"/>).
    /// </summary>
    public static class FigureProps
    {
        private static readonly Color Wood = new Color(0.45f, 0.31f, 0.18f);
        private static readonly Color Iron = new Color(0.3f, 0.31f, 0.33f);
        private static readonly Dictionary<FigureProp, Mesh> Shared = new Dictionary<FigureProp, Mesh>();

        /// <summary>The mesh of a prop; null for <see cref="FigureProp.None"/>.</summary>
        public static Mesh For(FigureProp prop)
        {
            if (prop == FigureProp.None)
            {
                return null;
            }
            if (!Shared.TryGetValue(prop, out Mesh mesh) || mesh == null)
            {
                mesh = Build(prop);
                Shared[prop] = mesh;
            }
            return mesh;
        }

        private static Mesh Build(FigureProp prop)
        {
            var model = new LowPolyMeshBuilder();
            switch (prop)
            {
                case FigureProp.Hammer:
                    // Handle forward out of the fist, head at its end.
                    model.Prism(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, 0.3f), 0.016f, 0.014f, 5, Wood);
                    model.Prism(new Vector3(0f, -0.075f, 0.32f), new Vector3(0f, 0.075f, 0.32f), 0.04f, 0.04f, 6, Iron);
                    break;
                case FigureProp.Broom:
                    // Reaching down and forward to the ground.
                    model.Prism(new Vector3(0f, 0.3f, -0.1f), new Vector3(0f, -0.75f, 0.45f), 0.018f, 0.018f, 5, Wood);
                    model.Box(new Vector3(0f, -0.82f, 0.5f), new Vector3(0.3f, 0.12f, 0.08f), Quaternion.Euler(-28f, 0f, 0f), new Color(0.75f, 0.65f, 0.38f));
                    break;
                case FigureProp.Pick:
                    // A handle forward out of the fist and an iron head across its end (GL415).
                    model.Prism(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, 0.55f), 0.017f, 0.015f, 5, Wood);
                    model.Prism(new Vector3(0f, -0.02f, 0.56f), new Vector3(0f, 0.2f, 0.53f), 0.026f, 0.006f, 4, Iron);
                    model.Prism(new Vector3(0f, 0.02f, 0.56f), new Vector3(0f, -0.17f, 0.54f), 0.024f, 0.008f, 4, Iron);
                    break;
                case FigureProp.Book:
                    model.Box(new Vector3(0f, -0.02f, 0.08f), new Vector3(0.05f, 0.2f, 0.15f), Quaternion.identity, new Color(0.45f, 0.15f, 0.12f));
                    model.Box(new Vector3(0.005f, -0.02f, 0.08f), new Vector3(0.042f, 0.19f, 0.14f), Quaternion.identity, new Color(0.88f, 0.84f, 0.72f));
                    break;
                case FigureProp.Stick:
                    // A stick being whittled.
                    model.Prism(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, 0.25f), 0.012f, 0.01f, 5, Wood);
                    break;
                case FigureProp.Dagger:
                    model.Prism(new Vector3(0f, 0f, -0.04f), new Vector3(0f, 0f, 0.08f), 0.016f, 0.016f, 5, new Color(0.25f, 0.15f, 0.1f));
                    model.Box(new Vector3(0f, 0f, 0.09f), new Vector3(0.09f, 0.02f, 0.02f), Quaternion.identity, Iron);
                    model.Prism(new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, 0.3f), 0.02f, 0.003f, 4, new Color(0.4f, 0.41f, 0.44f));
                    break;
                case FigureProp.Staff:
                    // A walking staff, upright in the fist of a hanging arm.
                    model.Prism(new Vector3(0f, -0.72f, 0f), new Vector3(0f, 0.85f, 0f), 0.022f, 0.03f, 6, Wood);
                    break;
            }
            return model.Make($"{prop} prop");
        }
    }
}
