# clube

## Project
A fresh Unity project (working name: "clube"), rebuilding a destructible
voxel-terrain concept (Marching Cubes) from scratch. This is a clean restart,
not a port of an earlier prototype — earlier code should inform decisions but
is not being copied in. Expect early architecture and details to change as
we go; don't treat early choices as locked in.

Two standing goals, equal in importance to shipping features:
1. Learn and practice good C# code organization (clear separation of
   concerns, one responsibility per class, no god-objects).
2. Learn good shared-code/GitHub practices (meaningful commits, reviewable
   diffs, treat this repo as if a collaborator will read it).

## Source of truth: `Docs/clube-objectives.md`
The scoped plan lives in [Docs/clube-objectives.md](Docs/clube-objectives.md).
It defines the architecture rules (A1–A14), ground rules (G1–G5), and every
objective by ID. Reference objective IDs in branches, commits, and PRs.
If this file and the objectives doc disagree, the objectives doc wins —
fix this file.

How the code fits together — the layers, the generation and mesh jobs, the
streaming frame, threading rules, where upcoming systems plug in, and a list
of self-contained optimization targets — is in
[Docs/architecture.md](Docs/architecture.md). Read it before changing
generation, meshing or streaming; keep it current in the same PR.

Milestone 1 = Chapters 0–3. Milestone 2 = the first gameplay loop (its own
section after Chapter 3, scheduled before the rest of Chapter 4). Chapters 4–5 are drafts; Chapters 6–8 are
design exploration. Do not build ahead of the current chapter; flag it if a
change starts pulling in a later chapter's direction.

## Current focus: Chapter 2 — Single chunk (`ChunkLab`), and Chapter 3
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
1/2/3 and switches `VoxelSelector` off while editing. *Clube → Build → Lab
demo* (`DemoBuild`) builds the playable exe to `Builds/Demo/`.
K34 is done: the exe opens on the `DemoMenu` welcome scene (Single voxel =
VoxelLab, Single chunk = ChunkLab, Exit; `DemoExitButton` and Esc in either
lab return to it), in a resizable 1280×720 window, and the lab panels fit
small windows.
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

## Chapter 3 — Collection of chunks (`WorldLab`)
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
Sixth pass (GL32–GL35, `Docs/20261009 clube-backlog.md`): volumes. One mined sample is 1 unit;
`ItemDefinition.UnitVolume`. Mined ore carries a `Composition` (shares by volume, on
`ItemStack.Contents`, blended when stacks merge) from its node's grade (`OreSpec.Grade`,
`Gangue`, `ExtraContents`; `OreField.ContentsAt`). (Recipes by volume, with held remainders, were
replaced by fixed batches in GL61.) The furnace burns fuel (`ItemDefinition.BurnSeconds`,
a `Fuel` slot). Clay (13) deposits are surface lenses (`TerrainLayers.Clay`,
`ChunkFillKernel.ClayDepthAt`); dropped clay dries into Brick (14) on a `GameClock`
(`GroundDrying`, `DroppedGroundDrying`), or is fired in the furnace.
Ninth pass (GL47-GL55, `Docs/20261013 clube-backlog.md`): the beginning path. `SkillSet` (Core/Skills:
Crafting, Woodcutting, Mining, Smelting; level n at 10 n (n + 1) xp) lives on `PlayerSkills`, which
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
Tenth pass (GL56-GL63, `Docs/20261019 clube-backlog.md`): recipes are fixed batches, nothing held
between runs (`CraftingStation.OutputFits` says why one waits, `Upcoming` is the greyed preview in
the output slot); `StationPanel` moves made tools to the inventory and, for stations that don't
`WorksUnattended` (worktops, anvils), returns leftover inputs on close. The merchant sells part of a
stack on a slider. Bronze pick, hatchet and shovel are hammered at the anvil (Crafting 4). The parcel
border (`ParcelBorders`) is a fence following the generated ground (`TerrainSurface.HeightAt`,
`WorldView.Generator`) and running down to y = 0, so it shows in holes. GL64 (a smaller inventory)
is waiting on its spec.
Eleventh pass (GL65-GL74, `Docs/20261019b clube-backlog.md`): the terrain shader breaks up tiling (`SampleBroken`);
the world labs have a dawn sky (`DawnSky.mat`, sun at 10°). `GrassScatter` (Core/Rendering) puts instanced
grass tufts (`Clube/Grass`) on upward grass triangles of the built meshes near the focus. The inventory
screen sends a clicked stack across (backpack ↔ hotbar, `Inventory.MoveInto`) and drags to a slot.
`OreKnowledge` has groups (copper ore Mining 3, silver 5, gold 7). Q opens the mining journal
(`JournalScreen`, `PlayerJournal`, `MiningJournal` in Core/Skills, `OreSketch`, `VoxelMaterial.Notes`); next
slot is X. `OreDebugView` counts ore 2 ms a frame.
"? ore" shows the stone texture with a question mark and hides its contents (`ItemDefinition.HidesContents`);
ores found before they're known get an uncoloured "Unknown ore" journal entry with their depth (GL75-GL77).
Twelfth pass (GL78-GL85, `Docs/20261019c clube-backlog.md`): F3 hides the lab HUD box. A hit also takes any floating
sliver of 3 samples or fewer next to it (`ToolStrike`). Stations start the recipe that uses the most of
what's loaded, and a picked recipe loads its ingredients (`CraftingStation.LoadFor`). Nothing is built on a
stump or on ground rising over 0.5 m across a cell, where the grid floats level. The campfire is a
`TerrainStructure`: stone and wood samples in the terrain around an ember bed, broken by mining any of them.
Thirteenth pass (GL86-GL94, `Docs/20261019d clube-backlog.md`, `Docs/20261020*.md` backlogs): the Bronze
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
Fourteenth pass (GL95, GL96, `Docs/20261020d clube-backlog.md`): a collecting test. The lab panel's Collecting section picks
the cursor style (`ToolCursorStyle`: dot, dot and voxel outline, faces, corner glows, impact sphere) and the collect
mode (`CollectingSettings`: voxel hits, or an area-of-impact sphere that lowers samples gradually or takes them whole,
`SphereStrike`). The dot and the gradual sphere are the defaults.
Fifteenth pass (GL97-GL105, `Docs/20261020e clube-backlog.md`): dropped stone (Loose stone) is cobbles; the furnaces are
`TerrainStructure`s like the campfire, primitive tapered shaft furnaces (parts carry their own densities) (*Clube → Models → Build furnaces*, `FurnaceStructures`): Stone
furnace of cobbles, Clay furnace hotter (`StationObject.heat` → `CraftingStation.Heat`). Structures turn with the
placed object. The volume inventory (GL99-GL103; replaced in GL120): a tool slot, a
volume slot (`VolumeMix`, `MixSlotGui`) that every mined sample goes into, and 3 item slots (`PlayerInventory`; `Hotbar`
cells map to slots). Recipes take a cut of the mix (`CraftingStation.LoadFromMix`, minimum share); what's built keeps
its contents and works at its material's share (`MaterialRegistry.Quality`, `StationObject.MadeOf`); Drop pours from
the mix by weighted random. Terrain materials meet in hard edges (`_HardMaterialEdges`). GL104 is part done. The
bare hand takes only the voxel under the dot (GL106).
Sixteenth pass (GL107-GL114, `Docs/20261020g clube-backlog.md`): the volume slot is three bins by `MaterialKind`
(Rock, Soil, Plant; `VoxelMaterial.Kind`, `PlayerInventory.Bins`), shown in the hotbar, on Tab and at the merchant
(`PriceList.ValueOf(VolumeMix)`); recipes take from the bin with the most of an ingredient, no minimum by default. In
the gradual sphere a tool takes its full amount from the kind it reaches wide on and 0.2 from the rest, halved off the
material under the dot (GL107). The bare hand picks up a loose surface stone whole with up to 2 neighbours
(`HandPickup`); every sphere and hand hit clears floating slivers (`ToolStrike.ClearSliversAround`). `GrassScatter`
looks only at chunks within its radius (it stalled in streamed worlds). Material stacks show their purity (GL115); there is no
Rock material, surface rocks are stone (GL116); a selected spare tool works and replaces a broken one (GL117). Stacks carry a quality
(`ItemStack.Quality`, GL118) from bin purity through recipes; a tool lasts its quality's share of its hits.
Seventeenth pass (GL119-GL125, `Docs/20261021 clube-backlog.md`): the bins are gone (with `MixSlotGui`, `LoadFromMix` and
`PriceList.ValueOf(VolumeMix)`). What a hit gathers goes to a stack of the material under the dot, mixed with what came
along (`Gathering` in Core/Items, `CollectingSettings.SortByTarget`): the stack's contents are the mix, its quality the aimed
material's share; a hit that can't take what it's aimed at (hands on rock) sends each material to its own stack. A mixed
stack drops as its mix (`ItemDropper`). The classic hotbar (9) and backpack (18) is the default again; the tool-slot layout
stays switchable (`PlayerInventory.UsesToolSlot`). Hands take soft ground only, plus loose stones whole. Hard-material piles
finish their build cell first (`TerrainPile.HoldsPile`). Grass is one voxel thick and a fuel; fuel burns for its quality's share.
Eighteenth pass (GL126-GL130, `Docs/20261021b clube-backlog.md`): off-kind tool hits take 0.1 (`ToolOffAmount`). A station's
`Quality` (set with its heat by `StationObject.MadeOf`) is a share of every recipe's yield; smelt recipes name the mineral they
extract (`Recipe.Extracts`, `TypicalGrade`) and yield by the ore's concentration against it (up to 2x), the metal coming out
pure; part items carry to the next run (`CraftingStation.YieldOf`, `Carried`; the station panel shows the yield). Anvil and
flat-rock recipes train `SkillType.Blacksmithing` and make nothing better than the smith (`SmithingQuality`: 50% at level 0 to
100% at 10). Items sell at their quality's share (`PriceList`).
Nineteenth pass (GL131-GL144, `Docs/20261021c clube-backlog.md`): crafted items of different quality don't stack
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
Twentieth pass (GL145-GL152, `Docs/20261022 clube-backlog.md`): materials need a tool tier (`VoxelMaterial.ToolTier`, and
`RequiredSkill`/`RequiredLevel`; dirt, grass, clay, leaves 0; wood, stone, ore 1; maple 3 and Woodcutting 3); tools have a
`ToolDefinition.Tier` (hand 0, hand axe 1, stone 2, bronze 3) and `Efficiency` (hand axe 0.3). `PlayerToolUser.Blocked` says why a
hit took nothing, shown by `ToolHint`. `BoulderBashing` (Place, right click, on boulder stone too big to be loose): a stone becomes
a Hand axe; from Mining 2, ore is crushed (`Crushing`: half the rock off) into crushed ore plus crushed stone as a part item; crushed
ore has its own smelt recipes and sells for 1.5x. The pointy stick is gone. `ChunkFillKernel.IsBuriedStone` puts 1-3 sample stones
in the dirt band (`TerrainLayers.BuriedStoneChance`); `HandPickup` takes a stone piece of 3 or fewer bedded only in air or tier-0
ground (`ToolStrike.IsLoosePiece`). Trees are birch (the old Wood, pale bark) or maple (`TreeGeneration.MapleWood/MapleLeaves/
MapleShare`, ids 21, 22); maple wood sells for 2x and whittles into 140% sticks (`Recipe.QualityFactor`; quality goes up to
`ItemStack.MaxQuality` 2). After pulling, the fill jobs' `LayerTable` changed: restart Unity if generation looks wrong (Burst gotcha).
Twenty-first pass (GL153-GL159, `Docs/20261027 clube-backlog.md`): inventory drops land on the nearest slot
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
GL163-GL165, P17 (`Docs/20261028c clube-backlog.md`): grass regrows nearest the focus first. `TreeForests` splits the land into
forests (`TreeGeneration.Chance`) and open fields (`FieldChance`; `ForestCover`, `ForestScale`) and scales each tree by a hashed
age (`YoungestSize`); GL166 gives birch and maple their own heights and trunks (`MapleHeight`, `MapleTrunkRadius`). The streamer keeps its wanted area per column (`World.LoadedColumns`, `WorldStreamer.IsWanted`): per chunk,
render distance 64 cost 240 ms every chunk-border crossing. Distant trees draw
only the tiles in view. To time a system, the far terrain, coarse terrain and distant tree views have `LastUpdateMilliseconds`
and `WorldView.Stats.Phases` splits streaming; `FrameTimingManager` reads real frames in Play mode (its GPU time reads ~0 here).
GL167-GL178, P18, P19 (`Docs/20261028e clube-backlog.md`): the world draws its chunks only within `WorldView.DetailDistance`
(a player setting, 24 chunks; P18): past it they stay loaded with their renderers switched off (`ChunkRendererPool.SetDrawRule`,
`WorldStreamer.IsColumnDrawn`; edited columns always draw, `World.HasEditsIn`) and the coarse terrain and distant trees draw
there, once `TerrainLodView` sets `CoarseTerrainReady`; the coverage mask counts only drawn columns (`WorldView.DrawVersion`).
The first coarse level is `TerrainLodView.FirstLevelFor` (level 1 in a fixed area, P19). Grass stands on a fixed world grid,
rescatters a rebuilt chunk in the same frame and keeps tufts within three radii across the ground (GL167, GL171). Hits hand
over part items at once (`Gathering.TakeAll`, GL169); hand and hand axe hits mix in the aimed voxel's other materials
(`CollectingSettings.AimedMixFactor`, GL172). The inventory screen has a trash slot and Sort (`Inventory.Sort`); Shift-click
loads half a stack into a station, Ctrl-click one (GL176). Holding Place on a boulder with a mixed stack picks it over
(`Refining`, `BoulderBashing`, GL177). Stations have a recovery (campfire 0.5, stone furnace 0.75, clay 1; GL178).

Settled (2026-10-08, GW1): chunks are 16 x 16 x 16 and build cells 1 m (both final);
voxels are 0.25 m, 4 per cell, pending playtests (M25). The labs keep these adjustable for
testing; the gameplay demo scenes use the fixed values. With 4 m chunks, P7 (LOD) and P16
(render distance in metres) are what let the world be seen far.

## Conventions
- Assemblies (A1): `Clube.Core`, `Clube.Debug`, `Clube.Game`. Debug and
  Game reference Core; **Core never references Debug or Game.** Debug also
  references Game (lab tools tune the player, M16); Game never references Debug.
- Namespaces match assembly names (`Clube.Core`, `Clube.Debug`, `Clube.Game`).
- Scenes (A9): `VoxelLab`, `ChunkLab`, `WorldLab` (fixed area), `ProceduralWorldLab` (streamed), `Game`. The `Game` scene
  contains no `Clube.Debug` components (A10).
- Docs go in `Docs/` (capital D, G5).
- Workflow (G2): one objective = one GitHub issue; one sub-section = one
  branch = one PR, listing the objective IDs it closes.
- Folder structure: code under `Assets/Scripts/{Core,Debug,Game}/`, one
  asmdef per folder, grouped by feature inside (e.g. `Core/Meshing/`).
  Core's folders are layers (A14, `Docs/architecture.md`): data (`Voxels`,
  `World`, `Materials`, `Config`, `Items`, `Crafting`, `Economy`) → algorithms (`Generation`,
  `Meshing`, `Editing`, `Queries`, `Volume`) → `Streaming` → `Rendering`
  (everything with a GameObject). Lower layers never use higher ones.
  Inspector/editor code goes in `Debug/Editor/` (`Clube.Debug.Editor`,
  editor-only); write `UnityEditor.Editor` in full there, since `Editor`
  alone names that namespace.
  Scenes in `Assets/Scenes/`, `WorldConfig` assets in `Assets/Config/`,
  edit-mode tests in `Assets/Tests/EditMode/` (`Clube.Core.Tests`, G3).
- Running tests: MCP can't drive the Test Runner and the project can't be
  opened twice, so clone the branch and run Unity in batch mode:
  `Unity.exe -batchmode -nographics -projectPath <clone> -runTests
  -testPlatform EditMode -testResults <clone>/results.xml`.
- Move, rename and delete assets through Unity (AssetDatabase / MCP), not
  the filesystem, so `.meta` GUIDs and scene references survive.
- Code in `Clube.Debug` must write `UnityEngine.Debug.Log`, not `Debug.Log`:
  inside the `Clube.*` namespaces, `Debug` resolves to the `Clube.Debug`
  namespace.
- MCP gotcha: inspecting a MeshRenderer/MeshFilter's full properties in edit
  mode reads `.material`/`.mesh`, which creates instance copies that get
  saved into the scene. Check scene diffs before committing.
- MCP gotcha: `ManageAsset` Move/Rename reports "failed unexpectedly" but
  usually succeeds; check the filesystem before retrying.
- Unity gotcha: an object's gizmos (`OnDrawGizmos`, `[DrawGizmo]`) are
  skipped once its renderer bounds leave the view. Scene-view lab overlays
  that must always show go through `SceneView.duringSceneGui` instead
  (see `VoxelLabSceneOverlay`); Game-view HUDs use `OnGUI` (`AxesHud`,
  `VoxelLabels`). Labels needed in both views share one IMGUI painter
  (`VoxelLabelPainter`) so the two never drift apart.
- Unity gotcha: in this project (Unity 6.3 URP on Direct3D 12) the gizmo
  pass dims gizmo parts it thinks are hidden using a vertically flipped
  depth buffer, so mirror images of opaque objects (ghost spheres, outlines,
  the surface) appear inside gizmo faces and lines. Not fixed by turning off
  the depth texture, SSAO, or switching the intermediate texture. Lab
  visuals are real meshes instead, built with `LabMeshObject` and the
  `LabMarker` (unlit, tinted), `LabVertexColor` and
  `LabVertexColorTransparent` materials: `ChunkDebugView`, `NormalLines`,
  `TetrahedraView`, `StepThroughVisuals`, `VoxelSelector`. Don't add new
  `OnDrawGizmos` visuals for anything that matters in Play mode.
- Unity gotcha: URP's Lit shader never writes depth when its surface type is
  Transparent (there's no Depth Write override in this version, and `_ZWrite`
  is reset on validation), so a transparent mesh shows its own back walls.
  Chunk renderers carry a second material, `ChunkDepthPrepass`
  (`Assets/Shaders/ChunkDepthPrepass.shader`), which writes the chunk's depth
  just before the transparent pass. Keep it when setting up new chunk views.
- Unity gotcha: `isActiveAndEnabled` stays false outside Play mode for a
  regular (non-`[ExecuteAlways]`) MonoBehaviour, because `OnEnable` never
  runs. Editor code should check `enabled && gameObject.activeInHierarchy`.
- Lab `WorldConfig` edits made in Play mode go to `ChunkView`'s private
  copy and reset on exit; only edits made outside Play mode are saved.
- MCP gotcha: during a domain reload (recompile, entering/leaving Play
  mode) the bridge drops and tools report "Unity not detected"; it comes
  back once `~/.unity/mcp/connections/bridge-*.json` is rewritten. Wait for
  it rather than switching to batch mode.
- MCP gotcha: Play mode doesn't tick while the editor window is unfocused.
  To test frame-driven behaviour, pause and advance with
  `EditorApplication.Step()` from `RunCommand`. `Camera_Capture` skips
  IMGUI; use `ScreenCapture.CaptureScreenshot` to see `OnGUI` HUDs.
  A faster way for one object's components: in Play mode, loop
  `go.SendMessage("Update")` (and `"LateUpdate"`) with a short sleep between
  calls, so jobs finish; e.g. pumping the `World` object runs `WorldView` and
  `RenderDistanceSetting` frame by frame. For pictures, render the world from
  an edit-mode `WorldStreamer` into a `RenderTexture` with a temporary camera
  (no texture array there, so the terrain shows untextured).
- MCP gotcha: an asset created in a `RunCommand` script and then assigned
  after `EditorSceneManager.OpenScene` saves as a null reference (the
  object goes stale). Re-load it with `AssetDatabase.LoadAssetAtPath`
  right before assigning, and check the saved scene for `{fileID: 0}`.
- Unity gotcha: when splitting a type out into a new file from the shell,
  create the new file first and edit the old one after. If Unity compiles
  in between, it can register the edited file without the new one and keep
  failing ("type not found") even after a clean rebuild. Fix: move the new
  file out and back with `AssetDatabase.MoveAsset` (keeps the GUID).
- Benchmarks: `GC.GetAllocatedBytesForCurrentThread` always reads 0 on
  Unity's Mono; measure allocations by heap growth (`GC.GetTotalMemory`)
  averaged over runs. In `RunCommand` scripts `System.Diagnostics.Stopwatch`
  doesn't resolve; time with `EditorApplication.timeSinceStartup`.
- MCP gotcha: `AssetDatabase.DeleteAsset` inside `Unity_RunCommand` is
  refused as a "user interaction"; delete through `ManageAsset` Delete.
- Batch builds from a copy: keep the copy's path short (e.g. map it to a
  drive letter with `subst Q: <copy>`). Under the long scratchpad path,
  files in `Library/PackageCache` pass Windows' 260-character limit, so
  URP and Shader Graph import partly; tests still pass, but the player
  hangs at startup on mismatched URP assets.
- Running tests: several Unity versions are installed; use
  `C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe`.
- MCP gotcha: `RunCommand` refuses `System.Reflection`, so it can't
  discover tests. For a quick in-Editor check, call test methods directly
  (`new ChunkMesherTests().SomeTest()`, calling `[SetUp]` methods first);
  NUnit's `Assert` works without the runner. Still run the full suite in
  batch mode before a PR.
- MCP gotcha: after writing scripts from the shell, Unity only refreshes
  when its window is focused. Call `AssetDatabase.Refresh()` through
  `RunCommand`; the bridge then drops for the recompile, and is back once
  `~/.unity/mcp/connections/bridge-*.json` exists again (the folder is
  emptied during the reload).
- MCP gotcha: modal Editor dialogs (e.g. "scene modified outside Unity —
  reload?") block the main thread and so the bridge. Save and switch to an
  empty scene before git operations that change open scene files, and
  never edit an open scene's YAML from the shell.
- MCP gotcha: GameObject instance IDs change on every domain reload
  (entering Play mode, recompiling). Look objects up again with `find`
  before passing an ID to tools like `Camera_Capture`.
- Lab tools: a chunk-level tool goes in the Chunk Tools prefab and gets its
  chunk from `LabChunkTarget` (never `GetComponent<ChunkView>()`), so it also
  runs on WorldLab's focused chunk; a voxel-level tool goes in Voxel Tools and
  works through `VoxelCornerEditor`. Its panel controls go in a `*Controls`
  class that `LabPanel` draws when the tool is in the scene. Edits go through
  the target's edit path, never straight to a world chunk (M2).
- The Inspector is a superset of the lab panel (M22): never add a panel-only
  knob. Draw panel sections in Inspectors with `LabPanelFrame.ForInspector`
  through `InspectorLabControls` (records edits for Undo and saving), as
  `LabPanelEditor`, `ChunkViewEditor` and `WorldViewEditor` do.
- Naming/formatting: follow the existing code (private fields camelCase,
  `[SerializeField] private`, XML doc comments on public types). Formalize
  later if needed.
- Jobs: a generic job scheduled from generic code (e.g.
  `HeightfieldGenerator<T>`) needs an `[assembly: RegisterGenericJobType]`
  line per concrete type (`Generation/Fill/ChunkFillJobs.cs`), or Burst
  never compiles it. Native buffers that outlive 4 frames must be
  `Allocator.Persistent`. Main-thread reads of a `[ReadOnly]` array while
  jobs read it are fine; writes are not.
- Two implementations of one thing (managed ↔ Burst mesher, `OreField.Pick`
  ↔ the fill kernel, `MarchingCubesJobTables` ↔ `MarchingCubesTables`)
  each have a test comparing them; keep it, and fix the side that's wrong.
- Unity gotcha: a `MeshCollider` keeps its old shape when its mesh's data
  changes (no re-cook); reassign (`null`, then the mesh) to update it, after
  `Physics.BakeMesh` in a job so the assignment is cheap (0.03 ms against
  8 ms). `Physics.BakeMesh(int, …)` is obsolete in 6.3: pass an `EntityId`
  (implicit from `GetInstanceID()`).
- Performance gotcha: in the Editor a garbage collection over its large heap
  stalls a frame by 5-10 ms, so per-chunk allocations show up as spikes.
  Pool chunk arrays (`VoxelArrayPool`) and native buffers; check
  `WorldView.Stats.Phases` before guessing where a frame went.
- Compile errors after shell edits may not reach `ReadConsole`; search
  `%LOCALAPPDATA%/Unity/Editor/Editor.log` for `error CS` before assuming a script compiled.
- Placeholder terrain textures (`MaterialTextureTools`) are seeded by entry order: append new
  materials at the end, and after regenerating, `git checkout` any unrelated PNGs that changed,
  then rebuild the texture array.
- MCP gotcha: in `RunCommand` scripts `Mesh` resolves to a `Unity.AI.Mesh`
  namespace; write `UnityEngine.Mesh` (or alias it).
- Burst gotcha: after a long Editor session the Burst JIT can stop rebuilding
  and keep running an old native copy of a job against changed structs (seen
  2026-10-07: generation produced floating garbage after a pull). Signs: a job
  is wrong but the same struct's `Execute()` called directly is right, and
  Burst stack traces cite line numbers from an older version of the file.
  Fix: close Unity, delete `Library/BurstCache`, reopen. Seen again 2026-10-09 straight after
  adding fields to `LayerTable` (a struct the fill jobs read) in a running Editor: the
  world generated as garbage. To keep working until a restart, turn Burst compilation off
  (`BurstCompiler.Options.EnableBurstCompilation = false`); `GenerationJobTests` then pass.
- Player screens (inventory, station, merchant) set `PlayerController.IsInMenu`,
  not the cursor: FreeCursor can't lock the cursor under an open screen, and
  `MenuClosedThisFrame` stops the E that closed a screen from reopening it.

## Environment
- Unity 6.3 LTS, URP, Windows Build Support (IL2CPP)
- C#, Visual Studio
- Unity MCP connected for Editor-level control (Inspector values, scene
  state, console errors) — not just file edits
