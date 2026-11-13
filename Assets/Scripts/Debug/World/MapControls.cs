using Clube.Core;
using Clube.Game;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The bounded map's part of the lab panel (GL598): the seed, each side's boundary kind (from the seed or picked
    /// by hand), a regenerate button, what the map session planned, and the lab's switches for the ground markers and
    /// the world map's resource and reasons layer (GL599). Edits the config and calls
    /// <see cref="WorldConfig.NotifyChanged"/>, so the world regenerates and the session surveys and plans again.
    /// </summary>
    public static class MapControls
    {
        private static readonly MapSide[] Sides = { MapSide.West, MapSide.East, MapSide.South, MapSide.North };

        public static void Draw(LabPanelFrame frame, WorldConfig config, MapSession session, MapMarkers markers, WorldMapScreen mapScreen)
        {
            TerrainSettings terrain = config.Terrain;
            MapDefinition map = terrain.Map;
            bool changed = false;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Seed {terrain.Seed}", frame.Label);
            int seed = terrain.Seed;
            if (GUILayout.Button("−", frame.Button))
            {
                seed--;
            }
            if (GUILayout.Button("+", frame.Button))
            {
                seed++;
            }
            if (GUILayout.Button("Random", frame.Button))
            {
                seed = UnityEngine.Random.Range(0, 100000);
            }
            GUILayout.EndHorizontal();
            if (seed != terrain.Seed)
            {
                terrain.Seed = seed;
                changed = true;
            }

            // Whole hundreds of metres.
            float size = Mathf.Round(frame.Slider("Map size (m)", map.Size, 1000f, 8000f, "0") / 100f) * 100f;
            if (!Mathf.Approximately(size, map.Size))
            {
                map.Size = size;
                changed = true;
            }

            bool fromSeed = frame.ToggleField("Sides picked from the seed", map.PickSidesFromSeed);
            if (fromSeed != map.PickSidesFromSeed)
            {
                map.PickSidesFromSeed = fromSeed;
                changed = true;
            }
            MapLayout layout = session != null ? session.Layout : null;
            if (fromSeed)
            {
                if (layout != null)
                {
                    foreach (MapSide side in Sides)
                    {
                        frame.Line($"{side}: {layout.Boundary(side).Name}");
                    }
                }
            }
            else
            {
                var names = new string[map.BoundaryTypes.Count];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = map.BoundaryTypes[i].Name;
                }
                foreach (MapSide side in Sides)
                {
                    int current = System.Array.IndexOf(names, map.SideName(side));
                    int chosen = frame.Toolbar(side.ToString(), current, names);
                    if (chosen != current && chosen >= 0)
                    {
                        map.SetSide(side, names[chosen]);
                        changed = true;
                    }
                }
                frame.Line("At least one side is an ocean: with none, one is turned into one.");
            }

            if (frame.ButtonRow("Regenerate the map") == 0)
            {
                if (session != null)
                {
                    session.Rebuild();
                }
                changed = true;
            }
            if (changed)
            {
                config.NotifyChanged();
            }

            if (session == null)
            {
                frame.Line("No map session in the scene.");
                return;
            }
            if (session.IsReady)
            {
                WorldPlan plan = session.Plan;
                frame.Line($"{plan.Settlements.Count} settlements · {plan.Roads.Count} roads · {plan.WaterRoutes.Count} water routes");
                frame.Line($"Capital: {(plan.Capital != null ? plan.Capital.Name : "none")} · survey and plan {session.LastBuildSeconds:0.0} s");
            }
            else
            {
                frame.Line(session.IsBuilding ? "Surveying the map…" : "Waiting for a bounded map (Terrain: BoundedMap).");
            }
            if (markers != null)
            {
                bool show = frame.ToggleField("Ground markers (sites and roads)", markers.Show);
                if (show != markers.Show)
                {
                    markers.Show = show;
                }
                if (show && session.IsReady)
                {
                    frame.Line($"{markers.SettlementCount} site posts · {markers.RoadPostCount} road posts near the viewer");
                }
            }
            if (mapScreen != null)
            {
                bool lab = frame.ToggleField("World map (M): resources and reasons", mapScreen.LabLayer);
                if (lab != mapScreen.LabLayer)
                {
                    mapScreen.LabLayer = lab;
                }
            }
        }
    }
}
