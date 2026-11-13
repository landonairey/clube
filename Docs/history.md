# clube — Build history

What each pass built and where it lives, moved out of `CLAUDE.md` on 2026-11-09 so it is not loaded every session. Search it for a class name or an objective ID (`GL…`, `M…`, `P…`) to find how and where a system was built. New passes add a paragraph at the end.

### Chapters 1–2 — `VoxelLab` and `ChunkLab`
Chapter 1 (`VoxelLab`) is complete apart from V23 (code panel) and the
optional 1E standalone build (V22); the scene stays as the single-voxel
lab. Both labs run on the same Core chunk model: `IVoxelStorage` →
`FlatVoxelStorage`, `Chunk` (single edit path, A7), `ChunkMesher` (mesh
variants as strategy objects picked once per build, A6), `ChunkView`,
and one `WorldConfig` asset per lab (`Assets/Config/`). Lab tools hook
`ChunkView.MeshRebuilt` / `ChunkCreated` rather than adding flags to the
core. VoxelLab extras: `MarchingCubesCases`, `VoxelVolume` (Volume Lab),
and `MeshingRecorder` + the `Step Through` child (`StepThroughLab`;
other lab tools defer to it via `StepThroughMode`).
2A is done (K1–K4): `ChunkLab` scene with live chunk resizing,
`ChunkMeshStats` + FPS shown by `ChunkStatsHud`, and `ChunkTestFill`
(ball, or solid with a shaft) as test shapes that stay alongside K9.
2B is done (K5–K7): `VoxelSelector` picks voxels by click, F frames the
selection, and its corners are edited (now `VoxelCornerEditor`, M19).
2C is done (K8–K10, K29): `TerrainSettings` (in `WorldConfig`) picks an
`ITerrainGenerator` (Flat, Sine, 2D/3D Perlin, fractal 2D Perlin, Spline,
Heightmap; noise seeded via `PerlinNoise`); `ChunkGenerator.Fill` samples
world positions; `ChunkTerrainFill` regenerates ChunkLab on config changes
and reports a missing heightmap as "Generate skipped" in the HUD.
`TerrainDensity` ramps over ±1 unit so extracted surfaces are exact for
voxel sizes up to 1. `HeightmapExport` and the ChunkView "Export heightmap
PNG" button write to `Assets/Heightmaps/` (git-ignored: local test output).
2D: K11 is done. `MeshStorageBenchmark` (*Clube → Benchmarks*) found no
difference between reused lists and arrays, so Core keeps lists; results
are in `Docs/benchmarks.md`. Order changed: 2E, then 2F, then 3A; the rest
of 2D (K12, K32) and 2G come after 3A.
K32 is done: `ChunkMesher` reads densities a Z layer at a time through
`IVoxelStorage.ReadLayer` (every storage scheme must implement it), reuses
the 4 corners shared with the previous voxel, looks up crossed-edge masks
in a table and skips uncrossed voxels unless recording; 64³ went from 81
to 5.5 ms. `MesherSpeedBenchmark` (*Clube → Benchmarks → Mesher speed*)
measures it, and `ChunkMesherTests.RandomChunk_MatchesPolygonisePerVoxel`
guards the output.
2E is done (K13–K16, K33). `TerrainBrush` (Core) adds or removes a sphere
through A7; `TerrainBrushTool` (ChunkLab) picks Select, Dig or Add with
1/2/3 and switches `VoxelSelector` off while editing. *Clube → Build → Game
demo* (`DemoBuild`) builds the playable exe to `Builds/Demo/` (since GL200: the
`MainMenu` scene and WorldLab; see the twenty-third pass below).
K34 is done: the exe opened on a welcome scene (now `MainMenu`); `DemoExitButton` and
Esc in VoxelLab or ChunkLab still return to it, in a resizable 1280×720 window, and the
lab panels fit small windows.
2F is done (K17–K22). ChunkLab's chunk has a `Step Through`
child (off by default; its Inspector and the in-game panel have the
controls), with `StepThroughVoxelLink` (selecting a voxel jumps there, K21)
and `StepThroughCameraFollow` (K22). The recorder
logs a closing `Normals` step. K19's "layer" granularity steps Z slices,
the mesher's outer loop, so playback stays in true build order.
`StepUnits` groups the recorded steps into what playback moves through
(sub-step, voxel, slice; K20 skips empty voxels); `StepThroughLab.Current`
maps that back to the recorded step.
K31 is done: every lab has the one in-game `LabPanel` (see 3A+). The demo
build sets `CLUBE_LAB_BUILD` so step-through recording works in the exe.

### Chapter 3 — Collection of chunks (`WorldLab`)
Since 2026-10-08 (M26, M27) there are two world labs from the same scene setup:
`ProceduralWorldLab` (the streamed world, formerly WorldLab; config
`ProceduralWorldLabWorldConfig`) and `WorldLab`, a fixed 104 m square of chunks loaded once
(`WorldConfig.FixedArea`, `StreamingSettings.FixedColumns`) with walls down its outer faces
(`WorldConfig.SealEdges`: `MeshSeal` meshes those faces' samples as air, mesh only). ChunkLab
has the same walls toggle. "WorldLab" below means either unless it's about streaming.
3A is done (M1–M5). `WorldGrid` converts world positions, global samples,
chunk coordinates and local samples (floored, so negatives work). `World`
(plain data) holds the loaded chunks and the world-wide edit paths:
`SetDensity` writes every copy of a border sample, `ApplyBrush` edits every
chunk a brush reaches, and a newly loaded chunk takes its border from edited
neighbours, so borders stay seamless (M2, M5). Edited chunks are kept in
memory when unloaded. `WorldView` is the chunk manager: streams chunks
around its focus (the camera), nearest first, a few per frame, with pooled
`ChunkRenderer`s; `ChunkMeshBuilder` builds meshes for it and `ChunkView`.
Chunks are cubic, stacked `WorldHeightInChunks` layers from y = 0 (the M17
decision; `ChunkShapeBenchmark` measured it against full-height columns), 16³ as
the plan of record: not every GPU has 32-bit index buffers, and a 16³ mesh fits 16-bit
for any terrain (`ChunkIndexBudget`, guarded by `ChunkIndexBudgetTests`); the world labs use
4 m chunks, 40 layers.
Render distance is a player setting: `PlayerSettings` in
`Clube.Game` (A3), applied by `RenderDistanceSetting` (M3). WorldLab adds
`ChunkFocus` (M4; volume from the chunk tools' `ChunkVolumeStats`), `WorldDebugView` (chunk
borders, each grid edge drawn once with the focused chunk's in the highlight
colour), `WorldLabHud` (position, voxel, chunk; brush volume totals) and the
shared `LabPanel` (incl. chunk size, voxel size, layers and terrain shape);
`WorldViewEditor` shows the live config in the Inspector.
`TerrainBrushTool` edits any `IEditableTerrain` (a `ChunkView` or a
`WorldView`). `TerrainBrush` runs on an `IDensityField` (a `Chunk`, or the
`World` as one field across borders): Hard changes every sample in the
radius, Soft only the surface layer (fills next to solid, empties next to
air), and it returns a `BrushResult` for volume tracking. World chunks use
the opaque `Terrain` material.
3A+ (stacked labs) is done (M18–M21): each lab carries the tools of the
labs below it, from the same scripts. `IRenderedChunk` (Core) is a chunk on
screen (`ChunkView`, or a world `ChunkRenderer` whose edits go through
`World.SetDensity`); `LabChunkTarget` (Debug) gives the lab tools their
chunk: the `ChunkView` above it, or WorldLab's focused chunk (it moves onto
it). The tools live in two prefabs in `Assets/Prefabs/Lab/`: **Chunk Tools**
(`LabChunkTarget`, `ChunkDebugView`, `NormalLines`, `FlipFaces`,
`VoxelSelector`, `ChunkVolumeStats`, the Step Through child) with **Voxel
Tools** nested in it (`VoxelCornerEditor`, `VoxelLabels`, `CaseStepInput`,
the Volume Lab child). VoxelLab removes the selector (the corner editor then
keeps its one stored voxel); WorldLab points the target at `ChunkFocus`,
whose click focuses a chunk and selects the voxel hit. `LabPanel` finds the
lab's parts and draws a section per part, each from a shared `*Controls`
class. K32 at world scale: meshing 1.30 → 0.13 ms per 16³ chunk;
generation is now 97% of loading (K35).
M22–M24: the Inspector is a superset of the panel (`LabPanelEditor`,
and the ChunkView / WorldView Inspectors draw the panel's terrain, chunk
and meshing sections); WorldLab selects a chunk, then a voxel inside it,
with separate highlight toggles; voxel size goes down to 1/16 m (1-16 per
metre) and `BuildGridOverlay` shows a 1 m build grid on the terrain
(`Clube/Build Grid Overlay` shader, an extra chunk material).
K12 is done: `BurstChunkMesher` (Core) is the K32 loop as a Burst job,
picked per config with `WorldConfig.Mesher` (managed stays the default and
is what step-through records); 4-5× the managed mesher, 13× with one job
per chunk in parallel. Benchmark Burst with *Jobs → Burst → Safety Checks*
off; the Editor's checks hide most of the gain.
2G is done (K24-K28): `IVoxelStorage` schemes behind `VoxelStorages` (flat
float, flat byte, run-length X/Y/Z, sparse octree), picked by
`WorldConfig.Storage`; every scheme passes `VoxelStorageTests`, and
`StorageView` (K27) draws runs or octree leaves. Flat float stays the
default; `Docs/benchmarks.md` has the K26 comparison.
M12 is done: the game stores densities as single bytes (`FlatByte`, the
`WorldConfig` default and WorldLab's setting; 0.25× memory at the same
speed across a streamed world). VoxelLab and ChunkLab keep exact floats.
`WorldStorageBenchmark` (*Clube → Benchmarks → Storage at scale*) has the
numbers. Generation dominates streaming (K35).
3B core is done (M6-M8): `WorldView` gives chunks a `ChunkCollider` (MeshCollider
re-baked after each rebuild). The player lives in `Clube.Game` (`Game/Player/`):
`PlayerController` (CharacterController, Input System `Player` map; Left Alt
frees the cursor for the lab panel),
`PlayerSpawn` (waits at the world's top for the chunk column, sets `WorldView.Focus`, lands
on the highest ground under the capsule, respawns after a fall or when found inside solid ground) and `PlayerDigTool` (Attack digs, Place adds; brush radius and
look sensitivity are `PlayerSettings`). In WorldLab, `PlayerCameraToggle` (P,
or the Camera panel section) swaps the fly camera for the inactive `Player`
object and turns the lab click tools off. The lean `Game` scene is the last
section of Chapter 4 (GW1-GW4).
M16 is done: the panel's Player section (`PlayerControls`) scales the
player's gravity in Play mode and sets its jump height.
M25 is under way: picking voxels per metre keeps chunk and world size in metres
(`ChunkSizing`), so terrain stays the same at any voxel size; 4 per metre (0.25 m) leads, pending playtests.
`PlayerBrushPreview` shows the player's brush; `AxesHud` follows `Camera.main`
(WorldLab keeps it on `World`, active in both camera modes); the build grid
draws x and z lines only.
3C is done (M9-M11, M13-M15). `VoxelMaterial` assets (ids: stone 0, dirt 1,
grass 2, copper 3, silver 4, gold 5) are listed by `MaterialRegistry`
(`Assets/Config/Materials/`), which holds the texture array and the render
materials. Material ids live per sample in `VoxelMaterials`, beside the density
storage (`Chunk.SetMaterial`, `World.SetMaterial` for border copies, the brush's
`BrushSettings.Material` for new ground). Generators report depth
(`ITerrainGenerator.Depth`; density is `TerrainDensity.FromDepth`), and
`TerrainLayers` picks grass, dirt, stone by depth. After meshing,
`ChunkMeshBuilder` runs a material pass when `WorldConfig.MaterialDisplay` isn't
None: `VertexMaterialSampler` (solid end of each vertex's edge), `MeshNormals`,
then a `HardSeamSplitter` or `BlendedSplitter` (A6) into a `MaterialMesh` (ids in
UV2, weights in UV3), drawn by `Clube/Terrain Materials` (texture array,
triplanar, debug colours). Views swap their first material through
`TerrainRenderMaterials`. *Clube → Materials* generates placeholder textures and
rebuilds the texture array. WorldLab shows Blended; ChunkLab keeps None.
3D is done (O1-O9; O10 is backlog). Ore materials are in the registry; where
they generate is `OreGeneration` (cell size + one `OreSpec` per ore: nodes per
cell, depth range below the surface, peak chance, σ per axis, hosts, priority) in
`TerrainSettings`. `OreField` places node centroids per cell from the seed
(`VoxelHash`), gathers the nodes reaching a chunk, and `ChunkGenerator.Fill`
rolls each sample against them (hash of the global sample, so borders agree).
`ItemDefinition` assets (`Assets/Config/Items/`) are what materials drop (O6).
WorldLab's `OreDebugView` draws nodes, σ rings and an X-ray of ore samples;
the panel's Ores section (`OreControls`) tunes the specs and shows ore counts
(`MaterialCensus`).
K35 and P1-P3 are done (2026-10-05, branch `architecture/chunk-pipeline`):
generation is Burst jobs (`Generation/Fields` structs wrapped by the
generator classes, `Generation/Fill` jobs ending in `ChunkFillKernel`; one
height per column; uniform all-air or all-solid chunks keep no density
storage), meshing is one Burst job per chunk with materials, normals and the
buffers written into `Mesh.MeshData` (`Meshing/Jobs`; the managed mesher in
`Meshing/Managed` stays for the labs and step-through), and `WorldView` is a
façade over `World` (data), `ChunkPipeline` (jobs), `WorldStreamer` (the
per-frame policy within a main-thread budget; edits rebuild the same frame)
and `ChunkRendererPool` (renderers only for chunks with a surface).
Colliders are cooked in jobs near the focus only. `WorldView` events are
`ChunkLoaded(coord)`, `ChunkUnloaded(coord)`, `ChunkMeshed(coord, renderer)`;
`WorldView.Stats` has per-phase timings; `StreamingBenchmark` (*Clube →
Benchmarks → World streaming*) measures frames. Chunk byte arrays come from
`VoxelArrayPool` and go back on `Chunk.Release` (the world calls it).
**Next:** Checkpoint 3.2 closes Milestone 1; then **Milestone 2 — the first
gameplay loop** (GL1–GL16: pickaxe and breaking stages, inventory, furnace,
anvil, merchant and coins; copper ore → bun → ingot → sold), ahead of the rest
of Chapter 4. Each GL item is the thinnest first pass of a Chapter 5–7
objective; don't build the full versions early.
LA–LE (GL1–GL16) are done. LA: `ToolDefinition` items (Hand, Pickaxe; a
reach in voxels: 1, or 3x3x3; power, hit rate), `VoxelMaterial.BreaksInto`
chains stone/copper → cracked → loose (ids 6–9), `ToolStrike` (Core/Editing)
hits every solid corner of the reached `VoxelBox` on `World` with `StrikeDamage`;
in Game `PlayerToolUser` (Attack hits with the hotbar's tool, else the hand),
`ToolCursor` (shades the mesh triangles inside the reached voxels, GL24; the pickaxe reaches a 4x4x4 ball, `VoxelReach`)
and `PickupNotice`. LB: `Inventory`/`ItemStack` (Core/Items, `MaxStack` per
item), `PlayerInventory` (takes `PlayerToolUser.Collected`), `Hotbar` (1–9,
scroll, Q), `InventoryScreen` (Tab), drawn with `ItemSlotGui`. Player brush
size is on [ and ]; G drops the selected item back into the ground, a tap one, a hold pouring onto one growing pile (`ItemDropper`, `TerrainPile.Pour`, GL27; ores stack 256, dirt and stone 1024). Stand-in art: *Clube → Models → Build station models* and *Clube → Items → Generate icons*. LC (GL8–GL11): `Recipe` assets and `CraftingStation`
(Core/Crafting, work by seconds or strikes), `PlayerInteractor` (E on an
`IInteractable` within 3 m; tools ignore what's behind it), `StationObject`
(furnace, anvil) and `StationPanel`.
LD (GL12–GL14): `Wallet` and `PriceList` (Core/Economy), `PlayerWallet` (coins HUD), `MerchantTable` and `MerchantPanel` (sell 1 or all). LE (GL15, GL16): the loop site (a flat valley over shallow copper; P from the fly camera starts there) and `Docs/20261007 loop playtest.md`. Milestone 2's first pass is complete pending a hand playtest of the controls; the notes list what the next pass should change (shaft lips, getting out of holes, items per swing).
Second pass, building (GL17, GL18, GL23): G drops piles shaped per material
(`VoxelMaterial.PileShape`: dirt cones, stone and ores fill build cells; `BuildGrid`,
`WorldConfig.BuildCellSize`). The stations are `PlaceableDefinition` starting items
(prefabs in `Assets/Prefabs/Stations/`): with one selected, `PlayerBuilder` shows a
preview and a local patch of the build grid (`BuildGridDisplay`), B places it and
holding B on a `PlacedObject` picks it up with its contents. R turns the preview a quarter
(GL28). `KeybindSheet` (Game/Hud) lists the keys in the lower left from the live bindings (F1 hides it, GL29).
Second pass, terrain and stations (GL19–GL22): heightfield chunks get bordered
column heights (`ChunkSampleGrid.BorderedColumns`), so `ChunkFillKernel` can mark
steep columns, which take stone in their top layers (`TerrainLayers` steep angle
and height), and stamp surface rocks (stone; `IsRockColumn` hashes the
global column). A hit on a small piece of a surface rock picks it up whole
(`VoxelMaterial.PickUpPieceSize`, GL25). `CraftingStation` has input and output inventories (2 and 1
slots); `InventoryGridGui` draws the same slot grid in the inventory, station and
merchant screens.
Third pass (GL26): the Landscape generator (`LandscapeHeight`) gives WorldLab plains at 20 m and
ridged ranges about 100 m tall in a 160 m world (40 layers of 4 m chunks); first pass of P12.
Fifth pass (GL30, GL31): trees grow in the density data. `TreeGeneration` (in `TerrainSettings`)
sets them; `TreeField` places at most one per cell from the seed on flat ground, `TreeShape`
builds it as tapered wood capsules (trunk, branches, twigs) and leaf balls, and `TreeStamp`
writes the parts at the end of `ChunkFillKernel` (the deepest part wins per sample; a chunk above
the ground still generates when a tree reaches it). Wood (id 11) and Leaves (id 12) are `Plant`
materials with items, placeholder textures and icons. Branches thinner than about a voxel break up.
Sixth pass (GL32–GL35, `Docs/backlogs/20261009 clube-backlog.md`): volumes. One mined sample is 1 unit;
`ItemDefinition.UnitVolume`. Mined ore carries a `Composition` (shares by volume, on
`ItemStack.Contents`, blended when stacks merge) from its node's grade (`OreSpec.Grade`,
`Gangue`, `ExtraContents`; `OreField.ContentsAt`). (Recipes by volume, with held remainders, were
replaced by fixed batches in GL61.) The furnace burns fuel (`ItemDefinition.BurnSeconds`,
a `Fuel` slot). Clay (13) deposits are surface lenses (`TerrainLayers.Clay`,
`ChunkFillKernel.ClayDepthAt`); dropped clay dries into Brick (14) on a `GameClock`
(`GroundDrying`, `DroppedGroundDrying`), or is fired in the furnace.
Ninth pass (GL47-GL55, `Docs/backlogs/20261013 clube-backlog.md`): the beginning path. `SkillSet` (Core/Skills:
Crafting, Woodcutting, Mining, Smelting; each level costs 50% more xp than the last, 20 for level 1, GL184) lives on `PlayerSkills`, which
awards xp per mined sample (`PlayerToolUser.Mined`) and per finished recipe
(`CraftingStation.Completed`). Recipes give xp and can need a level (`Recipe.UnlockSkill/Level`,
checked against `CraftingStation.Learner`; `StationObject` connects the player). `SkillsHud` shows
levels and names unlocks. The player starts with bare hands (no pickaxe or furnace): wood from
trees, sticks and a pointy stick at the stump, a campfire (`StationKind.Campfire`) smelts
cassiterite (id 19, shallow and at the surface) into tin; the stone pick, hatchet, hammer, furnace
and spear unlock by Crafting level; bronze buns and ingots alloy at Smelting 3. Until Mining 3,
`OreKnowledge` hides chalcopyrite and malachite: the terrain shader remaps their layers to stone
(`_LayerRemap`, set by `MaterialRegistry.ShowAs`, runtime only) and their drops are "? ore",
identified by composition when the level is reached.
Tenth pass (GL56-GL63, `Docs/backlogs/20261019 clube-backlog.md`): recipes are fixed batches, nothing held
between runs (`CraftingStation.OutputFits` says why one waits, `Upcoming` is the greyed preview in
the output slot); `StationPanel` moves made tools to the inventory and, for stations that don't
`WorksUnattended` (worktops, anvils), returns leftover inputs on close. The merchant sells part of a
stack on a slider. Bronze pick, hatchet and shovel are hammered at the anvil (Crafting 4). The parcel
border (`ParcelBorders`) is a fence following the generated ground (`TerrainSurface.HeightAt`,
`WorldView.Generator`) and running down to y = 0, so it shows in holes. GL64 (a smaller inventory)
is waiting on its spec.
Eleventh pass (GL65-GL74, `Docs/backlogs/20261019b clube-backlog.md`): the terrain shader breaks up tiling (`SampleBroken`);
the world labs have a dawn sky (`DawnSky.mat`, sun at 10°). `GrassScatter` (Core/Rendering) puts instanced
grass tufts (`Clube/Grass`) on upward grass triangles of the built meshes near the focus. The inventory
screen sends a clicked stack across (backpack ↔ hotbar, `Inventory.MoveInto`) and drags to a slot.
`OreKnowledge` has groups (copper ore Mining 3, silver 5, gold 7). Q opens the mining journal
(`JournalScreen`, `PlayerJournal`, `MiningJournal` in Core/Skills, `OreSketch`, `VoxelMaterial.Notes`); next
slot is X. `OreDebugView` counts ore 2 ms a frame.
"? ore" shows the stone texture with a question mark and hides its contents (`ItemDefinition.HidesContents`);
ores found before they're known get an uncoloured "Unknown ore" journal entry with their depth (GL75-GL77).
Twelfth pass (GL78-GL85, `Docs/backlogs/20261019c clube-backlog.md`): F3 hides the lab HUD box. A hit also takes any floating
sliver of 3 samples or fewer next to it (`ToolStrike`). Stations start the recipe that uses the most of
what's loaded, and a picked recipe loads its ingredients (`CraftingStation.LoadFor`). Nothing is built on a
stump or on ground rising over 0.5 m across a cell, where the grid floats level. The campfire is a
`TerrainStructure`: stone and wood samples in the terrain around an ember bed, broken by mining any of them.
Thirteenth pass (GL86-GL94, `Docs/backlogs/20261019d clube-backlog.md`, `Docs/20261020*.md` backlogs): the Bronze
chisel (`ToolDefinition.Chisels`, `ChiselStrike`) carves one voxel per hit out of the ground on the voxel grid and
never adds any (GL92). Carved voxels are carve flags on two corners of each voxel (`CarveFlags`, `Chunk.IsCarved`,
`World.SetCarved`; border copies tell each chunk about the voxels just outside it); marching cubes skips them
(`SurfaceExtraction`) and `CarvedVoxels` in the Burst job walls them off with flat faces cut to the neighbour's
cross-section; `SurfaceRaycast` sees the walls; ground put back into a carved voxel fills it in again (the managed
mesher ignores carving). The chisel's cursor is a faint cube on its voxel (`ToolCursor`). A level 3 x 3 patch of
carved voxels floored with stone or rock becomes Flat rock (id 20, `FlatTop`,
`FlatRockCarver`), opened by `StumpWorktops` as `StationKind.FlatRock`. The Cheats section has a debug
material chunk (`DebugMaterialChunk`). Hidden ore drops as a per-ore copy of "? ore" (`ItemDefinition.Variant`,
GL90), so each stacks apart and is identified on its own. The journal shows an unknown ore as an outline,
"Unknown Ore" and the Mining level that discovers it (GL91).
Fourteenth pass (GL95, GL96, `Docs/backlogs/20261020d clube-backlog.md`): a collecting test. The lab panel's Collecting section picks
the cursor style (`ToolCursorStyle`: dot, dot and voxel outline, faces, corner glows, impact sphere) and the collect
mode (`CollectingSettings`: voxel hits, or an area-of-impact sphere that lowers samples gradually or takes them whole,
`SphereStrike`). The dot and the gradual sphere are the defaults.
Fifteenth pass (GL97-GL105, `Docs/backlogs/20261020e clube-backlog.md`): dropped stone (Loose stone) is cobbles; the furnaces are
`TerrainStructure`s like the campfire, primitive tapered shaft furnaces (parts carry their own densities) (*Clube → Models → Build furnaces*, `FurnaceStructures`): Stone
furnace of cobbles, Clay furnace hotter (`StationObject.heat` → `CraftingStation.Heat`). Structures turn with the
placed object. The volume inventory (GL99-GL103; replaced in GL120): a tool slot, a
volume slot (`VolumeMix`, `MixSlotGui`) that every mined sample goes into, and 3 item slots (`PlayerInventory`; `Hotbar`
cells map to slots). Recipes take a cut of the mix (`CraftingStation.LoadFromMix`, minimum share); what's built keeps
its contents and works at its material's share (`MaterialRegistry.Quality`, `StationObject.MadeOf`); Drop pours from
the mix by weighted random. Terrain materials meet in hard edges (`_HardMaterialEdges`). GL104 is part done. The
bare hand takes only the voxel under the dot (GL106).
Sixteenth pass (GL107-GL114, `Docs/backlogs/20261020g clube-backlog.md`): the volume slot is three bins by `MaterialKind`
(Rock, Soil, Plant; `VoxelMaterial.Kind`, `PlayerInventory.Bins`), shown in the hotbar, on Tab and at the merchant
(`PriceList.ValueOf(VolumeMix)`); recipes take from the bin with the most of an ingredient, no minimum by default. In
the gradual sphere a tool takes its full amount from the kind it reaches wide on and 0.2 from the rest, halved off the
material under the dot (GL107). The bare hand picks up a loose surface stone whole with up to 2 neighbours
(`HandPickup`); every sphere and hand hit clears floating slivers (`ToolStrike.ClearSliversAround`). `GrassScatter`
looks only at chunks within its radius (it stalled in streamed worlds). Material stacks show their purity (GL115); there is no
Rock material, surface rocks are stone (GL116); a selected spare tool works and replaces a broken one (GL117). Stacks carry a quality
(`ItemStack.Quality`, GL118) from bin purity through recipes; a tool lasts its quality's share of its hits.
Seventeenth pass (GL119-GL125, `Docs/backlogs/20261021 clube-backlog.md`): the bins are gone (with `MixSlotGui`, `LoadFromMix` and
`PriceList.ValueOf(VolumeMix)`). What a hit gathers goes to a stack of the material under the dot, mixed with what came
along (`Gathering` in Core/Items, `CollectingSettings.SortByTarget`): the stack's contents are the mix, its quality the aimed
material's share; a hit that can't take what it's aimed at (hands on rock) sends each material to its own stack. A mixed
stack drops as its mix (`ItemDropper`). The classic hotbar (9) and backpack (18) is the default again; the tool-slot layout
stays switchable (`PlayerInventory.UsesToolSlot`). Hands take soft ground only, plus loose stones whole. Hard-material piles
finish their build cell first (`TerrainPile.HoldsPile`). Grass is one voxel thick and a fuel; fuel burns for its quality's share.
Eighteenth pass (GL126-GL130, `Docs/backlogs/20261021b clube-backlog.md`): off-kind tool hits take 0.1 (`ToolOffAmount`). A station's
`Quality` (set with its heat by `StationObject.MadeOf`) is a share of every recipe's yield; smelt recipes name the mineral they
extract (`Recipe.Extracts`, `TypicalGrade`) and yield by the ore's concentration against it (up to 2x), the metal coming out
pure; part items carry to the next run (`CraftingStation.YieldOf`, `Carried`; the station panel shows the yield). Anvil and
flat-rock recipes train `SkillType.Blacksmithing` and make nothing better than the smith (`SmithingQuality`: 50% at level 0 to
100% at 10). Items sell at their quality's share (`PriceList`).
Nineteenth pass (GL131-GL144, `Docs/backlogs/20261021c clube-backlog.md`): crafted items of different quality don't stack
(`Inventory.Stacks`; stacks with contents, i.e. gathered mixes, still blend). A stack can hold a part item
(`ItemStack.Part`/`Amount`, GL136): a station's yield comes out as a decimal amount, parts add up on stacking, recipes use
whole items. A gathered stack's quality is `MaterialRegistry.Quality` of its contents (an ore's share of the ore itself).
Bare hands take only loose stones (`HandPickup`: a small piece, `ToolStrike.IsSmallPiece`, or a material with
`VoxelMaterial.PickedFromPiles`, the loose stages) and leave hard slivers (`ClearSliversAround` `canTake`). Broken tools
aren't replaced. Station screens head with "Efficiency xx%" and what it works on. `LoadFor` loads a full stack of each input
(`Inventory.AddToStacks`); swapping in another recipe's inputs drops the picked one. Furnace colliders cover the body and
`PlayerInteractor.TargetIsGround` lets tools still break it. J opens the crafting journal (`CraftingJournalScreen`: Crafting,
Mining, Smelting chapters; `ItemSketch` inks item icons; both journals draw through `JournalBook`). New worktop recipes: shovel,
bronze chisel from a bronze bun and a stick; ingots sell for 30/26/38.
Twentieth pass (GL145-GL152, `Docs/backlogs/20261022 clube-backlog.md`): materials need a tool tier (`VoxelMaterial.ToolTier`, and
`RequiredSkill`/`RequiredLevel`; dirt, grass, clay, leaves 0; wood, stone, ore 1; maple 3 and Woodcutting 3); tools have a
`ToolDefinition.Tier` (hand 0, hand axe 1, stone 2, bronze 3) and `Efficiency` (hand axe 0.3). `PlayerToolUser.Blocked` says why a
hit took nothing, shown by `ToolHint`. `BoulderBashing` (Place, right click, on boulder stone too big to be loose): a stone becomes
a Hand axe; from Mining 2, ore is crushed (`Crushing`: half the rock off) into crushed ore plus crushed stone as a part item; crushed
ore has its own smelt recipes and sells for 1.5x. The pointy stick is gone. `ChunkFillKernel.IsBuriedStone` puts 1-3 sample stones
in the dirt band (`TerrainLayers.BuriedStoneChance`); `HandPickup` takes a stone piece of 3 or fewer bedded only in air or tier-0
ground (`ToolStrike.IsLoosePiece`). Trees are birch (the old Wood, pale bark) or maple (`TreeGeneration.MapleWood/MapleLeaves/
MapleShare`, ids 21, 22); maple wood sells for 2x and whittles into 140% sticks (`Recipe.QualityFactor`; quality goes up to
`ItemStack.MaxQuality` 2). After pulling, the fill jobs' `LayerTable` changed: restart Unity if generation looks wrong (Burst gotcha).
Twenty-first pass (GL153-GL159, `Docs/backlogs/20261027 clube-backlog.md`): inventory drops land on the nearest slot
(`InventoryGridGui.PlayerSlotNear`). The hand (0.1 a hit) and the hand axe (`ToolDefinition.AimedOnly`, 0.2) take only the
ground sample under the dot. Stumps need tier 3. Every stack shows its quality, 100% included. GL157 (tunnel speckles) is open.
P7a (2026-10-27, branch `p7/far-terrain-lod`): far terrain to 10-50 km. `FarTerrainView` (on the world labs' World,
Rendering) draws a geometry clipmap of the generator's heightfield: `FarTerrainBuilder` (Streaming) rebuilds the levels a
move shifts as one batch (`FarTerrainGrid`, `FarTerrainMeshJob` in `Meshing/Far`, heights via
`HeightfieldGenerator.ScheduleColumnHeightsParallel`), and the `Clube/Far Terrain` shader leaves out the chunk columns the
voxel world draws in full (`ChunkCoverageMask`). The panel's Far terrain section sets reach, spacing and level tints.
P7b: Landscape's `DistantRanges` (great ranges beyond 2.5 km; the world labs' fog is now 0.00003).
P7c (2026-10-28): coarse voxel terrain in between. `TerrainLodView` (Rendering, on World) runs `TerrainLodBuilder`
(Streaming): eight rings (`TerrainLodRing`) of 16 x 16 chunks, level l with voxels 2^l times the world's, each a
`ChunkPipeline` on its own `WorldGrid` (fill `ChunkFillSettings.ForLod` widens the ramp and layer bands; layers from
`ChunkPipeline.TryGetColumnRange`), drawn in the world's surface material, a copy per level clipped per pixel (`ClipLod` in
`Clube/Terrain Materials`: the covered chunk columns and the finer level's square). The far terrain starts past it. The
panel's Coarse terrain section sets levels and tints. Edits don't reach coarse levels; P16 is still open.
GL160: maples stand on Maple stump (id 23). GL161: `GrassScatter` drops a rebuilt chunk's tufts at once and regrows
rebuilt chunks nearest first.
GL162: past the chunks, trees are instanced meshes out to 3 km (`DistantTreeView`, `DistantTreeBuilder`: the voxel trees'
own placement, `TreeField.TryGetSpot`/`IsFlatEnough`, heights from `HeightfieldGenerator.SchedulePointHeights`); the coarse
levels stamp no voxel trees. Don't pump `LateUpdate` by hand for long with instanced draws: each draw's GPU buffers are
freed only when a real frame ends (draw once per `Time.frameCount`; a 50 s pump once ran the GPU out of memory).
GL163-GL165, P17 (`Docs/backlogs/20261028c clube-backlog.md`): grass regrows nearest the focus first. `TreeForests` splits the land into
forests (`TreeGeneration.Chance`) and open fields (`FieldChance`; `ForestCover`, `ForestScale`) and scales each tree by a hashed
age (`YoungestSize`); GL166 gives birch and maple their own heights and trunks (`MapleHeight`, `MapleTrunkRadius`). The streamer keeps its wanted area per column (`World.LoadedColumns`, `WorldStreamer.IsWanted`): per chunk,
render distance 64 cost 240 ms every chunk-border crossing. Distant trees draw
only the tiles in view. To time a system, the far terrain, coarse terrain and distant tree views have `LastUpdateMilliseconds`
and `WorldView.Stats.Phases` splits streaming; `FrameTimingManager` reads real frames in Play mode (its GPU time reads ~0 here).
GL167-GL178, P18, P19 (`Docs/backlogs/20261028e clube-backlog.md`): the world draws its chunks only within `WorldView.DetailDistance`
(a player setting, 24 chunks; P18): past it they stay loaded with their renderers switched off (`ChunkRendererPool.SetDrawRule`,
`WorldStreamer.IsColumnDrawn`; edited columns always draw, `World.HasEditsIn`) and the coarse terrain and distant trees draw
there, once `TerrainLodView` sets `CoarseTerrainReady`; the coverage mask counts only drawn columns (`WorldView.DrawVersion`).
The first coarse level is `TerrainLodView.FirstLevelFor` (level 1 in a fixed area, P19). Grass stands on a fixed world grid,
rescatters a rebuilt chunk in the same frame and keeps tufts within three radii across the ground (GL167, GL171). Hits hand
over part items at once (`Gathering.TakeAll`, GL169); hand and hand axe hits mix in the aimed voxel's other materials
(`CollectingSettings.AimedMixFactor`, GL172). The inventory screen has a trash slot and Sort (`Inventory.Sort`); Shift-click
loads half a stack into a station, Ctrl-click one (GL176). Holding Place on a boulder with a mixed stack picks it over
(`Refining`, `BoulderBashing`, GL177). Stations have a recovery (campfire 0.5, stone furnace 0.75, clay 1; GL178).
Twenty-second pass (GL179-GL199, `Docs/backlogs/20261029 clube-backlog.md`): land too far to buy says "You don't own this land"
(`INoticeOnly`: a prompt with no key). The crafting journal (J) has no Mining chapter; an entry is full once unlocked,
outlined a level away, a question mark further off; `ItemSketch` reads icons on opaque backgrounds (the furnaces); both
journals put their glyphs in the font before drawing (`JournalBook.Prepare`). Mining xp per sample is stone 0.08, ore 0.16,
soil 0.02. Crushed ores measure purity as their raw ore (`ItemDefinition.PurityAs`), so refining keeps the ore; a refine pass is
1 s and takes 1 item of impurities. Part items fill a stack to its MaxStack exactly, never past (`Inventory.RoomFor`); the tooltip shows each material's amount. Under one raw ore left, a bash crushes the part.
Wood wins over leaves inside a trunk (`TreeStamp`). `InventoryDrag` is the mouse for the inventory and station screens:
drag (Shift half, Ctrl one), right-click splits (`Inventory.Move(from, to, count)`, `Split`); drop on a station's input or
fuel slot. A smelt's count is what pure mineral gives, so yield is mineral share times efficiency, never over 100%; a smelter
whose picked recipe ran out starts the other loaded ore. Tools: `ReachScale` (bronze 1.3, 3 hits/s) and `SoftFactor` (hatchets
0.15 on leaves). The merchant buys sticks; coins are decimal (`Wallet.Coins` float, `Wallet.Format`). The furnace build preview
is the meshed surface of its samples (*Clube → Models → Build furnaces*, which keeps the body collider).
Sound effects (2026-10-28): `PlayerSounds` (`Assets/Config/Audio/`) holds `SoundSet`s (random clip and pitch) for
footsteps by material (stone, dirt, grass, clay; others by `MaterialKind`), a hit per tool (hand, hand axe, stone pick,
stone hatchet, chisels, other tools; any hit on soft ground or leaves sounds like the material, `PlayerToolUser.HitMaterial`)
and actions (a finished recipe by the skill it trains: crafting, smelting, smithing). The player's `PlayerFootsteps`,
`PlayerToolSounds` (`PlayerToolUser.Struck`) and `PlayerActionSounds` (`PlayerSkills.RecipeFinished`, `MerchantPanel.Sold`,
`PlayerBuilder.Placed`, `PlayerParcels.Bought`) play them on one 2D AudioSource. `PlayerSounds.EffectsOn` (lab panel, Sound
section, `SoundControls`) switches all of it, and is off until the clips are picked properly. Clips are Kenney CC0 in
`Assets/Audio/`; sources in `Docs/audio-credits.md`.
Twenty-third pass (GL200-GL209, `Docs/backlogs/20261030 clube-backlog.md`, `20261030b`, `20261030c`): the demo exe is the game. *Clube → Build → Game demo*
(`DemoBuild`) builds `MainMenu` and WorldLab, writing the version from git into `Resources/BuildInfo.txt` for the build
(gitignored, removed after; `BuildInfo.Version`). `MainMenu` (Game/Menu) has New, Load, Exit over a render of the valley;
New opens `NewGamePage` (mode: only World playable; world name, "Hello World" by default; seed, typed or rolled), Load opens
`LoadGamePage` (saves newest first, the newest selected). `GameSession` (static, Game/Session) carries the mode, seed, world
name and loaded save into the scene; `GameScenes` names the scenes. In WorldLab, `GameSessionHost` (on World) is an
`IWorldConfigOverride` (Core: changes `WorldView`'s play copy of its config in Awake) that sets the seed, restores a save and
captures one; `PauseMenu` is Esc (closes an open player screen first; Back, Save and Exit, Exit Without Saving;
`Time.timeScale` 0; Save and Exit asks for a name, filled in with `<world> - <mode> - Day <##> - <time>` (GL260); Graphics opens
`GraphicsSettingsPage`: field of view 60-120 and a frame rate limit, kept in PlayerPrefs by `GraphicsSettings` and applied by
`GraphicsSettingsApplier` on World, in the Editor only for a menu game; the main menu caps unlimited at 60, GL209). A menu game starts as the player (`PlayerCameraToggle`). Saving (first pass of S1-S4) is `Core/Saving`:
`GameSave` (plain data, ids not references), `GameSaveFile` (header plain, body gzipped, `SaveHeader.CurrentVersion` 2: names
added; version 1 still reads), `WorldSaving` (edited chunks; `World.EditedChunks`, `World.Keep` puts them back before
streaming), `StackSaving`, `SaveNames` (defaults, file names); in Game `PlayerSaveState` (on Player), `PlacedObjectSaving`,
`ItemLookup` (catalog, then `OreKnowledge.DisguiseById`), `SaveSlots` (one file per save, named after it, in
`Application.persistentDataPath/Saves/`; the same name replaces a save). Restore hooks: `PlayerSpawn.PlaceAt`,
`PlayerParcels.Restore`, `GameClock.Restore`, `TerrainStructure.Restore`, `SkillSet.SetExperience`, `MiningJournal.Restore`,
`GroundDrying.Restore`. When testing by pumping `SendMessage("Update")`, note it also runs disabled components (a disabled
`PlayerController` then resets the head's pitch); step real frames to check look angles.
Twenty-fourth pass (GL210-GL230, `Docs/backlogs/20261030d clube-backlog.md`): defaults are render distance 32, a 300 m fixed area and
80° field of view; the graphics popup also sets render distance and freezes the sky at an hour; the lab panel starts hidden
(F2). Place no longer adds dirt in play (`PlayerDigTool.placesGround`). Q opens the field log (`FieldLogScreen`): tabs for
Quests, the ore journal (`JournalScreen`) and the tool journal (`CraftingJournalScreen`, starting with the stump and the
boulder); both journals are now page drawers, not screens. Quests: `QuestLog` (Core/Quests) of main and side quests,
ticked by `PlayerQuests` (inventory checks and events), shown by `QuestHud` under the lab HUD box (`HudLayout`; the lab HUD
reports its bottom), saved at the body's end (`SaveHeader.CurrentVersion` 3). C crouches, and slides out of a sprint
(`PlayerController`). Boulder refining and crushing hold Place, an item every 0.25 s (`BoulderBashing.secondsPerVolume`).
`DayNightCycle` (on World) lights the sky from `GameClock` (a day is 2400 s, so clay dries in 1 day) and works on copies of
the skybox and ambient settings. `DryingClayEffect` steams drying clay. The station screens have a trash slot
(`InventoryGridGui.DrawTrash`). Silver and gold smelt in every furnace and hammer into ingots; the flat rock makes bronze
tools; the brick furnace (heat 2.25, 100 bricks, Crafting 6) is built by *Clube → Models → Build furnaces*. The starting
inventory is empty and the merchant stands in the starting parcel. The exe runs in the background. The load list deletes
saves. Stumps open from the foot of the trunk (`StumpWorktops`, GL231); `PlaceSketch` draws the stump and boulder entries (GL232).
Twenty-fifth pass (GL234-GL239, `Docs/backlogs/20261030f clube-backlog.md`): varied flora, drawn meshes on the built terrain, not voxels.
`GrassGrowth` (Core/Generation/Flora, on `GrassScatter`) grows grass up to twice as tall in open meadows (by
`TreeCover`, the trees that grow within 10 m), shorter high up, and wheat in patches of open land (`ValueNoise`, shared with the forests); the
`Clube/Grass` shader turns tall instances to straw. `BushScatter` (Rendering, on World) grows small, medium and large
bushes where `BushGrowth` puts them (`BushMeshes`, `Bush.mat`, no collider); both scatters find their spots through
`GrassyGround` and draw through `InstanceBatches`. A rebuilt chunk breaks bushes whose ground went (`BushScatter.Broken`);
`PlayerToolUser` picks the bush aimed at on Attack (`BushPicked`), and `PlayerBushes` hands over leaves and sticks. Picked and
broken bushes are saved (`GameSave.RemovedBushes`, save version 4). Wind: `Assets/Shaders/Wind.hlsl` for grass, bushes, the
voxel leaves (`VoxelMaterial.WindSway`, `_MaterialSway` from `MaterialRegistry`) and the distant trees.
Twenty-sixth pass (GL240-GL251, `Docs/backlogs/20261030g clube-backlog.md`): a stump opens only once the wood above it is cleared
(GL231 reverted). In the sphere modes a tool on ground it isn't made for (a shovel on stone) works only the sample under the
dot. (GL245's purity tolerance was replaced by GL256, below.) Stations
load the best stack first (`SlotsByQuality`); the merchant sells part items (`Inventory.RemoveAmount`). Slides last while C is
held, up to 8 m, never up a steep rise. The sky: `SkyPositions` (Core/Sky) puts the sun (under the horizon at night) and the
moon (its own 29.5-day orbit and phases); `DayNightCycle` lights from either, `MoonDisc` (`Clube/Moon`) draws the moon.
The wooden chest (`StorageChest`, `ChestPanel`, `StorageCapacity`: 27 slots at 100% wood down to 5) is a placeable from
100 wood at the stump. Birds: `BirdFlock` (Core/Fauna) and `BirdFlocks` on World. Gold shines (`VoxelMaterial.Shine`,
`_MaterialShine` in `Clube/Terrain Materials`, glints in its texture). Silver and gold buns are called buttons.
Twenty-seventh pass (GL252-GL264, `Docs/backlogs/20261031 clube-backlog.md`): the sky. `CloudLayer` (on World) draws low-poly clouds (GL277:
faceted puffs with flat bottoms, instanced over a wind-drifted grid round the camera, `Clube/Low Poly Clouds`), lit by the sun or
moon as `DayNightCycle` gives it; `SunDisc` (`Clube/Sun`) draws the sun as a ball of light whose 12-sided outline shines through
clouds in front of it, and the moon is a 12-sided faceted disc (GL278); wind and cover change with the game's days. `StarSky` (`Clube/Stars`)
is turned by `DayNightCycle` once a sidereal day (`SkyPositions.SiderealAngle`). The moon is 2.16° and grows a quarter near
the horizon (`SkyPositions.MoonIllusion`). Birds come every 10-30 s, 25-80 m off, 10-35 m over the ground. Draw order on
the sky: stars (Transparent-450), moon (-400), clouds (-300). The lab HUD ends with the game time and day (GL259).
Also in this pass (GL256-GL264): stacks mix again when digging and when dropped by hand (`Inventory.Combines`); only what
comes out of a crafting station keeps apart from other qualities (`StacksApart`, `keepApart` on `Add`/`MoveTo`). Grass reaches
64 m, thinning past 22 m, and grass ground takes the tufts' colour from afar (`GrassLawn`, `GrassLawnMap` round the focus,
`Assets/Shaders/GrassLawn.hlsl` in the terrain, far terrain and grass shaders), so the tufts end without a line. Distant
trees have four levels made from real `TreeShape` trees (`DistantTreeMeshes.BuildLevels`: every leaf ball to 220 m, leaf
clusters to 520 m, crowns to 1100 m, one blob beyond; six variants per kind), cross-faded by dither in `Clube/Distant Trees`,
in the voxel trees' texture colours. `SkyWeather` (Core/Sky) gives each sunrise and sunset a colour and strength and each day
its cloud cover; `DayNightCycle` lights the clouds itself (`_CloudLightDir`, `_CloudLightColor`), by the sun a while after it
leaves the land. The main menu reads "Extract, Refine, Craft, Build your World". Dahlias (GL265, `DahliaGrowth` on
`GrassScatter`) grow rarely among the grass over about a third of gold nodes, a patch of a few flowers mostly of one colour. Birds come mostly
in flocks (GL266). Any shader drawn in the world needs a DepthNormals pass (GL267): with SSAO the depth texture is built from
them, and the clouds stop at it. Terrain relief, held tools and sound (GL268-GL272): `Clube/Terrain Materials` roughens
rock and soil by `VoxelMaterial.Relief` (drawn only); `HeldToolView` (Game/Tools) holds and swings the tool; `ToolCooldown` puts
a second between uses (hands and dawn a quarter quicker); `SoundSynth` (Core/Audio) makes every sound effect, written by *Clube →
Audio → Generate sound effects*, and `AmbientSounds` (on World) plays wind, leaves and birds. Hits throw low-poly crumbs of the material (`HitDebris`, GL274); the
stone pick is a stone lashed to a stick (GL273).
Twenty-eighth pass (GL279-GL291, `Docs/backlogs/20261101 clube-backlog.md`): a gradual hit gathers the volume the mesh lost
(`RegionVolume` in Core/Volume, by the tetrahedra before and after; GL288), and the pickup notice shows what went in. Clouds
come in four kinds (cumulus, cumulonimbus, stratus, cirrus) and drift by game time; the moon's phase edge is a polygon line.
Tool tiers: hand axe 1, stone 2, copper 3, bronze 4; copper and bronze tools are mended by dropping their ingot on them
(`ToolRepair`; a share of each mended hit is lost for good as quality, grey on the bar). Torches (`TorchDefinition`, an unlit
and a lit item; `TorchBurner`, `PlacedTorch` in Game/Torches) burn by game time (`GameClockTicks`); charcoal is cooked from
wood or sticks. The sleeping roll (`SleepingRoll`, `RestScreen`, `RestBlur` in Game/Rest; Clube.Game now references URP)
rests 1-24 hours at one a second (`GameClock.Advance`). Placed objects keep a wear (save version 5). The chest is a crate.
Slope ridges are generated into the density (GL293: `SlopeRidges` in the heightfield fill, by column gradient); the
terrain shader adds only fine pitting in the shading (GL268).
Torches light the terrain, grass and held items (GL294: `AdditionalLights.hlsl` in their shaders); a torch is held
until B starts placing it. E also opens the inventory (GL298); fuel clicked at a fire goes to its fuel slot (GL299);
right-click pulls half a station or chest slot (GL300). A swing at nothing whooshes, a refused hit errs (GL302);
placed torches have a flickering flame (GL304); Ctrl-click at the merchant picks stacks to sell together (GL305).
Thirtieth pass (GL306-GL323, `Docs/backlogs/20261102b clube-backlog.md`, `20261102c`, `20261102d`, `20261103`, `20261103b`): water and falling ground. `Hydrology` (Core/Generation/Water)
drains the heightfield over `WaterGeneration.AreaSize` (640 m round the origin; start area kept dry) into a `WaterMap` (1 m
cells: river cut, water surface and depth, kind, flow, distance to lake and river water), cached per world shape.
`LandscapeHeight.Height` subtracts the cut, so river beds are in the terrain data and every height reader agrees;
`ChunkFillSettings.Water` lets `ChunkFillKernel.StampWaterBanks` lay sand (id 24, GL306) on beds and lake shores and clay on
banks and deep lake floors; `TreeField.TryGetSpot` keeps trees off. Jobs refuse unassigned arrays even unread, so "no water"
is `WaterMap.None` (empty arrays), never `default`. Dirt and grass drop, sand also slides
(`VoxelMaterial.Fall`): `GroundSettling` (Core/Editing) wakes on `World.DensityChanged` and `FallingGround` (on World) steps it
(GL308). Water is voxel data (GL315): a byte per sample beside density and material (`Chunk.GetWater/SetWater`, null while
dry, `WaterVersion`; `World.GetWater/SetWater` writes border copies, `World.TryGetOwner` is the quick per-sample lookup). `WaterView`
(on World) fills each loaded chunk from the map (`WaterFill`), steps `LiveWater` (Core/Editing: falls first, spreads to lower
neighbours, heads for the nearest drop within 5 samples, thin water dries, generated water is an endless source, ground replaces
water; GL313, GL315), and draws each wet chunk's water with `WaterMesher` (Core/Meshing/Water: marching cubes, solid samples
take the water beside them so the surface runs into banks) in `Clube/Water` (each triangle a facet: bands down runs, faster where
steeper, slow twinkles on still water); past the drawn chunks it draws the map as flat sheets. `ChunkDebugView` marks water samples. Water work is time-budgeted (`buildBudgetMs` 1.5, `flowBudgetMs` 2.5; `WaterView.LastUpdateMilliseconds`; `Docs/benchmarks.md`).
Swimming (GL316): `PlayerSwimming` (from `WaterQuery`) sets `PlayerController.Submersion`; `UnderwaterTint` tints the screen.
Torches burn with a `FlameCluster` of dancing low-poly tongues (GL314). `DemoBuild` rebuilds the gitignored texture array (GL317).
GL318: `WaterFill` fills to the real ground and runs lakes over low shores (dry cells only, across chunk faces); a cell's
water never rises over its own level; slope ridges fade by water (`WaterMap.ShoreFade`); the mesher smooths thin runs
and marks bank triangles for foam (UV3). GL319: the wooden bucket (`BucketDefinition`, water in `ItemStack.Wear`;
`WaterScoop`; `PlayerBucket` on Player, Attack scoops, Place pours). GL320: `Hydrology.ContainLakes/ContainRivers` hold water
to the 1 m ground; `LiveWater` pours over edges straight down and works out from `LiveWater.Focus`. GL321: brick mix (sand +
clay) fires into refractory brick (ground id 25), which builds the refractory furnace (heat 3). Clay falls (GL322).
GL323: far water sheets are wet cells only, clipped by a coverage mask where chunks draw; water is never deleted; pours pool
(`WaterScoop.Pour`); `WorldStreamer.DrawRadius` shrinks the drawn chunks with the focus's height over the world.
Testing gotcha: pumping the World object's LateUpdate by hand from high up crashed the Editor (GPU out of memory) twice.
GL324: a pour sinks through water and fills no higher than a sample over the puddle's surface; downhill water takes its
thin remainder along and thin water on the flat joins the wettest beside it (`LiveWater.Gather`), so no specks are left;
the bulk load drops to `farLoadingBudgetMilliseconds` past the drawn columns; distant trees re-collect only when the view
moves. GL325: downhill water takes one way (`LiveWater.PickPath`: wet first, then the deepest fall, then a place hash), so
spills run in veins; water moves in whole 1/255 steps (`LiveWater.Steps`), since rounded moves made water out of nothing. GL326: water drops up to `LiveWater.FallReach` (2 m) through empty air per step.
GL327 (first pass of SM1): ground has a stability: 100% on a 3 m solid column, the same resting on a sample, less
`VoxelMaterial.stabilityLoss` per sample out or hanging (rock 0%: holds while joined to standing ground, so cave ceilings
hold and only cut-off pieces fall, whole; dirt/grass/clay 34%: two out; sand 100%); at 0% it falls (`GroundSettling`,
`GroundHold`). GL328 (crushed ground) is open.
GL329: `StationFire` (on the campfire and furnaces) brightens and flickers the Glow light while the station smelts; the
campfire also shows a `FlameCluster` and smoke. GL330: only a lone trickle moves whole downhill; water fed from behind
(`LiveWater.IsFed`) evens out with the sample ahead, so a dug trench fills as one stream. GL331: ground changes wake the water
flow only where there's water beside them (a big dry dig once left 200k samples sorted every step, 170 ms frames).
GL332: water comes in sixteenths (`Chunk.WaterLevels`, `LiveWater.Quantum`); it spreads on the flat only while two steps deep,
evens out one neighbour at a time, and water pressed from above pushes the last step; downhill needs `LiveWater.MinFlowSlope`
(0.25) over 2 m ahead or a 2-sample cliff. `WaterScenarioTests` measure pours, slopes, a fed channel and a flood at game scale.
GL333: only rivers are sources (`WaterFill.RiverAt`), holding their level both ways; lakes and ponds are finite. GL334: wet clay
doesn't dry or steam (`GroundDrying.IsWet`). GL335: the bucket scoops the shovel's sphere (`CollectingSettings.ToolRadius`).
GL336: rivers stand at least `WaterMap.MinRiverDepth` over their bed. GL337 (clumps on hills) and WT1 (moisture) are open.
P12a, P12b, WT2 (branch `p12/eroded-landscape`, `Docs/backlogs/20261103j clube-backlog.md`): the ErodedLandscape generator runs
`ErosionFilter` (Generation/Noise, after runevision's erosion filter) over Landscape with smooth massifs
(`LandscapeHeight.WithSmoothMassifs`; the filter needs a smooth base: on ridged noise its gullies turn to noise and seams);
panel Erosion section. Rivers start in its biggest gullies' valleys (`HeightfieldGenerator.ScheduleValleys`, `Hydrology` by
steepest descent) and merge downstream into lakes. River water comes from the chunk's soil (`Chunk.SoilWater`, `LiveWater`
`Draw`/`Soak`), kept full by `HoldSoilWaterFull` until there is rain (needs more playtesting). GL338: the merchant table skips
and clears trees on its square, and is placed again when the world regenerates (GL341). Large boulders (GL342) are 3D shapes
from `BoulderField` (Generation/Rocks), 4-12 m, faceted, often overhanging, stamped as `TreePart.Boulder` with the trees.
Grass reaches the drawn terrain (`WorldView.DrawnDistance`, GL340). The world labs default to ErodedLandscape (GL343). AR1: palette idea.
GL344 felling: `PlayerToolUser.Chopped` → `TreeFeller` (Game/Tools, on Player) → `TreeFelling.FindSevered` (Core/Editing: wood cut off
from the ground through wood, plus the leaves nearer to it) → a mesh from a scratch chunk → `FallingLimb` tips it to the player's right
(cardinal), dust, then `TreeFelling.Land` (turned 90° about the cut, dropped per column, wood under a tenth of the leaves).
GL349-GL371 (`Docs/backlogs/20261103o clube-backlog.md`): landed wood keeps its density, and a cut piece that already lies there drops
straight (`TreeFeller`); leaves land as their `VoxelMaterial.FallenForm` (ids 26, 27: no sway) and rot away 10% an in-game
hour (`FallenLeaves` on World). A finished run that doesn't fit the output waits (`CraftingStation.OutputFits`); the flat rock
mends a tool fully with one ingot (`ToolRepair.RepairFully`); `ItemStack.MinLoosePart` (0.05) clears specks. Torches go in the
open voxel before the aimed face (`PlayerBuilder.BuildTorch`). `CampfireBounce` knocks the player back (`PlayerController.Knockback`).
`InteractHighlight` (on Player) outlines the interact target with `Clube/Interact Outline`. Tab no longer opens the inventory.
GL372-GL378 (`Docs/backlogs/20261104 clube-backlog.md`): the interact outline is a stencil-masked 1.5-pixel rim (`Clube/Interact
Outline`, mask and rim materials made in `InteractHighlight.Awake`); a model's "Outline shape" child replaces its meshes for
it (the merchant table's). Smelted metal keeps slag (`CraftingStation.SmeltPurity`, `SlagCarried`: ore share, heat, Smelting),
so a bun's quality is its purity; hammering drives out half the slag (`ForgedQuality`). Crushing keeps volume (the crushed
ore is the rest of the item). `PlayerController.Knockback` keeps its hop on the frame it starts.
GL379: `GrassScatter` thins far tufts on the CPU (tufts sorted by a hashed pick, a far chunk sends the share it keeps) and resumes its chunk scan where it stopped; `InstanceBatches` passes each batch's bounds.
GL380-GL383: far grass is four-tuft clumps drawn without depth passes (`GrassScatter.FarMaterial`); fallen leaves drop like dirt and `FallenLeaves` follows them down; `SoundSynth.Chip` plays on `BoulderBashing.HandAxeMade`.
GL384-GL398 (`Docs/backlogs/20261104c clube-backlog.md`): Q and E switch between the field log and the inventory. Buns and ingots stack to 32;
a station's output blends qualities by amount (`CraftingStation.BlendsInOutput`), and a dragged stack moves onto another by amount,
parts included. Refining takes one impurity a pass and fails more the purer the stack (`Refining.FailChance`); crushing and refining
crushed ore train the new Prospecting skill. Crushed silver and gold ore. A campfire's or furnace's ground samples target the station
(`TerrainStructure.FindAt`); idle stations don't glow. A tap of Drop places one sample on the aimed spot (`TerrainPile.PlaceOne`).
Bucket capacity follows quality (`BucketDefinition.CapacityOf`). `PlayerWaterSounds` splashes, sloshes and drips.
Height maps: `Docs/images/20261103 *`.
Town center (GL399-GL407, `Docs/backlogs/20261104d clube-backlog.md`, `20261104e`; branch `gl399/town-center`): a "Town" object in both
world labs (`TownSite`, Game/Town) searches for a site near tin, copper, silver and gold nodes and water (`TownSiteSearch` in
Core/Generation/Settlements, 4 ms a frame; inside the fixed area, or within 1 km of the spawn), builds the town (`TownBuilder`:
`TownLayout` plan, `TownMeshes` models in `Clube/Low Poly`, built with `LowPolyMeshBuilder`, Core/Meshing/Models) with its gate
facing the spawn, and levels it once its chunks load (`GroundLeveling`, Core/Editing: dirt between cobbled paths, `TownLayout.IsPath`).
Five shops (`ShopSpecialty` assets in `Assets/Config/Economy/Shops/`, prices from the one merchant list, 1.5x to buy, wares 2x)
each have a `Shopkeeper`: an `ITrader` for `MerchantPanel` (lessons for coins, `SkillTraining`) and an `IClosableInteractable`
(a notice while shut), following `ShopHours` (counter 9-17, chores, asleep 18-6) by the `DayNightCycle`; `KeeperFigure` poses
the body in code. Nothing about the town is saved; it follows from the seed and the clock. `TownGate` opens with E.
Second pass (GL408-GL415, `Docs/backlogs/20261105 clube-backlog.md`): the town fills one 32 m land parcel (sites on parcel centres),
locked (`LandParcels.Lock`: never bought, so never dug). `TownPlan` (Core/Generation/Settlements) works out the frame, each shop's
pad height, the wall's stones and the mine shafts once; `TownStructures` has their shapes. The wall is marching-cubes stones
stamped into the terrain; the shops are square voxels of stone and Oak/Maple boards (materials 28, 29, `VoxelMaterial.BuildsSquare`;
`SquareVoxels` in Core/Editing: a block's corners solid and the voxels round it carved, GL92's flat walls; the player places boards
the same way with a tap of Drop); only the shops' pads are levelled, the square keeps the land's shape. Four stair shafts spiral down
to the nearest tin, copper, silver and gold nodes (heads inside or up to 10 m outside the wall), lit by lanterns; after work the miner
walks down one and takes a sample of the node every 6 s (`NodeMining`). Keepers walk 3D routes over the terrain (`TownContext`).
Sign words use `Clube/World Text` (the built-in font material shows through walls). Board textures stay straight in the terrain
shader (`_MaterialRegular`).
Third pass (GL416-GL426, `Docs/backlogs/20261105b clube-backlog.md`, the first playtest): 60 fps at 1440p (GL416): the grass draws from a
GPU buffer of matrices (`InstanceBatches.DrawFromBuffer`, procedural instancing in `Clube/Grass`), newly meshed chunks join a
waiting list rather than calling a full look, and new grass is batched in 4 times a second. Measure the built game with
`clube.exe -townbench -screen-width 2560 -screen-height 1440` (`TownBenchmark`, writes `townbench.txt`; the exe segfaults on quit,
after writing it). The wall is one solid band of Loose stone 0.9 m high (`TownStructures.StampWall`); stairs are a 4 m spiral with
landings, every side whole (steps shorter than the capsule's radius stuck the player); pads sit on the voxel grid. The anvil,
furnace, sleeping rolls and counter items are copies of the game's own models (`ModelCopy`: meshes and colliders, no scripts;
tools via `HeldToolMeshes.Build(tool, withHand: false)`); the prospector shows square ore blocks in the terrain. Shafts are lit by
everlasting torches (`PlacedTorch.Everlasting`, `PlacedObject.Fixed`); torches are 3/4 their old height. Real trees and a woodpile
of wood logs by the lumber shop (`TownPlan.LumberYard`, written by `PartStamp` in Core/Editing). The town is grass. Keepers buy
from a purse (`ITrader.Purse`) of 525 coins a day. `TownSite` writes the town once, checking for the blacksmith's oak floor
(`IsWritten`).
To profile real frames in Play mode, hook `EditorApplication.update` and read `ProfilerRecorder`s (Internal
category: `PlayerLoop`, `Update.ScriptRunBehaviourUpdate`, `PreLateUpdate.ScriptRunBehaviourLateUpdate`), logging to the
Editor log; per-script markers aren't available without the profiler.


Week of 2026-11-09, batch A (GL428-GL445, branch `gl428/batch-a`): a new world starts at 7:00 (`DayNightCycle.startHour`);
the pause menu shows `BuildInfo.Version` under its box; `CampfireBounce` only throws the player off a lit fire
(`StationFire.IsLit`). Qualities read rounded down (`QualityText.Percent`), so only pure reads 100%. Part fuel burns for its
part and is used up (`CraftingStation.Ignite`). `GameUi.OutlinedLabel` outlines slot counts, purity and the cursor prompts.
Ctrl-click at a trader picks every stack of the item (Ctrl+Shift one stack). The generic Pickaxe item is gone. Ore picked out
of crushed ore comes out crushed (`BoulderBashing.CrushedFormOf`); `ToolHint` pops and fades each message. Smelts always show
their yield; picking another recipe moves the output to the inventory. Saves have named values (version 6,
`GameSave.Values`): a component implementing `ISavedValues` (Game/Session) is found by `GameSessionHost` and keeps a few
keys, so small state needs no new save version. `PlayerVolumeLedger` (on Player) counts lifetime volume collected and placed,
shown as "Volume" on the lab HUD. The quest box draws over open screens. `JournalBook.FitLabel` shrinks journal titles and
notes to fit. The player camera's near plane is 0.08 m (it was 0.3, the capsule's radius, so the near plane reached through
walls).

Batch D (GL446, GL447, branch `gl446/onboarding`): `QuestHud` draws a sketch on a paper card beside the hand axe, gather wood
and campfire quests (`PlaceSketch.Boulder`, `Tree`, `Stump`) and a "Tip" box under the quest box from
`PlayerQuests.TipFor` (the first eight main quests).

Batch B (GL448-GL456, branch `gl448/crafting`): `StationPanel` is two halves over the inventory, the recipe list on the left
(scrolls; `Recipe.IsInSightFor` shows what's unlocked and, with a padlock, the next level's; names say what from when two
make the same thing) and the active recipe on the right (title, details, inputs with a red X and "Need N more" for what's
missing, work, output, fuel). The stump makes copper picks and hatchets ("(worktop)" recipes); Refractory brick and Brick mix
read Firebrick and Firebrick mix. The workbench (*Clube → Models → Build workbench*, `WorkbenchStructure`): 100 oak boards
at the stump, a `TerrainStructure` table of 44 board samples that is a worktop with every stump recipe. `BoulderBashing`
also works the flat rock (`flatRock`), two batches a cycle. `SkillType.Goldsmithing` is added (no recipes yet). Coffee is a
`ConsumableDefinition` (Core/Items): `PlayerConsumables` (on Player) drinks it on Place, multiplies
`ToolCooldown.SecondsFor`'s boost for its game hours, shows a line over the hotbar and saves through `ISavedValues`; the
trading post merchant sells it at 50c (listed 100 per 3, times the town's 1.5).

Batch C (GL457-GL460, branch `gl457/prospecting`): `ProspectReading` (Core/Items) reads gathered stacks below Prospecting 10
as grades (`Grade`: PURE, High Grade, Good, Medium, Poor, each a colour), the stack's own material first, one more component
a level, the rest "?"; `ItemSlotGui.ProspectingLevel` (set by `PlayerSkills`) switches the slot corner to a short grade and
the tooltip to rich-text grades. `PlayerProspecting` (on Player): bare-hand Place reads the aimed sample (ore contents from
`OreField.ContentsAt`) through `ToolHint`, with Prospecting xp once per sample (saved). `BoulderBashing.OreCrushed` drives a
`SoundSynth.Crush` sound (`PlayerSounds.Crushed`), a `HeldToolView` strike and ore fragments. `HitDebris` splashes crumbs in a
cone out of the struck face (towards the head), floors each crumb on the ground below it ignoring the player, and throws
sparks (the torch flame material) off rock and crushes.

Batch I (GL461-GL463, branch `gl461/ui-settings`): `Minimap` (Game/Hud, on Player) draws a north-up top-down view in the
top-left corner from an orthographic camera that renders into a 256 px texture every 0.4 s (enabled for one frame at a
time); `HudLayout.ReportMinimap`/`BelowMinimap` put the lab HUD box under it, and `TopLeftFree` keeps last frame's whole
stack so report order doesn't matter. `DisplaySettings` (Game/Settings) has the 16:9, 21:9 and 32:9 presets and the window
mode (Window, BorderlessWindow, FullScreen = `FullScreenWindow`); borderless strips the window frame through user32 in
Windows builds only. The Video page draws them first. `ParcelBorders` draws maple wood posts and two rails
(`LowPolyMeshBuilder`, the LowPoly material) above ground and a faint band below it.

GL478 (branch `gl478/lick-ore`): `PlayerProspecting` also licks the selected ore on Place when not aimed at a boulder
(`BoulderBashing.CanBash`): one item (or the part left) is used up for Prospecting xp (1, or 3 for an ore not yet told
apart) and `ToolHint` says its grade.

GL464-GL466 (branch `gl464/sky-nature`): `SkyPositions.SunHourAngle` runs the sun 04:00-20:00 (16 h up, 8 h down; faster in
the dark), so `DayNightCycle`, stars and moon follow; `ToolCooldown.DawnFrom/To` is 02:30-07:30 and `ShopHours.Default`
sleeps 21:00-04:00. `Fireflies` (on World; `DuskLight.Glow` from the sun's height, evening only) keeps a pool of instanced
specks seated on grass (`GetMaterial`) round the camera. `RiverTrout` (on World; `TroutJumps.Activity` by hour: most before
09:00, least after 15:00, none at night) starts instanced leaps from deep `WaterMap` river cells still wet by `WaterQuery`,
with a spray and a 3D `PlayerSounds.Splash`. Tests: `SkyNatureTests`.
GL467-GL469 (trees and climbing): `PlayerController.Climb` climbs a face steeper than the slope limit in front (two sphere casts,
chest and knee height) while Jump is held in the air once a jump's rise is spent, at `climbSpeedShare` 0.25 of the walk, through
the CharacterController; letting go drops off, past the top it hops onto the ledge (`IsClimbing`). `FellingYield` (Core/Editing)
caps the wood a felled piece keeps at 10 x 1.1^Woodcutting level: `TreeFeller` meshes the whole piece, then `CapWood` keeps the
wood nearest the cut (`FelledLimb.WoodLost` counts the rest), so landed wood is what can be gathered; re-cut landed wood isn't
capped again. `FallingLimb` starts a bigger dust cloud just before landing (sized by the piece, thick from its first frame) and
keeps the mesh 0.7 s under it; `FellingBurst` throws up leaves that shrink away, and splinters for lost wood. Tests: `FellingYieldTests`.

Week of 2026-11-09, batch E (GL470-GL475, branch `gl470/trading-post`): the town is a trading post with a name on a wooden sign by
its gate (`SettlementNames`, Core/Generation/Settlements, first pass of ST8: world seed and parcel). `TradingPost` (Game/Town,
`ISavedValues`, added to the Town object by `TownSite`) is the only saved part of a settlement: each keeper's `Disposition`
(Core/Economy: starts at 10, +1 per 100c of trade (sales, purchases, lessons), each point under 100 is 1% markup and 1% off what
they pay; `PriceList.Scaled`/`Share`, shown in `MerchantPanel`), the `PropertySale` (the merchant offers the post for 10k through
`ITraderDeal` once the player has it; owned, `LandParcels.Unlock` frees the parcel and it pays 500c a game day), and what's left in
the cave's crates. `TownStation` keeps the smithy's anvil and furnace as looks with a notice until the blacksmith reaches 25, then
swaps in the real stations. Tin's node within `OpenPit.MaxDepth` (6 m) of the ground, with room, is an open pit (`TownShaft.IsPit`):
grass cleared to dirt at the start, and the miner digs `OpenPit`'s cone (Core/Editing: 45° walls, centre and top first) 16 samples
a swing, standing on the new floor. `TownCave` (Core/Generation/Settlements) is a round room under the back of the square (or a
side clear of the mines) with a tunnel up to an opening outside the wall; `TownBuilder.BuildCave` puts three crates
(`TownSite.cratePrefab`, the wooden chest) of the shops' dearest goods, an everlasting torch and the thief (`KeeperFigure.BuildThief`,
`TownThief`) in it. Tests: `TradingPostTests`.

Batch F (GL476-GL477, branch `gl476/travelers`): `AdventurerCamp` (Game/Travellers, on the labs' Travellers object, `ISavedValues`:
arrived, purse, disposition) finds the peak once the world is up (`PeakSearch`, Core/Generation: highest ground within 250 m of the
starting parcel on the generated surface, then `Summit` climbs to its top, kept inside a fixed area) and, from the dawn after night 5
(`AdventurerVisit`, Core/Economy), builds a campfire (`ModelCopy` of the campfire, `FlameCluster`, smoke, a point light that shines only
at night) and an `Adventurer` (`ITrader`, `KeeperFigure.BuildAdventurer`) who buys the whole price list and sells it at 2.5x, from a 10k
purse refilled only by the player's purchases. `TradeCart` (same object, `ISavedValues`) waits for the town's plan, places a distant
town 10-13 parcels out of the gate (`CartRoute.DistantTown`, within 60° of it; in a fixed area the direction most inside it; named by
`SettlementNames`) and roughs in a road (`CartRoute`: bends every 16 m moved sideways to the least climb, rounded once, heights kept
every metre). `CartTrack` writes it as a 2.4 m dirt track with `GroundLeveling` (following the ground) in 2 m pieces as their chunks
load, about 3 ms each in the Editor (25 ms where a tree is cut), and saves which pieces are done. `CartSchedule` (Core/Economy) times
trips from the seed and clock (first 09:00 day 2, then every 2-3 days: to the post, a 2.5 h stop outside the gate, back), so the cart
isn't saved; `TradeCartModel` is the hand cart, carter (`KeeperFigure.BuildCarter`) and two wooden crates of the dearer trade goods,
refilled each trip, their leftovers saved per trip (`StorageText`, Game/Building, the crate format `TradingPost` used). The distant
town is only a cobbled yard with a name sign and crate looks, built once its chunks load; beyond a fixed area's edge it isn't built
and the cart hides there. Tests: `TravellerTests`.

Tutorial and title page (GL479-GL480, branch `gl479/tutorial-menu`): the New Game page offers the Mining Corp tutorial or
skipping it (`GameSession.Tutorial`, cleared by `FinishTutorial`; saving is refused while it runs). The tutorial is a mode of
WorldLab, not its own scene: `MiningCorpTutorial` (Game/Tutorial, on WorldLab's Tutorial object) waits for the town's plan, and
`CompanyQuarry` digs a 5 m `OpenPit` out of the gate and beside the road (`CompanyWork`, Core/Economy: site, 10% wage, theft
rule), then the player is set down on its rim (`PlayerSpawn.SetDownOver`) with a `CrewLead` (`KeeperFigure`, miner) and an
`OreCart`. Steps on a card at the top: look, walk, jump, sprint, crouch, F1, talk (E, gives the Stone pick), hotbar, dig, inventory,
hand in (stone dug in the quarry is company goods, paid `CompanyWork.Wage` of its `PriceList` price); then the lead walks off to
the gate. Carrying company goods past the rim plus 5 m, or holding anything else, is theft: fade, inventory emptied (coins kept),
the player set down on the spawn and the quest box back on. The title page's background is `MenuDiorama` (Game/Menu, MainMenu
scene): a low-poly cut-open block of the parcel built in code (layers, ore nodes, tree, campfire copy, stump, a `KeeperFigure`
in the Mine pose), framed right of the menu column, which `MainMenu` now draws on the left over a fading shade. Tests:
`CompanyWorkTests`.

GL491-GL496 (branch `gl491/sounds-fx`): menu clicks live in `GameUi.Button`/`GameUi.LayoutButton` (every menu, journal, station, merchant, rest and chest IMGUI button now calls them) which play `UiClick` (a clip from `SoundSynth.Click`, one hidden `AudioSource`; labels containing Back/Cancel/Close play at 0.82 pitch). `SoundSynth.Lick`/`Pick` are built at runtime in `PlayerActionSounds` (no WAV assets) on `PlayerProspecting.Licked` and the new `BoulderBashing.RefinePassed`. `PlayerProspecting.crushedLickMultiplier` (1.5) scales lick xp for crushed ore (`PurityAs != null`). `TorchFlame.MakeSmoke` takes `sizeScale`/`darkness`; the adventurer campfire uses 3 and 1. `StarSky` runs one pooled `LineRenderer` shooting star (Sprites/Default) every 60-180 s when night > 0.85.

**GL500-GL502 (crafting panel).** `StationPanel.DrawMissing` now draws `have/need` on every input of the chosen recipe: red with a greyed icon (and the X) when short. `LoadClicked` swaps a non-stackable (`MaxStack == 1`) clicked in the inventory with the same item already in an input slot, the old one going back to its inventory slot. `ItemDefinition.fuelQuality` (0-1, default 1; charcoal 1, maple wood .9, wood/boards .85, stick .7, grass/leaves .5) feeds `CraftingStation.FuelFactor` (burning fuel, else the one waiting), which `YieldOf` multiplies into smelts (`Extracts != null`); the yield note shows "(fuel N%)" below 100%. Test: `Yield_FollowsTheFuelsQuality`.
GL497-GL499 (branch `gl497/missing-features`; the demo showed none of them): trout never jumped because each jump tried one random spot in the 4-40 m ring and only ~0.4% of WorldLab's map is river deeper than 0.6 m (1708 of 409600 cells; deepest 1.19 m): `RiverTrout` now tries up to 60 spots per jump and `TroutJumps.MinDepth` is 0.4 m. `HitDebris` ignored hits that broke nothing, sparked 20% of rock hits and the scene's serialized `sparkChance` overrode defaults: it now bursts on any strike that reaches ground, with bigger sprays, and the spark fields were renamed (`rockSparkChance` 0.7, `boulderSparkChance` 0.85) so old scene values drop. The early-bird bonus is intact but `ToolCooldown.DawnTo` was 07:30 and a new game starts at 07:00 (GL428): it now runs to 09:00.

**2026-11-09, GL503-GL506 (terrain).** `Hydrology` now sets a river reach's water level from the ground under its centre line (eroded land), so channels are cut across and full. `RiverDebris` (an `IStampField`, combined with `BoulderField` by `StampFields` in `WorldView`) lays logs across rivers and rocks in them. `TreeForests.IsFallen` + `TreeShape.BuildFallen` make 12% of deep-forest trees a stump with a leafless trunk lying in a cardinal direction (far trees skip them). `WaterMap.ShoreJitter` makes the sand-shore edge wander. GL507 (5x deeper world) was not done: needs timing and tuning checks. Tests: `FallenTreeTests`.

Batch 6, town and roads (GL509-GL511, branch `gl508/town-roads`; GL508, the organic layout, was not done): `TownCave` is now sealed by terrain: the room under the back or a side of the square keeps its two metres of cover, and up to four level tunnels (`Tunnels`, one per mine-free direction) run from it to the north, east, south and west faces of the parcel, ending underground at the face, so a buyer of the next parcel who digs down finds them (nothing opens to the surface). `CartRoute` now picks each bend by a least-cost search over nine sideways spots per station (`StretchCost`: length times 1 + 40 x steepness squared, sway 30 m), so the road goes round a slope instead of up it. The trade cart never showed because its first trip left at 09:00 on day 2, over an hour of play away on the 40-minute day, and then only its last stretch is inside a fixed area: `CartSchedule.FirstTrip` is now 09:00 on day 1 (checked in Play mode on WorldLab: the cart is placed on the road mid-trip). Tests: `TownCave_SealedUnderground_WithTunnelsToTheFourParcelFaces`, `CartRoute_GoesRoundASteepFaceThroughTheGap`.

Batch 7a, the tutorial's own map (GL513-GL516, branch `gl514/tutorial-map`): the opening tutorial moved out of WorldLab into
the `Tutorial` scene, which `GameScenes.LoadGame` opens while `GameSession.Tutorial` is set. Its world (`TutorialWorldConfig`,
200 m, Heightmap generator, no trees, boulders or water) is a level yard boxed in on three sides by a steep stone mountain
(`CompanyWork.Height`, written to a 16-bit RAW by *Clube → Tutorial → Build map*, `TutorialMapTools`, which also copies the
scene from WorldLab and strips the town, travellers and parcels). `CompanyWarehouse` builds the warehouse in code across the
yard's open end: dorm hall with the player's bunk (a `SleepingRoll` with `FitsGround` off), the crew lead's room with a crate of
valuables, locked stores (`LockedDoor`), supply crates outside, dirt under the floor so no grass grows through. The player
starts behind it facing the lead (`PlayerController.LookTowards`) and types a name (`GameSession.CharacterName`, saved by
`PlayerName`); at the talk step a `QuestArrow` bobs over the lead and `Minimap.Marker` marks him. With no parcels the whole map
is minable (GL516), and theft is only taking from a `CompanyCrate`, never what's held (GL515). Caught, the inventory is emptied
and WorldLab loads as a new game, with the wages carried in `GameSession.CarriedCoins`. Tests: `CompanyWorkTests`.

Title page from real world generation (GL512, branch `gl512/title-worldgen`): `MenuDiorama` no longer builds boxes; the MainMenu scene has a `WorldView` on `MenuWorldConfig` (16 m fixed area, 3 chunk layers, seed 512, sealed faces so it reads cut open, small trees, no water/ores of its own), so the grass, dirt, stone, trees, boulders and fallen log are the game's own. When it has loaded, `MenuOres` (Game/Menu) stamps ore balls through `World.SetMaterial` into the cut face (gold at the bottom, then silver, chalcopyrite, malachite, tin) plus a raised tin lump and a stump (`World.SetDensity`); the campfire and the mining `KeeperFigure` are placed on the measured ground. Camera framing and menu layout are unchanged.

Tutorial fixes (T1, GL519-GL530, branch `gl519/tutorial-fixes`): `PlayerController.InputLocked` (set while the name is typed) stops walking, jumping, sprinting and crouching, and `ClimbingDisabled` turns climbing off; `MiningCorpTutorial` sets it and `SkillSet.Suppressed` (no xp, `SkillsHud` off) in its `Start`. Crates open to a list of their items (`MiningCorpTutorial.OpenCrate`); only taking one is theft, and each now holds valuables. The hand-in skips every `ToolDefinition` (a worn pick is a runtime copy, so `Item == pick` missed it). `CompanyWarehouse` hangs a rules sign on the back wall, puts the player's name on a placard over their bunk (`NameBunk`), and clears dirt 0.8 m past the walls, again 4 s after arrival, so no grass or bushes grow in or against it.

Tutorial world and quota (T2, GL522/GL528/GL531-GL534, branch `gl531/tutorial-features`): `ToolShed` (a "Tool Shed" sign; `MiningCorpTutorial.TakePickFromShed` gives a company pick only when none is carried, so a worn-out pick is replaced) and `WorkerCrew` (six `KeeperFigure`s in `KeeperPose.Mine` at the north wall) are built in `Arrive`; `CompanyWarehouse.BuildFence` puts 6 m fences along both sides and across the back corners so the front can't be reached. Digging an ore (`Content` of category Ore in a collected stack) adds it to `foundOres`; talking to the lead then opens a panel (`DrawTalk`) with "Hand in", "Discuss the <Ore>" (always the "we only do Stone here" line) and "Goodbye". Quota: `CompanyWork.Quota(day)` = 1024 x day, cumulative Stone handed in (`stoneDelivered`, saved by `MiningCorpTutorial` as `ISavedValues`); `CheckQuota` throws the player out (`ThrowOut`, the caught path with its own story text) at 17:00 if short, and `DrawQuota` shows it top right. Not checked in Play mode (it didn't tick unfocused); tests: `CompanyWorkTests.QuotaGrowsByTheStepEachDay`.

Title page and menu fixes (GL517, GL518, GL523, GL524, branch `gl517/title-menu`): `MenuWorldConfig` is now a copy of WorldLab's config (ErodedLandscape, full trees, rocks, ores, rivers) over a 32 m fixed area, 18 chunk layers, seed 475, found by `Clube.Debug.Editor.MenuSpotSearch` (scores seeds on relief, a tilt up toward the back, river cells and none in a lake; *seeds 1-1500*). `MenuDiorama` frames from the measured ground range, adds a `WaterView` (scene) and the game's own fire (`FlameCluster` + `TorchFlame.MakeSmoke` with the campfire prefab's materials); `MenuOres` stamps ores, tin node and stump relative to the ground at each spot. The menu clicks were silent because the MainMenu scene had no `AudioListener` (the diorama camera now gets one). `HeldToolMeshes.HeadColourOf` returns copper for the copper pick (it fell through to iron, so its patina never drew). `Inventory.Add` fills the sub-one-item room of a part-full stack (1023.88 of 1024) before starting a new stack (`WholeItemNearAFullStack_FillsItBeforeStartingANewOne`).

GL535-GL538 (title screen): the diorama is now a 64 m block (2 x 2 parcels: `MenuWorldConfig.fixedAreaSize` 64), and `MenuOres` takes the block's half-width, so the ore layout spreads across the wider cut face and the fire and tin node sit a fixed few metres in from it; the camera looks down at `MenuDiorama.cameraPitch` 28 degrees (was 38). The floating water was `WaterView`'s far sheets (the map's rivers drawn beyond the loaded chunks): they now have a `drawFarWater` toggle, off in the MainMenu scene. The "dirt ball" was the hand-stamped stump cylinder at the lip of the cut face; it is gone. The Tool Shed sign text is white (`CompanyWarehouse.AddText` takes an optional colour).

GL539-GL544 (tutorial UI, branch `gl539/tutorial-ui`): `InventoryScreen.Blocked` (set by `MiningCorpTutorial` at the name step) keeps the inventory shut and E from opening it while typing; `FieldLogScreen.Blocked` keeps the quest, ore and tool journals off for the whole tutorial. The crew lead adds "Let me know if you find anything interesting too..." to his hand-in speech; an ore he has been asked about (or that was handed in with the rest, which now also gets the "we only do Stone" line) goes into `discussedOres` and its Discuss option never returns. The quota is the new plain-C# `DailyQuota` (Core/Economy): day N needs `CompanyWork.Quota(N)` = 1024 x N stone handed in that day, the count restarting each day (saved as `quotaStone` + `quotaDay`); the HUD reads "Stone handed in today". Tests: `DailyQuotaTests`.

Tutorial map (GL545-GL547, branch `gl545/tutorial-map`): the plank fences are gone; `TutorialTerrain.Apply` (run beside `CompanyWarehouse.ClearGround`, both on arrival and again 4 s later) stamped 4 m high, 1.5 m thick Loose stone walls along both sides and across the back corners (since GL545 follow-up: one short wall from the middle of each side out into the mountain) with `TownStructures.StampWallRun`, the straight-run twin of the town wall (both now share `StampColumn`). It also turns every solid sample on the mountain side (outside `CompanyWork.Fields`, up to 5 m over the yard) to Stone, so the foot reads as rock instead of a grass ramp. `CompanyWarehouse` writes the player's name on both faces of the headboard placard and hangs a second rules board on the dorm hall's south wall.

Tutorial workers (P4, GL548-GL549, branch `gl548/tutorial-workers`): `WorkerCrew` is now a MonoBehaviour. Each of the six workers owns a 6.7 m stretch of the north wall and, every ~2.2 s, takes three small `World.ApplyBrush` Remove bites (radius 0.55 m, head height) in front of it, steps 0.45 m along the stretch, and after a full sweep digs 0.5 m deeper (8 passes, then back to the face), so the whole wall is worked over time; no allocations per frame. A schedule on `DayNightCycle.ClockHour`: work 06:00 to `CompanyWork.QuotaHour`, then walk to a queue spot by the crew lead, stand 2.5 s (handing in), walk to a bed north of the warehouse and `LieAlong`; at 06:00 they stand and walk back. Checked in Play mode by pumping `Update` (density removed at the face; at 17:30 worker 1 was in bed, at 07:00 back at the wall); no test added.

GL535 follow-up (title skirt): the title's dirt "curtains" were every other 0.25 m column of the cut face keeping a dirt layer, and the ore patches only seemed fixed because the terrain shader's textures and glints are in world space while the block turned. `MenuDiorama` now swings the camera round the block instead of turning it (`sway` is the camera's swing), and `MenuOres.StripSoilFromSkirt` turns the soil on the outer 1.5 m of the block's sides to stone, so the skirt shows stone and the ore nodes stamped on it. (A scan that starts half a sample off the grid rounds to every other sample: start on sample centres.)

Tutorial rules (Q1, GL550, GL553-GL555, GL565, GL567, branch `gl550/tutorial-rules`): `PlayerCameraToggle` ignores P while a `MiningCorpTutorial` is in the scene. `CompanyWork.Quota` doubles per day (1024 << day-1) and `IsWorkHour` (09:00-17:00) drives `PlayerToolUser.Lockout`, a hint-returning delegate the tutorial sets (from the first hand-in on) that stops digging by hand or tool. The early bird only fired on day 1 because its window (02:30-09:00) is before the first work hour and the player sleeps past it; the tutorial now moves it with `SetEarlyBirdWindow` to the first two work hours, so it holds daily. Tutorial crates are the Wooden Chest prefab (`cratePrefab` on the tutorial, `StorageChest`); `CompanyCrate` watches the inventory and calls `Steal()` when stock leaves. E on the crew lead always opens Hand in / Discuss ore (greys out for good, saved as `oreDiscussed`) / Exit. Caught: `ThrowOut` locks input, closes the chest panel, `CrewLead.Appear`s him front-right, and `Thrown()` turns the view with `PlayerController.TurnTowards` (~0.5 s).

Tutorial workers, take two (Q2, GL551, GL552, GL556, GL566, branch `gl551/tutorial-workers`): `WorkerCrew` now builds six workers (one per dorm bunk, `CompanyWarehouse.WorkerBedSpot`) that sleep in the warehouse hall and walk out the back door (`DoorInside`/`DoorOutside`) at 08:00; they dig only 09:00-17:00. Each walks to a spot in front of the face, turns to the wall and swings on the `KeeperFigure.MineBeat`; each landing is a `SphereStrike.FindTargets` ball with `ToolStrike.Hit` (the pick, per-worker `StrikeDamage`), with sound through `PlayerToolSounds.StrikeSound` on a 3D `AudioSource` and crumbles through `HitDebris.StrikeAt`. After 9 swings, or 3 empty ones, it walks 1.1 m on; after a sweep it digs 0.5 m deeper, with no cap (GL556: the old code wrapped back to the face after 8 passes, so the wall stopped advancing). Simulated 2 days at 20x with the clock jumped: about 2100 strikes a day, 23 passes deep, still advancing. `KeeperFigure.BuildWorker` (patched dull cloth, worn cap) and `BuildCrewLead` (long red coat, gold buttons, cravat, tall hat) are the GL566 looks. No test added.

Item text (Q3, GL558, GL562-GL564, GL574, GL575, branch `gl558/item-text`): `StationPanel.DrawMissing` writes `have/need` on a dark strip at the bottom of each input slot, `have` the decimal `Inventory.AmountOf` ("90.74/1"). `BoulderBashing`'s refining text reads qualities through `ProspectReading.QualityAt` (grade word below Prospecting 10). `PlaceableDefinition.DropsMix` (set on the chest, workbench, anvil, campfire, sleeping roll and merchant table; furnaces keep their clay mix) stops `CraftingStation` passing the inputs' contents to a crafted placeable. `ItemSlotGui.ReadsAsGrades` also holds for quality above 100% (maple-made, GL151), so maple reads PURE like birch. `ProspectReading.ShareWord` (Dominant/Abundant/Moderate/Minor/Trace) names non-primary components in item text. `StationPanel.Open` clears a worktop's chosen recipe.

Skills notebook and tips (Q4, GL568, GL569, GL572, GL573, branch `gl568/tips-skills`): the quest tips in `PlayerQuests.Tips` now say to raise Crafting toward the campfire (sticks at a Stump, hand axes on a boulder) while "Build a campfire" is active, and to raise Crafting for tools while "first ore" is. `ProspectReading.UnlockAt` words what each Prospecting level lets you read of a mix ("the second item" at level 1) and `SkillsHud` adds it to the level-up notice. `SkillUnlocks` (Core/Skills) derives a skill's unlocks from recipe requirements, `OreKnowledge` groups and `ProspectReading`, and splits them at the player's level into reached levels plus the next level only (`SkillChapter`); `SkillsJournalScreen` draws one page per skill as the "Skills" tab of `FieldLogScreen` (added at runtime, so scenes are unchanged).

World materials and trout (Q5, GL557, GL559, GL571, branch `gl557/world-materials`): `TutorialTerrain.StoneFoot` now turns every sample with any density to stone (not only those at or above the iso level), from 5 m under the field's height and right to the fields' edge, so no grass lip is left for mining to hand out. Trout never jumped in WorldLab for two reasons found in a Play run: `RiverTrout.TryStart` demanded live water 0.2 m under the map's surface, but the simulated water is thinner than the river map's depth (125 of 651 deep river cells passed; none at 0.5 m), so it now looks 0.05 m down, and it tries 200 spots a jump instead of 60 (audio and materials were fine; `JumpNow` then made 5 jumps in 5 tries by a river). Crushed stone is material 30 (`Crushed stone` asset, copied from Sand: falls and piles the same, drops the crushed stone item, placeholder texture `crushed stone` appended last in `MaterialTextureTools`); `IsMaterialItem` is true for it, and `BoulderBashing` gives knocked-off stone a pure `Crushed stone` composition so its stack reads POOR–PURE.

Bridges and spade (Q6, GL561, GL570, branch `gl570/bridges-spade`): `RouteBridges.Find` (Core/Generation/Settlements) walks the trade road every half metre against the generator's `WaterMap` and returns a `Span` for each stretch over river water (merged within 4 m, widened 1.2 m to each bank, deck at least 0.45 m over the water and at the bank heights otherwise). `TradeCart.BuildBridges` builds each as `RoadBridge` once the chunks along it have loaded: two cobblestone footings sunk into the banks, maple boards on two stringers, and the parcel fence's posts and rails (`ParcelBorders.PostWood/RailWood`) on both edges, one low-poly mesh with box colliders for the deck, rails and footings. The held shovel is now a spade (D-handle, socket, rounded blade narrowing to a point). `RouteBridgesTests` covers a crossing and dry land.

Frame rate while roaming (GL560, branch `gl560/fps`): measured in the built demo with `clube.exe -roambench -screen-width 1920 -screen-height 1080` (`RoamBenchmark` in Debug/Benchmarks, writes `roambench.txt`; `-roamseconds N` shortens the walk). It carries the player at 9 m/s over a figure across the whole fixed area and reports each 15 s stretch, plus 15 s standing at the spawn before and after: frame, main/render/GPU thread times, draw calls, renderers by parent, loaded and kept chunks, the systems' own timings, water awake, and in a development build the script and render markers. The slowdown was the live water: each edited chunk that streamed back in near water woke every one of its 4,913 samples (`LiveWater.WakeChunk`), and each flow step copied and sorted all awake samples before its 2.5 ms budget started counting, so 80,000 awake samples cost about 20 ms a frame and drained at 1,000 a second (the spawn ran at 7 ms before the walk and 29 ms after). `WakeChunk` now wakes only wet samples and open ones under a river's level, and `Step` takes the nearest samples by two counting passes (`TakeNearest`) instead of sorting the backlog. Also: the coarse terrain switches off chunks its clipping would leave nothing of (inside the finer level's square, or wholly over columns the world's chunks cover; `TerrainLodView.HiddenCount`, about 300-450 renderers, using `ChunkCoverageMask.Covers`), and `WorldStreamer.UpdateColliders` checks the chunks round the focus only when a mesh changed, a bake finished or the focus moved 1 m (it cost 0.5 ms a frame standing still). RTX 3080 / i9-10900K, render distance 12: spawn after the walk 28.8 → 7.4 ms, the last stretch of the walk 33.0 → 12.1 ms, standing streamer 0.7 → 0.0 ms, draw calls standing 3,690 → 3,490. Still costly while walking (10-12 ms main thread): scripts' LateUpdate 4.5-5.8 ms (distant trees 1.7, streamer 0.9), Update 2.2, rendering 2.7-3.7; one 1-1.4 s hitch each run around 650 m in.

Tutorial fixes (GL576-GL580, branch `gl576/tutorial-fixes`): `MiningCorpTutorial` offers "Discuss ore" only once ore has been dug (`oreMined`, saved) or is held (greyed out after use, as GL554). It sets the clock to 09:00 on arrival and `WorkerCrew.Build` puts each worker asleep in their bed (phase `Sleep`), so they wake and walk to work by the normal schedule. The grass up the mountain came back because GL547/GL557 only rewrote the loaded chunks near the dig, once: the generator still made grass and dirt, and the steep-slope rule (35°, from 24 m) leaves flat tops, gentle benches and coarse levels (whose gradients are taken at a coarser step) with the grass layers, and chunks that streamed in later or came back were never touched. New `TerrainLayers.BaseAboveHeight` / `LayerTable.BaseAboveHeight` makes every column above that surface height the base material, in the one fill kernel that all levels and re-streams share (`LayerTable.IsSteep`, `HasBareRock`); the tutorial config sets it to 20.1 m (fields stay grass), with a lab-panel slider; `TutorialTerrain.StoneFoot` stays as a harmless extra. Quests: `QuestLog.Frozen` stops `Complete` (so no progress, notice or event), `PlayerQuests` skips its checks and notice while frozen, and the tutorial freezes the log (the HUD and journals were already off); test in `QuestLogTests`. Workers stuck in rock: workers walked in straight lines between the yard and their post, which for a deep post cuts through the neighbours' undug rock, so they now go by the lane in front of the face (`Phase.Approach`, `LaneOf`) in both directions, and `NextSpot` will not step to a post whose body (three small balls) is inside terrain (`BodyBlocked`), digging on instead.

GL581-GL582 (same branch): `BoulderBashing.HandAxeDisabled` (set by `MiningCorpTutorial`, cleared in its `OnDestroy`) stops a stone being knocked into a hand axe in the tutorial; the tutorial map has no stations, so that boulder is its only hand recipe. Workers now ground themselves each frame (`WorkerCrew.Ground`): a cast down from 0.6 m above the feet against `ChunkCollider` meshes only, stepping up at 3 m/s and falling with gravity when the ground is dug away, and keeping height where no collider is loaded. A NavMesh was not used: the voxel ground changes with every dig and colliders exist only near the player, so surfaces would be rebuilt constantly and absent far away.

Arrival cutscene and pause input (GL583, GL584, branch `gl583/cart-dropoff`): `GameSession.FinishTutorial` sets `ArrivesByCart`; `ArrivalCutscene.Bootstrap` (after the game scene loads) starts the cutscene, which waits for `PlayerSpawn` to place the player, then switches `PlayerController` off (`InputLocked`) and moves the eye by hand: ride in an `ArrivalCart` (`TradeCartModel` without crates, new `PlaceAt` with a tip angle, driven in a straight line over the terrain), tip, fall, thud (`SoundSynth.Thud`) and jolt, lie rolled, rise, control back. `ArrivalTimeline` holds timings and easing. `GameSessionHost.Save` refuses while `ArrivalCutscene.IsPlaying`; loaded games never play it. `PausedInput` disables all enabled input actions of the player's asset while `PauseMenu` is open (Esc is read from the keyboard so still closes it); `PauseMenu.IsPaused` gates the lab's F2/F3 raw-key handlers.

GL585 (branch `gl585/adventurer`): the adventurer now arrives at sunset on day 5 (`AdventurerVisit.ArrivalDay`; `AdventurerCamp` compares with `DayNightCycle.SunsetHour`), not the dawn after. `UpgradeSale` (Core/Economy) is the pay-once logic (price, owned, coins to the seller); `PlayerUpgrades` (Game/Player, added by `PlayerController.Awake`, `ISavedValues` key `upgrades.climbing`) owns the climbing sale, sets `PlayerController.ClimbSpeedMultiplier` (serialized `climbingMultiplier`, 2) and offers it as an `ITraderDeal` that `Adventurer.Interact` hands to `MerchantPanel`; `ITraderDeal.RefusalText` gives the panel a message when the buy is refused.

GL586 (branch `gl586/worker-stall`): workers stopped digging and making noise after the player dug under one. Cause found in code, not reproduced in play: `WorkerCrew.NextSpot` (GL580) refuses a next post whose body would be in rock and "digs on here" instead, resetting its counters; but the swings reach only the face, 0.9 m or more from that rock, so the worker strikes air forever (`Strike` plays sound only on a hit) and never moves on. A dug-under drop (GL582) shifts the worker off the heights the posts and strikes assume, which makes this likelier, and neighbours stuck the same way stall the crew. Fix: `Worker.Stall` (`Core/Queries/StallTimer`) counts digging time with no strike landing on rock; at 8 s `WorkerCrew.Replan` looks up to 12 posts ahead for a clear one (else takes the next) and sends the worker there by the lane (`StartApproach`), which also walks a dropped worker back out of a pit. Test: `StallTimerTests`.
GL586 (more) and GL587 (same branch): the user reported all workers stopping, and that a night's sleep did not reset it, so it is shared, persistent state. Not found by reading or in the Editor log; the best fit is an exception in `WorkerCrew.Update`'s loop (it skips every later worker each frame, for good). Each worker is now ticked in a try/catch that logs and calls `ResetWork`, and `ResetWork` also rebuilds each worker's work state every morning (keeps `Pass`, the dug depth). GL587: `MiningJournal.Frozen` (data layer, as `QuestLog.Frozen`); the tutorial sets it, so `PlayerJournal` records nothing and `JournalScreen` never gets `Discovered`; the crafting journal reads the same data. Test in `MiningJournalTests`.

GL588 (branch `gl588/arrival-trigger`): `ArrivalCutscene` hooks `SceneManager.sceneLoaded` at `SubsystemRegistration` instead of using `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`, which only runs for the first scene of a play session, so the cart cutscene never started after the tutorial loaded the game scene. Any per-scene start-up must hook scene loads the same way.

Bounded world, batch A, GL589-GL592 (branch `gl589/bounded-map`): everything is in `Core/Generation/Map/` plus `Fields/BoundedMapHeight` and `Generators/BoundedMapGenerator`. `MapDefinition` (a field of `TerrainSettings`) resolves by seed to a `MapLayout`; the map is centred on the origin. `MapBoundaryType` entries are blended into the land by `MapShape.Apply`, which also adds the great peak and a tilt towards the sea (without the tilt the plains ponded into lakes covering a third of the map). `Hydrology.Build` gained a `WaterExtent` (area past the 2048 m limit, water cells of 2 m, a sea that is one outlet at sea level; `WaterMap.HasSea`/`IsSea`); its distances stay in metres. Biomes: `BiomeDefinition` rows → `BiomeField` (inline `FixedList`, read by `ChunkFillKernel.StampBiomes`) and `BiomeMap` (main thread; the trees read a 16 m grid of it through `ITreeClimate`, set on `TreeGeneration.Climate` by `TerrainGenerators.Create`). `ResourceSurvey` samples the ground in point-height jobs; `OreField.AppendTries`/`MarkPlaced` is node placing in two halves so the ground under 5 million tries comes from jobs. Measured in the Editor at 5 km: generator and water 6-8 s (the 7.6 million water cells; cached per session), survey 4 s (ores 2.3 s). *Clube → Map → Write bounded map preview* draws the survey to `Temp/BoundedMapPreview.png`. Tests: `BoundedMapTests`.

GL619 (branch `gl619/arrival-cutscene-starts`): the game scene's Player is inactive at scene load (`PlayerCameraToggle.Start` switches it on), so `ArrivalCutscene` finds it with `FindObjectsInactive.Include`; `GameSession.StartNew` sets `ArrivesByCart` for a new game without the tutorial. Anything that looks up the player at scene load must include inactive objects.

Bounded world, batch B, GL593-GL597 (branch `gl593/settlements`): the world plan. `WorldPlanner.Build(survey, seed)` → `WorldPlan` (`PlannedSettlement`, `PlannedShop`, `PlannedRoad` with `RoadBridge`s, `WaterRoute`), from the tables in `WorldPlanRules`: `SettlementType` (the seven types and counts), `EconomySpecialty` (20 specialties in five `SupplyChains`; `TradeGood` is an item id or a name for a good not in the game; `ToShop(catalog)` gives the existing `ShopSpecialty`, `VarietyFor` the merchant's stock), `RoadClassDefinition`, `NamedVessel`. Steps: `SettlementPlacer`, `SpecialtyAssigner`, `RoadPlanner`, `WaterRoutePlanner` over `PlanContext`; resources by id in `SurveyResource`, sums in `ResourceReach`. Names: `SettlementNames.Generate(seed, cell, typeName, taken)`. The *Edmund Fitzgerald* is a `NamedVessel` for iron ore: no iron ore exists yet, so it sits in `WorldPlan.ReservedVessels` with a note, and takes the sea route to the best iron-mining port once an ore named iron (or hematite, magnetite, taconite) is on the map. Found on the way: the sea drained on any map with an ocean beside a void cliff or a wet start area (`Hydrology.ContainLakes` spilled it into the low dry ground; the survey's `Sea` was 0 everywhere on the lab config), fixed in `Hydrology`, test `Sea_StaysBesideAVoidCliff`. 5 km: plan 0.3 s after the survey. The preview menu draws the plan too. Tests: `WorldPlanTests`.

Bounded world, batch C, GL598-GL601 (branch `gl598/mapgen-scene`): the map in a scene. `MapGen.unity` is the streamed lab's scene on `MapGenWorldConfig` (BoundedMap, 104 layers of chunks: the world stacks every column from y = 0, so the 375 m peak needs 416 m; about 333,000 chunks at render distance 32, loaded in about 30 s, nearest first). `MapSession` (`Game/Map/`, on the World object, `MapSession.Current`) builds the `ResourceSurvey` and `WorldPlan` when the world's generator is a `BoundedMapGenerator` and again after each regenerate, then sets the player down at `MapStart.Pick`. `MapMarkers` posts the sites and nearby roads; `MapControls` is the lab's Map section. `WorldMapScreen` (M) draws `MapPicture.Render` with GL lines and `MapIcons` over it; `BoundedMapPreviewMenu` now draws with `MapPicture` too. Roads follow GL620: footpath narrow dirt, dirt cart path wider dirt, paved cart path wider cobble grey (`MapPicture.RoadColour`, `RoadWidth`). GL600: nothing in the LOD needed widening (coarse terrain 8 km, far terrain 33 km); the sea stopped at the water map's edge, so `WaterView.BuildOuterSea` adds one mesh of growing cells out to 40 km. Editor Play mode, 2272 x 1278, 20 m over the peak looking east and south-east: 8.3-8.4 ms a frame once loaded; not yet measured in a built game. Left: the far water is still two triangles per 2 m water cell (2.4 million triangles on this map; the sea could be a few large quads), the void is ground 600 m down rather than nothing, ground below y = 0 (deep sea floor, the void) and above 416 m (the ridge, to 867 m) is outside the voxel world, the player can land on a treetop at the start, the survey stalls the first frames for 4 s. Tests: four more in `WorldPlanTests`.

**GL621-GL625 (iron, coal and steel; batch K).** Content only, no new systems: every asset is a copy of tin's or bronze's with its numbers changed, listed after the one it copies wherever that one is listed (station prefabs, `PlayerJournal`, `BoulderBashing.crushes` and `StumpWorktops` in the four scenes, shops, `ItemCatalog`). Materials Magnetite 31, Hematite 32, Coal 33 are appended to `MaterialRegistry` and to `MaterialTextureTools.Placeholders` (write new ones with `Regenerate(key)`, then rebuild the array). Their `OreSpec`s have priority -1 and are appended, so the earlier ores' nodes (salted by spec index) are where they were. Chain: ore or crushed ore → Iron bun (furnace, Smelting 4) → Iron ingot (anvil, flat rock); 4 Iron bun + 1 Coal → 4 Steel bun (furnace, Smelting 6) → Steel ingot; tools on the anvil; Build anvil = 100 Iron ingots on a worktop (the iron ingot's `maxStack` is 100 because a station has two input slots). `ToolDefinition.tier` now runs to 6. The world plan needed no change: `NamedVessel.OreWords` already named the two ores, and the specialties' goods are id tables in `EconomySpecialty`. Name-keyed tints (`HeldToolMeshes.HeadColourOf`, `DisplayItemMeshes.ColourOf`) check "charcoal" before "coal". Left: no heat requirement for iron (any furnace smelts it), the anvil still sells for 60 coins against 100 ingots at 48, no iron ore group for the town's site search, nothing in the Tutorial's ground. Tests: `IronSteelTests` (10), on the assets themselves.

GL626 (branch `gl626/sea-lod`): the far water no longer has two triangles per 2 m cell. `WaterSheetMesh` (Core, `Meshing/Water`) took the sheet streams and the cell-per-quad mesher out of `WaterView`; `CoarseWaterMesher` cuts the water map into 256-cell regions, each a quadtree whose squares are one sheet where every cell is flat (wet, not river, level with all wet cells touching it) and single cells elsewhere, sheets fanned through the corners on their rim so no corner sits part-way along an edge. `WaterView` draws those once, past `farDetailMetres` (256 m), and keeps cell-per-quad tiles only inside that square, built a tile ahead of the viewer; the shader's `_Detail` vector clips one layer inside the square and the other outside and fades the ripples to the edge (coarse sheets and the outer sea are level). Far meshes are uploaded and their readable copies dropped. MapGen: 2,425,936 → 454,531 far triangles (about 4,900 → 121 meshes), 7.28 → 1.42 million vertices (466 MB + readable copies → 91 MB), meshing 1.46 s → 0.5 s. Checked by eye from above the start (river, shore, the square's edge in the sea 256 m out) and from 480 m up over the middle: no seams or holes. Left: rivers stay a quad per cell (303,000 of the triangles); the outer sea's inner edge is not stitched to the regions; GL600's built-game number is still not measured. Tests: `CoarseWaterMesherTests` (6).

Bounded world, batch D, GL602-GL607 (branch `gl602/animals`): animals. Core/Fauna: `AnimalSpeciesTable` (nine `AnimalSpecies` rows: rabbit, goat, sheep, yak, alpaca for textiles; cow, deer, elk, bison for the tannery), `HerdSites` (a herd per 96 m cell by the seed and the biome at its home, chance 0.3-0.75 by the biome's grazing or game; `StandInBiome` for worlds with no `BiomeMap`), `AnimalBrain` (graze/wander by day, gather/rest out of its active hours, flee within its skittishness or when hit, dead at no health; `Blocked()` swerves a fleeing one). Game/Fauna: `AnimalLooks` recipes, `AnimalModels` + `AnimalRig` (low-poly parts; `LowPolyMeshBuilder.Blob` is new), `Animal` (the body: ground following through `AnimalHerds.TryGround`, a probe ahead every 0.15 s; `IToolTarget`), `AnimalHerds` (spawner on World, `ISavedValues` key `fauna.dead`). `PlayerToolUser.Strike` hits any `IToolTarget` under the view (raises `TargetStruck`, which swings the held tool); a carcass is a `StorageChest` filled by `Hold(name, inventory)`. Items: five furs and wools and four hides (`ItemIconTools.CreateAnimalGoods` makes assets, icons and prices). Seen in MapGen Play mode, driven by scripts: cows, deer and sheep spawned on the ground near the start, grazed and wandered at 07:00, fled the player, all lay down at 23:00; a cow died to ten hand hits, its carcass opened in the chest screen with one cow hide, and went 15 s after the hide was taken. Not checked: a real mouse click and E press, WorldLab's stand-in in Play mode, saving and loading the dead. Animals have no sounds, and the player walks into their box colliders. Tests: `AnimalTests` (15).

**GL627-GL630 (ore looks, skill pace, skill cheats; batch L).** How an ore is drawn in the journal is data now: `VoxelMaterial.SketchStyle` (`OreSketchStyle`), read by `OreSketch.Shade`: Speckled (the old drawing, its random sequence untouched, so the other ores look as they did), Crystals (magnetite: inked diamonds with a bright facet) and Solid (coal: filled black). Magnetite's placeholder texture is the one placeholder not made of round specks: `MaterialTextureTools.WritePlaceholder` paints it without flecks and adds `AddCrystals`; `RegenerateMagnetite` rewrites it, the crushed icon and the array. The skill curve is `SkillSet.FirstLevelCost` (300) and `LevelCostGrowth`; change the first and every level scales. A save keeps the cost its experience was earned on under `skills.firstLevelCost` in `GameSave.Values` (`PlayerSkills.RestoreValues`, which runs after `PlayerSaveState.Restore` has put the raw experience back, and rescales with `SkillSet.FromCurve`; no key means the old cost of 30), so retuning the first-level cost again needs no migration; changing the growth would. `SkillSet.SetLevel` sets an exact level (cheats), `RaiseTo` still never lowers, `SkillSet.Types` is every skill but None. Cheats: `SkillCheatControls` (buttons, sliders) beside `CheatCommands.SetSkill`/`SetAllSkills`. Left: lessons cost ten times the coins (`ShopSpecialty.coinsPerExperience` unchanged at 2); `SkillsHud.Shown` is still its own list; lowering a level does not take back ores `OreKnowledge` has revealed. Tests: `OreLooksSkillPaceTests` (8).

**GL608, batch E: low-poly people** (branch `gl608/character-models`). The blocky `KeeperFigure` (boxes on six joints) is gone; every person is now a `FigureRig` (`Game/Figures/`) dressed from data in Core (`Core/Meshing/Models/Figures/`): `FigureLook` (height, build, head size, seven colours, `FigureHat`, `FigureOutfit` flags: apron, robe, coat, patched, backpack, cloak, beard, belt, scarf, bare arms; `FigureProp`), `FigureLooks.Of(FigurePreset)` (Blacksmith, MineKeeper, Merchant, Woodsman, Prospector, Worker, Foreman, Adventurer, Thief, Carter, Player; `Of(ShopKind)` maps the shops) and `Varied(preset, seed)` (height, build, skin, fade and, for workers, cloth, patch, hair and beard, all in a few steps so alike people share meshes through `FigureMeshes.For`, keyed by `FigureLook.MeshKey`). `FigureMeshes` builds seven parts (pelvis, torso, head, upper arm, forearm with mitten hand, thigh, shin with wedge foot) with the new `LowPolyMeshBuilder.Loft` (rings along Y) and `Blob`; 820-1050 triangles a figure, 11 renderers. `FigurePoses.At(action, time, step, stride)` is pure and gives `FigurePose` (elbows and knees are new; the walk's stride and pace follow `FigureRig.Speed`); `FigureAction` is the old `KeeperPose`. The mining swing now really lands at `MineImpact` (0.7): before, the beat fell at the top of the swing. `FigureRig.Hand` is the palm socket (`FigureSkeleton.HandSocket`); `Hold(mesh)` swaps the prop; a staff or dagger shows in every pose but lying (they were never shown before, as only chore poses showed props). The player: `PlayerShadowBody` (added by `PlayerController`, like `PlayerUpgrades`) is a shadows-only rig in the Player look that walks, stands and squats with the capsule; `HeldToolMeshes.Arm` is the first-person sleeve, cuff, wrist and faceted fist in the Player look's colours. `FigureContactSheet` (*Clube → Models → Figure contact sheet*) renders every preset to `Temp/figure-contact-sheet.png`. Left: no swing on the player's shadow, no third-person view, the apron hangs from the torso (it tilts when the keeper bends), no figure LOD or instancing for a 32-NPC city. Tests: `FigureTests` (10).

**Batch M, the map screen and the new-game preview (GL635, GL636; 2026-11-12, branch `gl635/map-polish`).** The world map (`WorldMapScreen`) draws routes from `MapLineStyle` rows (`MapPicture.RoadStyle`, `RouteStyle`): dashes and a dark casing are style fields, so the three road classes read apart without colour (cased pale, solid brown, dotted). `MapLegend.For(plan)` builds the legend rows (lines, bridge, water swatches, the settlement types on the map, the player) and `Place` puts it right of the map or inside it; the screen draws each symbol with its own `Stroke`, `BridgeMark`, `Shape` helpers, clipped to a `clip` rectangle (the map frame, or the screen for the legend). All settlement names show. The new-game page shows `MapPreview`: `BoundedMapGenerator.CreateDry` (the land, no hydrology) sampled by `MapContours.SampleHeights` and drawn by `MapContours.Render` (a pure function of a height grid, line width kept to a pixel by dividing by the slope), 33 ms a seed at 256 x 256, redrawn 0.2 s after the seed settles. `MainMenu.mapConfig` points at `MapGenWorldConfig`. Left: the preview has no rivers or lakes (by design) and shows the bounded map although Start still loads WorldLab. Tests: three in `BoundedMapTests`.

**Batch N, natural terrain and organic map edges (GL637-GL641; 2026-11-12, branch `gl637/natural-terrain`, from a worktree with no Editor of its own).** The bounded map's shape was rebuilt on the eroded landscape: `MapShape.Apply` no longer adds a cone and straight side bands. `MapDefinition.Resolve` picks an outline (`MapShapeType` rows: Continent, Peninsulas, Island and satellites, Island chain, Archipelago), as many sea sides as it wants and the other sides' kinds by weight; `MapLandBuilder` lays it out as points (mainland fronts, sea reaches, lands, islets, bays in `MapShape.Lands`; ranges as chains of bumps with one summit, spurs and forks in `MapShape.Ranges`, the bumps' heights fitted so their sum is the planned crest). In `Apply` the coast is the zero line of a land field (soft maximum and minimum of the round lands, read at a bent position, plus a fractal wander and 2.5 m of land per metre of range, so a range at sea makes its own islands); the ground is a beach up to the plains, a rise inland and a shelf down to the sea floor; hills are 65% size on the plains and 115% by a range; each side that isn't sea blends its change in along `EdgeInside` (the wandering playable edge) by `SideWeights` (the sides' wandering borders). `GradientNoise` (hashed, no table) replaced `ValueNoise` here and in the climate, whose blobs showed the lattice. What went wrong on the way: a chain's islands merged until its crest was held to the lands (`Standing`) and the islands spaced by their radii; a continent's sea hugged the edge line until each sea side got its own reach into the mainland (as deep as the facing mainland's front, or a lesser bite facing another sea); bays on a mainland circle landed inland as round lakes, so they now open off the sea's reach as two overlapping bites; summits were cut flat at the ceiling until the summit's height was capped by the room the voxel world leaves (294 m over the plains on MapGen) and hills fade on high crests; the erosion lifted crests past the ceiling, so `MapShape.Keep` runs again after it; dunes ponded into dozens of desert lakes until `WaterExtent.IsDry`. GL640: `LimitAt`, `MapLimit` (Core), `MapBoundaryLimit` (Game, on the player, added by `MapSession`); the stop line's own normal (`PastStopAt`) is the outward direction, so a held walker slides along the line. GL641: `MapHorizon` and a tint in `FarTerrain.shader`. MapGen's surface level went from 20 to 36 (sea at y 31, floor 26 m under it) and `steepMinHeight` with it. Measured in batch mode at 5 km: generator and water 5.3-7.0 s (as before), survey 5.0-7.9 s (was 4; more land holds more ore), 51 settlements on each of three seeds. Left: nothing here was seen in Play mode (the limit's feel, the horizon tint, the far range); the planner still sites only on the largest land, so an archipelago's other islands are empty; parcels are not tied to the playable edge; the void is still ground 600 m down; the lab's `BoundedMapPreviewMenu` now reads `MapGenWorldConfig`. Tools: *Clube → Map → Write seed sheet* (`MapSeedSheet`, also `-executeMethod … WriteFromArguments -mapSeeds 1,2,3 [-mapFull]`). Tests: `MapOutlineTests` (12), `BoundedMapTests` updated.
