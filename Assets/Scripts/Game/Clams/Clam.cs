using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A clam on a lake or ocean bed (GL735), a low-poly shell a hand across (GL808, <see cref="ClamMeshes"/>). E opens it
    /// on the <see cref="ClamPanel"/>: the lid swings up and what it holds shows inside. What it holds is rolled once
    /// (<see cref="ClamPearls.Open"/>) and kept by <see cref="ClamBeds"/> while it is left closed. Taking from it makes it
    /// vanish in a burst of bubbles (<see cref="ClamBubbles"/>), and its spot is not filled again.
    /// </summary>
    public class Clam : MonoBehaviour, IInteractable
    {
        private const float OpenAngle = 50f, LidSpeed = 240f;

        private static Material shellMaterial;

        private static readonly Color ShellColor = new Color(0.47f, 0.36f, 0.3f);

        private ClamBeds beds;
        private Transform lid;
        private GameObject pearl;
        private bool decided;
        private bool isOpen;
        private float angle;

        /// <summary>The scatter cell this clam came from.</summary>
        public Vector2Int Cell { get; private set; }

        /// <summary>What is inside; rolled on first opening.</summary>
        public ClamFind Contents { get; private set; }

        public string Prompt => "Open clam";

        public static Clam Make(ClamBeds owner, Vector2Int cell, Vector3 position)
        {
            var root = new GameObject("Clam");
            root.transform.SetPositionAndRotation(position + Vector3.up * 0.04f, Quaternion.Euler(0f, Random.value * 360f, 0f));
            Material material = SharedMaterial();
            Part(root.transform, "Bottom", ClamMeshes.BuildShell(false, ShellColor), material);
            // The lid turns about the hinge, at the clam's origin.
            var lidPivot = new GameObject("Lid").transform;
            lidPivot.SetParent(root.transform, false);
            Part(lidPivot, "Top", ClamMeshes.BuildShell(true, ShellColor), material);

            // Wider than the shell, so it can be aimed at; the shell itself is a quarter of a metre.
            var hit = root.AddComponent<SphereCollider>();
            hit.center = new Vector3(0f, 0f, ClamMeshes.Length * 0.5f);
            hit.radius = 0.36f;

            Clam clam = root.AddComponent<Clam>();
            clam.beds = owner;
            clam.Cell = cell;
            clam.lid = lidPivot;
            return clam;
        }

        public void Interact(PlayerInteractor by)
        {
            ClamPanel panel = by.GetComponent<ClamPanel>();
            if (isOpen || panel == null || panel.IsOpen)
            {
                return;
            }
            if (!decided)
            {
                decided = true;
                Contents = beds != null && beds.TryKept(Cell, out ClamFind kept) ? kept : ClamPearls.Open(Random.value, Random.value);
            }
            SetOpen(true);
            panel.Open(this);
        }

        /// <summary>Closes the lid on whatever is inside; the clam stays and keeps it.</summary>
        public void CloseKeeping()
        {
            SetOpen(false);
            beds?.Keep(Cell, Contents);
        }

        /// <summary>Whatever was inside has been taken: the clam goes in a burst of bubbles and its spot stays empty.</summary>
        public void Vanish(Material bubbleMaterial, PlayerSounds sounds)
        {
            Contents = ClamFind.Empty;
            ClamBubbles.Play(transform.TransformPoint(ClamMeshes.PearlSpot), bubbleMaterial, sounds);
            beds?.Opened(Cell);
            Destroy(gameObject);
        }

        private void SetOpen(bool open)
        {
            isOpen = open;
            string id = ClamPearls.IdOf(Contents);
            if (open && id != null && pearl == null)
            {
                Color tint = Contents == ClamFind.BlackPearl ? new Color(0.16f, 0.16f, 0.2f) : new Color(0.96f, 0.93f, 0.88f);
                pearl = Part(transform, "Pearl", ClamMeshes.BuildPearl(tint), shellMaterial);
                pearl.transform.localPosition = ClamMeshes.PearlSpot + Vector3.up * ClamMeshes.PearlRadius;
            }
            if (pearl != null)
            {
                pearl.SetActive(open && id != null);
            }
        }

        private void Update()
        {
            float target = isOpen ? OpenAngle : 0f;
            if (Mathf.Approximately(angle, target))
            {
                return;
            }
            angle = Mathf.MoveTowards(angle, target, LidSpeed * Time.deltaTime);
            // Negative about x lifts the far (+z) edge.
            lid.localRotation = Quaternion.Euler(-angle, 0f, 0f);
        }

        private void OnDestroy()
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Destroy(filter.sharedMesh);
            }
        }

        private static GameObject Part(Transform parent, string name, UnityEngine.Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        // The shared vertex-colour material of every clam.
        private static Material SharedMaterial()
        {
            if (shellMaterial == null)
            {
                shellMaterial = new Material(Shader.Find("Clube/Low Poly")) { name = "Clam" };
            }
            return shellMaterial;
        }
    }
}
