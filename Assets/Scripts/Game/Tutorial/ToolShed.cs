using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The Mining Corp's tool shed in the tutorial (GL533): Interact hands out a new company pick when the player has none,
    /// one at a time, so a worn-out pick can be replaced (GL522).
    /// </summary>
    public class ToolShed : MonoBehaviour, IInteractable
    {
        /// <summary>The tutorial that decides whether a pick is handed out.</summary>
        public MiningCorpTutorial Tutorial { get; set; }

        public string Prompt => "Take a company pickaxe";

        public void Interact(PlayerInteractor by)
        {
            if (Tutorial != null)
            {
                Tutorial.TakePickFromShed();
            }
        }

        /// <summary>Builds the shed (plank walls, a lean-to roof, a "Tool Shed" sign) at a point, facing a direction.</summary>
        public static ToolShed Build(Vector3 at, Vector3 facing, Material material, MiningCorpTutorial tutorial, Material textMaterial)
        {
            var plank = new Color(0.55f, 0.43f, 0.3f);
            var dark = new Color(0.36f, 0.27f, 0.18f);
            var roof = new Color(0.33f, 0.35f, 0.38f);
            var mesh = new LowPolyMeshBuilder();
            mesh.Box(new Vector3(0f, 1.1f, 0f), new Vector3(3f, 2.2f, 2.2f), Quaternion.identity, plank);
            mesh.Box(new Vector3(0f, 2.3f, 0f), new Vector3(3.4f, 0.14f, 2.6f), Quaternion.identity, roof);
            mesh.Box(new Vector3(0f, 0.95f, 1.12f), new Vector3(1.0f, 1.9f, 0.06f), Quaternion.identity, dark);
            mesh.Box(new Vector3(0f, 2.0f, 1.14f), new Vector3(2.4f, 0.5f, 0.06f), Quaternion.identity, dark);
            var go = new GameObject("Tool shed");
            go.transform.position = at;
            Vector3 flat = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                go.transform.rotation = Quaternion.LookRotation(flat);
            }
            go.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Tool shed");
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.2f, 0f);
            box.size = new Vector3(3.2f, 2.4f, 2.4f);
            // The sign faces +z at yaw 180 (see CompanyWarehouse.AddText).
            CompanyWarehouse.AddText(go.transform, "TOOL SHED", new Vector3(0f, 2.0f, 1.19f), 180f, 0.05f, textMaterial);
            ToolShed shed = go.AddComponent<ToolShed>();
            shed.Tutorial = tutorial;
            return shed;
        }
    }
}
