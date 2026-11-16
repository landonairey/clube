using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a camera stands to take an item's icon (GL612): an orthographic view down <see cref="Rotation"/>'s forward
    /// that holds the whole model, centred, the larger of its width and height filling the square frame but for a
    /// margin. Pure arithmetic, so the framing is tested without a camera; the icon renderer applies it.
    /// </summary>
    public readonly struct IconFraming
    {
        /// <summary>The usual view of a model: from the front, a little to one side and above.</summary>
        public static readonly Quaternion ThreeQuarter = Quaternion.Euler(24f, -28f, 0f);

        public IconFraming(Vector3 position, Quaternion rotation, float orthographicSize, float nearClip, float farClip)
        {
            Position = position;
            Rotation = rotation;
            OrthographicSize = orthographicSize;
            NearClip = nearClip;
            FarClip = farClip;
        }

        /// <summary>Where the camera stands: back from the model along its view, clear of it.</summary>
        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        /// <summary>Half the height (and width: the frame is square) of what the camera sees.</summary>
        public float OrthographicSize { get; }

        public float NearClip { get; }

        public float FarClip { get; }

        /// <summary>
        /// The framing that holds every one of <paramref name="points"/> seen down <paramref name="view"/>'s forward,
        /// leaving <paramref name="margin"/> of the frame's half-size empty round the widest of them.
        /// </summary>
        public static IconFraming Fit(IReadOnlyList<Vector3> points, Quaternion view, float margin = 0.06f)
        {
            if (points == null || points.Count == 0)
            {
                return new IconFraming(view * Vector3.back, view, 0.5f, 0.01f, 2f);
            }
            Quaternion toView = Quaternion.Inverse(view);
            Vector3 min = toView * points[0], max = min;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 p = toView * points[i];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 centre = (min + max) * 0.5f;
            float half = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f;
            half = Mathf.Max(half, 1e-4f) / (1f - Mathf.Clamp(margin, 0f, 0.9f));
            float depth = max.z - min.z;
            float back = depth * 0.5f + half + 0.1f;
            Vector3 position = view * new Vector3(centre.x, centre.y, centre.z - back);
            return new IconFraming(position, view, half, 0.01f, back + depth * 0.5f + half + 0.1f);
        }

        /// <summary>The framing of a box (a mesh's bounds) placed by <paramref name="model"/>: its eight corners fitted.</summary>
        public static IconFraming Fit(Bounds bounds, Matrix4x4 model, Quaternion view, float margin = 0.06f)
        {
            var corners = new Vector3[8];
            Corners(bounds, model, corners, 0);
            return Fit(corners, view, margin);
        }

        /// <summary>Writes a placed box's eight corners into <paramref name="into"/> from <paramref name="at"/>.</summary>
        public static void Corners(Bounds bounds, Matrix4x4 model, Vector3[] into, int at)
        {
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                into[at + i] = model.MultiplyPoint3x4(corner);
            }
        }

        /// <summary>
        /// Where a point lands in the frame: x and y from -1 (left, bottom) to 1 (right, top), z its distance in front
        /// of the camera.
        /// </summary>
        public Vector3 ToFrame(Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(Rotation) * (point - Position);
            return new Vector3(local.x / OrthographicSize, local.y / OrthographicSize, local.z);
        }
    }
}
