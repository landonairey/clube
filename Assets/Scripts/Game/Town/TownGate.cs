using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The town's wooden gate (GL400): two leaves hung on the pillars either side of the opening in the wall, swinging
    /// inward. E opens or closes it; it starts open. Each leaf has a box collider, so a shut gate keeps the player out.
    /// </summary>
    public class TownGate : MonoBehaviour, IInteractable
    {
        // Degrees an open leaf has swung, and how fast it swings.
        private const float OpenAngle = 100f;
        private const float DegreesPerSecond = 110f;

        private Transform left;
        private Transform right;
        private float angle = OpenAngle;

        /// <summary>True when the gate stands open (or is opening).</summary>
        public bool IsOpen { get; private set; } = true;

        public string Prompt => IsOpen ? "Close the gate" : "Open the gate";

        /// <summary>Hangs the two leaves on the gate's opening, in the gate's frame: centred on x = 0, swinging into +z.</summary>
        public void Build(Material material)
        {
            float half = TownLayout.GateWidth * 0.5f;
            float height = TownLayout.GateHeight;
            Mesh leaf = TownMeshes.GateLeaf(half, height);
            left = Leaf("Left leaf", new Vector3(-half, 0f, 0f), 0f, leaf, material, half, height);
            right = Leaf("Right leaf", new Vector3(half, 0f, 0f), 180f, leaf, material, half, height);
            Swing();
        }

        private Transform Leaf(string name, Vector3 hinge, float turn, Mesh mesh, Material material, float width, float height)
        {
            var hingeObject = new GameObject(name);
            hingeObject.transform.SetParent(transform, false);
            hingeObject.transform.localPosition = hinge;
            var leafObject = new GameObject("Leaf");
            leafObject.transform.SetParent(hingeObject.transform, false);
            leafObject.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            leafObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            leafObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = leafObject.AddComponent<BoxCollider>();
            box.center = new Vector3(width * 0.5f, height * 0.5f, 0f);
            box.size = new Vector3(width, height, 0.12f);
            return hingeObject.transform;
        }

        public void Interact(PlayerInteractor by)
        {
            IsOpen = !IsOpen;
        }

        private void Update()
        {
            float target = IsOpen ? OpenAngle : 0f;
            if (!Mathf.Approximately(angle, target))
            {
                angle = Mathf.MoveTowards(angle, target, DegreesPerSecond * Time.deltaTime);
                Swing();
            }
        }

        // Both leaves turn into the town: the left one about its hinge one way, the right the other.
        private void Swing()
        {
            if (left == null)
            {
                return;
            }
            left.localRotation = Quaternion.Euler(0f, -angle, 0f);
            right.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
