using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A yellow arrow bobbing and turning over someone's head (GL513), pointing down at who a quest says to talk to.
    /// Turned on and off with its GameObject.
    /// </summary>
    public class QuestArrow : MonoBehaviour
    {
        private const float Bob = 0.15f;
        private float height;

        /// <summary>Builds an arrow over a figure, <paramref name="height"/> metres up; it starts hidden.</summary>
        public static QuestArrow Build(Transform over, float height, Material material)
        {
            var yellow = new Color(1f, 0.82f, 0.1f);
            var mesh = new LowPolyMeshBuilder();
            mesh.Prism(new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0.8f, 0f), 0.09f, 0.09f, 6, yellow);
            mesh.Prism(new Vector3(0f, 0.25f, 0f), new Vector3(0f, -0.15f, 0f), 0.24f, 0.01f, 6, yellow);
            var go = new GameObject("Quest arrow");
            go.transform.SetParent(over, false);
            go.transform.localPosition = Vector3.up * height;
            go.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Quest arrow");
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            QuestArrow arrow = go.AddComponent<QuestArrow>();
            arrow.height = height;
            go.SetActive(false);
            return arrow;
        }

        private void Update()
        {
            transform.localPosition = Vector3.up * (height + Mathf.Sin(Time.time * 3f) * Bob);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }
    }
}
