using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A clam on a lake or ocean bed (GL735), about a voxel across. Interact (E) opens it on the <see cref="ClamPanel"/>;
    /// when that closes, the clam is gone and its spot is not filled again (<see cref="ClamBeds"/>).
    /// </summary>
    public class Clam : MonoBehaviour, IInteractable
    {
        private ClamBeds beds;
        private bool opened;

        /// <summary>The scatter cell this clam came from.</summary>
        public Vector2Int Cell { get; private set; }

        public string Prompt => "Open clam";

        public static Clam Make(ClamBeds owner, Vector2Int cell, Vector3 position)
        {
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Clam";
            Destroy(shell.GetComponent<Collider>());
            shell.transform.SetPositionAndRotation(position + Vector3.up * 0.05f, Quaternion.Euler(0f, Random.value * 360f, 0f));
            shell.transform.localScale = new Vector3(0.3f, 0.14f, 0.24f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.42f));
            shell.GetComponent<Renderer>().SetPropertyBlock(block);
            // The collider is wider than the shell, so it can be aimed at; the shell itself is a quarter of a metre.
            var hit = shell.AddComponent<SphereCollider>();
            hit.radius = 1.2f;
            Clam clam = shell.AddComponent<Clam>();
            clam.beds = owner;
            clam.Cell = cell;
            return clam;
        }

        public void Interact(PlayerInteractor by)
        {
            ClamPanel panel = by.GetComponent<ClamPanel>();
            if (opened || panel == null)
            {
                return;
            }
            opened = true;
            ClamFind find = ClamPearls.Open(Random.value, Random.value);
            panel.Open(this, find);
        }

        /// <summary>The panel closed: the clam goes.</summary>
        public void Finish()
        {
            beds.Opened(Cell);
            Destroy(gameObject);
        }
    }
}
