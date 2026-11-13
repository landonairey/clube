# clube — Backlog (2026-11-12e)

Feedback given in conversation on 2026-11-12 after batch M's report, from a screenshot of the generated map, kept as it was given. Organized in `Docs/clube-objectives.md` under "Bounded world, models and animals"; the GL ID after each line says where it went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

1. Heads up, I saw a screenshot of the generated map and the terrain gen looks poor, it needs fixing. There's a giant perfect circular mountain in the middle of the map, abrupt-block of mountains to the west, straight line desert to the north, and near straight line ocean edge on the east. This is not natural looking at all. Instead, work with the eroded landscape gen we developed earlier to provide more natural landscapes. → GL637
2. Add variation to the map outline  there could be islands that break up the water edge, penninsulas, or network of islands, or mostly continents. The map should not look like a big rectangle of land mass. → GL638
3. Make the map biomes transition more cleanly into the other types of map edges. For example, a mountain rigline cuts across the playable map and extends out to a larger mountain system, the edge of the playbe edge is not straight. Again, the map boundary should not be straight lines, it should be organic and blend in with the playable landscape features. → GL639
4. The desert boundary type doesnt prevent the player from walking, it lets them walk forever in an endless loop with no option to buy parcels of land, same thing with the ocean as the sea floor drops to a low constant level and just repeats forever, mountains continue on and are walkable but not buildable or diggable even in the debug tool as they only generate surface level mesh and no 3d terrain mesh for marching cube deformation. → GL640
5. And the distant skybox should have matching horizons to these areas. → GL641
6. Yes show ocean blue lines in contour preview. Keep the worldlab show in main title screen → no item (GL636 keeps its sea wash and shore line; the Start button keeps loading WorldLab for now)
