using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The tool cursor (GL4, first pass of TL3): lights up the surface the held tool would
    /// hit. The targets are solid samples ("corners"), which sit just inside the ground, so
    /// the glows go on the polygon corners each target gives the mesh
    /// (<see cref="SampleSurface"/>): always on the surface the player sees. A buried target
    /// shows nothing until digging uncovers it. Each glow warms from
    /// <see cref="idleColor"/> towards <see cref="damagedColor"/> as its sample takes
    /// damage, showing how close it is to breaking.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class ToolCursor : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("A glow material with a _BaseColor (ToolCursorGlow).")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private Color idleColor = new Color(1f, 0.95f, 0.7f, 0.6f);

        [SerializeField]
        private Color damagedColor = new Color(1f, 0.45f, 0.1f, 1f);

        [Tooltip("Glow diameter as a fraction of the voxel size.")]
        [SerializeField, Range(0.1f, 1f)]
        private float size = 0.5f;

        private readonly List<MeshRenderer> glows = new List<MeshRenderer>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private PlayerToolUser tools;
        private MaterialPropertyBlock properties;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            properties = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            ShowCount(0);
        }

        private void OnDestroy()
        {
            foreach (MeshRenderer glow in glows)
            {
                if (glow != null)
                {
                    Destroy(glow.gameObject);
                }
            }
        }

        // After the tool user's Update has found this frame's targets.
        private void LateUpdate()
        {
            IReadOnlyList<Vector3Int> targets = tools.Targets;
            WorldView view = tools.WorldView;
            ToolDefinition tool = tools.Current;
            if (material == null || tool == null || view == null || !view.IsReady)
            {
                ShowCount(0);
                return;
            }

            World world = view.World;
            ChunkMeshSettings settings = view.Config.MeshSettings;
            Vector3 scale = Vector3.one * (world.Grid.VoxelSize * size);
            int shown = 0;
            foreach (Vector3Int sample in targets)
            {
                vertices.Clear();
                SampleSurface.Vertices(world, sample, settings, vertices);
                Color color = Color.Lerp(idleColor, damagedColor, DamageFraction(view, sample));
                foreach (Vector3 vertex in vertices)
                {
                    MeshRenderer glow = Glow(shown++);
                    glow.transform.SetPositionAndRotation(view.transform.TransformPoint(vertex), Quaternion.identity);
                    glow.transform.localScale = scale;
                    properties.SetColor(BaseColorId, color);
                    glow.SetPropertyBlock(properties);
                }
            }
            ShowCount(shown);
        }

        // How far a sample is towards breaking, 0-1.
        private float DamageFraction(WorldView view, Vector3Int sample)
        {
            float damage = tools.Damage.Get(sample);
            if (damage <= 0f)
            {
                return 0f;
            }
            byte? id = view.World.GetMaterial(sample);
            VoxelMaterial voxelMaterial = id.HasValue && view.Config.Materials != null ? view.Config.Materials.Get(id.Value) : null;
            float hardness = voxelMaterial != null ? voxelMaterial.Hardness : 1f;
            return hardness > 0f ? Mathf.Clamp01(damage / hardness) : 1f;
        }

        // The glow at an index, made when first needed.
        private MeshRenderer Glow(int index)
        {
            while (glows.Count <= index)
            {
                glows.Add(CreateGlow());
            }
            return glows[index];
        }

        // Shows the first count glows and hides the rest.
        private void ShowCount(int count)
        {
            for (int i = 0; i < glows.Count; i++)
            {
                if (glows[i] != null)
                {
                    glows[i].gameObject.SetActive(i < count);
                }
            }
        }

        private MeshRenderer CreateGlow()
        {
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Tool Cursor";
            // A cursor only: nothing collides with it or casts shadows from it.
            Destroy(glow.GetComponent<Collider>());
            var glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = material;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
            glow.SetActive(false);
            return glowRenderer;
        }
    }
}
