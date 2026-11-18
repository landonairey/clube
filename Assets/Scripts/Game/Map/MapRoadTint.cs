using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// GL718: paints the bounded map's planned roads into a <see cref="RoadMask"/> once the map is ready, and hands it to
    /// the coarse and far terrain (<see cref="TerrainRoadTint"/>), so the roads show from far away where the world's
    /// chunks, which hold the road voxels, are not drawn. Each road in its class's surface colour.
    /// </summary>
    public class MapRoadTint : MonoBehaviour
    {
        [SerializeField]
        private MapSession session;

        [Tooltip("Metres of ground per texel of the mask (it is at most 2048 texels across).")]
        [SerializeField, Range(1f, 8f)]
        private float texelSize = 2f;

        private int version = -1;

        private void Update()
        {
            if (session == null || !session.IsReady || session.Plan == null || session.Layout == null)
            {
                return;
            }
            if (session.Version != version)
            {
                version = session.Version;
                Paint();
            }
        }

        private void OnDisable()
        {
            TerrainRoadTint.Clear();
            version = -1;
        }

        private void Paint()
        {
            Rect area = session.Layout.Bounds;
            int size = Mathf.Clamp(Mathf.CeilToInt(area.width / texelSize), 256, 2048);
            var mask = new RoadMask(area, size);
            WorldPlan plan = session.Plan;
            MaterialRegistry materials = session.WorldView.Config.Materials;
            foreach (PlannedRoad road in plan.Roads)
            {
                RoadClassDefinition definition = plan.Rules.Road(road.Class);
                mask.Paint(new RoadMask.Road(road.Points, definition.Width * 0.5f, ColourOf(materials, definition.Surface), road.Tunnels));
            }
            TerrainRoadTint.Set(mask, session.WorldView.transform.position);
        }

        // GL757: the surface's real colour, in linear space like the rest of the terrain's albedo.
        private static Color32 ColourOf(MaterialRegistry materials, string surface)
        {
            VoxelMaterial material = materials != null && !string.IsNullOrEmpty(surface) ? materials.Find(surface) : null;
            Color colour = material != null ? material.MeanColor : new Color(0.55f, 0.47f, 0.36f).linear;
            colour.a = 1f;
            return colour;
        }
    }
}
