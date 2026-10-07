using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Placing and picking up objects (GL23, first pass of PK17). With a placeable item (a
    /// furnace, an anvil, a merchant table) in the selected hotbar slot, the player is in build
    /// mode: the build cell they look at is lit, a local patch of the build grid shows around it
    /// (<see cref="BuildGridDisplay"/>), and a see-through preview stands in the cell, turned to
    /// face them. Build (B) places it there, spending the item; the grid hides again once
    /// nothing placeable is selected. Holding Build while looking at a placed object picks it
    /// back up, with whatever a station holds.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory), typeof(Hotbar), typeof(PlayerController))]
    public class PlayerBuilder : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private BuildGridDisplay grid;

        [Tooltip("Places (press) and picks up (hold) (B).")]
        [SerializeField]
        private InputActionReference buildAction;

        [Tooltip("A transparent material with a _BaseColor for the preview (BrushPreview, URP Unlit).")]
        [SerializeField]
        private Material previewMaterial;

        [SerializeField]
        private Color clearColor = new Color(0.4f, 1f, 0.6f, 0.35f);

        [SerializeField]
        private Color blockedColor = new Color(1f, 0.3f, 0.25f, 0.35f);

        [Tooltip("How far from the head things can be placed or picked up, in metres.")]
        [SerializeField, Min(1f)]
        private float reach = 6f;

        [Tooltip("Seconds Build must be held on a placed object to pick it up.")]
        [SerializeField, Range(0.1f, 2f)]
        private float holdSeconds = 0.5f;

        private readonly Collider[] overlaps = new Collider[16];
        private PlayerInventory inventory;
        private Hotbar hotbar;
        private PlayerController player;
        private GameObject preview;
        private PlaceableDefinition previewItem;
        private MaterialPropertyBlock properties;
        private float pressedAt;
        private PlacedObject pressedOn;
        private GUIStyle hintStyle;
        private string hint;
        private PlaceableDefinition aimedItem;
        private Vector3 aimedPosition;
        private Quaternion aimedRotation;

        /// <summary>True while a placeable item is selected and aimed at free ground within reach.</summary>
        public bool CanPlace { get; private set; }

        /// <summary>The placed object aimed at within reach, while nothing placeable is selected.</summary>
        public PlacedObject Target { get; private set; }

        /// <summary>Places the selected item where the preview stands (what Build does). False when it can't go there.</summary>
        public bool TryPlace()
        {
            if (!CanPlace)
            {
                return false;
            }
            GameObject placed = Instantiate(aimedItem.Prefab, aimedPosition, aimedRotation);
            placed.name = aimedItem.DisplayName;
            placed.AddComponent<PlacedObject>().Item = aimedItem;
            inventory.Inventory.RemoveAt(hotbar.Selected, 1);
            CanPlace = false;
            return true;
        }

        /// <summary>Picks the aimed placed object back up into the inventory (what holding Build does). False when there's none, or no room.</summary>
        public bool TryPickUp()
        {
            if (Target == null)
            {
                return false;
            }
            if (!Target.PickUpInto(inventory.Inventory))
            {
                hint = "Not enough room to pick it up";
                return false;
            }
            Target = null;
            return true;
        }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            hotbar = GetComponent<Hotbar>();
            player = GetComponent<PlayerController>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            buildAction?.action.Enable();
        }

        private void OnDisable()
        {
            StopBuilding();
        }

        private void OnDestroy()
        {
            Destroy(preview);
        }

        private void Update()
        {
            hint = null;
            CanPlace = false;
            Target = null;
            if (!player.enabled || player.IsCursorFree || worldView == null || !worldView.IsReady)
            {
                StopBuilding();
                return;
            }

            InputAction action = buildAction != null ? buildAction.action : null;
            if (hotbar.SelectedStack.Item is PlaceableDefinition placeable && placeable.Prefab != null)
            {
                Build(placeable, action);
                return;
            }

            StopBuilding();
            PickUp(action);
        }

        private void Build(PlaceableDefinition placeable, InputAction action)
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (!worldView.Raycast(new Ray(head.position, head.forward), out Vector3 hit)
                || (hit - head.position).sqrMagnitude > reach * reach)
            {
                StopBuilding();
                return;
            }

            // The cell aimed at; the object stands on the ground at its centre, facing the player.
            var cells = new BuildGrid(worldView.Config.BuildCellSize);
            Vector3 local = worldView.transform.InverseTransformPoint(hit);
            Vector2Int cell = cells.Cell(local);
            Vector2 centre = cells.Centre(cell);
            Vector3 position = Ground(new Vector3(centre.x, local.y, centre.y));
            Vector3 toPlayer = transform.position - position;
            float yaw = Mathf.Round(Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg / 90f) * 90f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            ShowPreview(placeable, position, rotation, out Bounds bounds);
            bool blocked = Blocked(bounds, rotation);
            properties.SetColor(BaseColorId, blocked ? blockedColor : clearColor);
            foreach (MeshRenderer part in preview.GetComponentsInChildren<MeshRenderer>())
            {
                part.SetPropertyBlock(properties);
            }
            if (grid != null)
            {
                grid.Show(new Vector2(cell.x, cell.y) * cells.CellSize, cells.CellSize);
            }

            aimedItem = placeable;
            aimedPosition = position;
            aimedRotation = rotation;
            CanPlace = !blocked;
            hint = blocked ? $"Something is in the way of the {placeable.DisplayName.ToLowerInvariant()}" : $"B  Place {placeable.DisplayName.ToLowerInvariant()}";
            if (action != null && action.WasPressedThisFrame())
            {
                TryPlace();
            }
        }

        // Hold Build on a placed object to pick it up.
        private void PickUp(InputAction action)
        {
            Target = Aimed();
            if (Target != null)
            {
                hint = $"Hold B  Pick up {Target.Item?.DisplayName.ToLowerInvariant()}";
            }
            if (action == null)
            {
                return;
            }
            if (action.WasPressedThisFrame())
            {
                pressedAt = Time.time;
                pressedOn = Target;
            }
            if (action.IsPressed() && pressedOn != null && pressedOn == Target && Time.time - pressedAt >= holdSeconds)
            {
                TryPickUp();
                pressedOn = null;
            }
        }

        // The placed object under the crosshair within reach, if any.
        private PlacedObject Aimed()
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (Physics.Raycast(head.position, head.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.GetComponentInParent<PlacedObject>();
            }
            return null;
        }

        // The terrain surface straight below (or above) a point, relative to the world origin; world space out.
        private Vector3 Ground(Vector3 local)
        {
            Vector3 above = worldView.transform.TransformPoint(local + Vector3.up * 3f);
            return worldView.Raycast(new Ray(above, -worldView.transform.up), out Vector3 ground)
                ? ground
                : worldView.transform.TransformPoint(local);
        }

        private bool Blocked(Bounds bounds, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents * 0.9f, overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i].GetComponentInParent<PlacedObject>() != null || overlaps[i].transform.IsChildOf(transform))
                {
                    return true;
                }
            }
            return false;
        }

        // A see-through copy of the item's meshes, rebuilt when the item changes.
        private void ShowPreview(PlaceableDefinition placeable, Vector3 position, Quaternion rotation, out Bounds bounds)
        {
            if (preview == null || previewItem != placeable)
            {
                Destroy(preview);
                preview = new GameObject($"{placeable.DisplayName} preview");
                previewItem = placeable;
                foreach (MeshFilter part in placeable.Prefab.GetComponentsInChildren<MeshFilter>())
                {
                    var child = new GameObject(part.name);
                    child.transform.SetParent(preview.transform, false);
                    Transform source = part.transform;
                    child.transform.localPosition = placeable.Prefab.transform.InverseTransformPoint(source.position);
                    child.transform.localRotation = Quaternion.Inverse(placeable.Prefab.transform.rotation) * source.rotation;
                    child.transform.localScale = source.lossyScale;
                    child.AddComponent<MeshFilter>().sharedMesh = part.sharedMesh;
                    var shown = child.AddComponent<MeshRenderer>();
                    var materials = new Material[part.sharedMesh != null ? part.sharedMesh.subMeshCount : 1];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = previewMaterial;
                    }
                    shown.sharedMaterials = materials;
                    shown.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            preview.SetActive(true);
            preview.transform.SetPositionAndRotation(position, rotation);

            // Local bounds of the meshes, centred in world space, for the overlap check.
            bounds = new Bounds(position, Vector3.zero);
            bool first = true;
            foreach (MeshFilter part in preview.GetComponentsInChildren<MeshFilter>())
            {
                if (part.sharedMesh == null)
                {
                    continue;
                }
                Bounds local = part.sharedMesh.bounds;
                Vector3 centre = part.transform.TransformPoint(local.center);
                Vector3 size = Vector3.Scale(local.size, part.transform.lossyScale);
                if (first)
                {
                    bounds = new Bounds(centre, size);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(new Bounds(centre, size));
                }
            }
        }

        private void StopBuilding()
        {
            if (preview != null)
            {
                preview.SetActive(false);
            }
            if (grid != null)
            {
                grid.Hide();
            }
        }

        private void OnGUI()
        {
            if (hint == null || !player.enabled || player.IsCursorFree)
            {
                return;
            }
            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUI.Label(new Rect(0f, Screen.height * 0.5f + 46f, Screen.width, 24f), hint, hintStyle);
        }
    }
}
