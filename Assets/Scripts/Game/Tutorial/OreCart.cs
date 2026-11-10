using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>The Mining Corp's ore cart in the tutorial (GL479): Interact hands in the company's stone for pay.</summary>
    public class OreCart : MonoBehaviour, IInteractable
    {
        /// <summary>The tutorial that takes the stone and pays for it.</summary>
        public MiningCorpTutorial Tutorial { get; set; }

        public string Prompt => Tutorial != null ? Tutorial.HandInPrompt : "Ore cart";

        public void Interact(PlayerInteractor by)
        {
            if (Tutorial != null)
            {
                Tutorial.HandIn();
            }
        }

        /// <summary>The company's ore cart (a low-poly hand cart, solid, with its hand-in prompt) at a point, facing a direction.</summary>
        public static OreCart Build(Vector3 at, Vector3 facing, Material material, MiningCorpTutorial tutorial)
        {
            var wood = new Color(0.42f, 0.29f, 0.17f);
            var iron = new Color(0.28f, 0.29f, 0.31f);
            var mesh = new LowPolyMeshBuilder();
            mesh.Box(new Vector3(0f, 0.75f, 0f), new Vector3(1.1f, 0.5f, 1.5f), Quaternion.identity, wood);
            mesh.Box(new Vector3(0f, 1.0f, 0f), new Vector3(0.95f, 0.06f, 1.35f), Quaternion.identity, new Color(0.45f, 0.45f, 0.43f));
            foreach (float side in new[] { -0.6f, 0.6f })
            {
                mesh.Prism(new Vector3(side - 0.05f, 0.35f, 0f), new Vector3(side + 0.05f, 0.35f, 0f), 0.35f, 0.35f, 8, iron);
            }
            mesh.Prism(new Vector3(0f, 0.75f, 0.75f), new Vector3(0f, 0.9f, 1.5f), 0.04f, 0.04f, 5, wood);
            var cart = new GameObject("Company ore cart");
            cart.transform.position = at;
            Vector3 flat = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                cart.transform.rotation = Quaternion.LookRotation(flat);
            }
            cart.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Ore cart");
            cart.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = cart.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = new Vector3(1.3f, 1.2f, 1.6f);
            OreCart ore = cart.AddComponent<OreCart>();
            ore.Tutorial = tutorial;
            return ore;
        }
    }
}
