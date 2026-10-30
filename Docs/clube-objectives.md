# clube — Destructible Terrain Demo: Scoped Objectives

Project: `E:\Repos\Unity3D\clube` (fresh restart; earlier terrain-demo repo is reference only, not ported)
Target: Milestone 1 — Destructible Terrain Demo = **Chapters 0–3** (ends at Checkpoint 3.2)
Next: **Milestone 2 — First gameplay loop** (collect → refine → craft → sell), scheduled straight after Checkpoint 3.2 and ahead of the rest of Chapter 4 *(added 2026-10-06)*

Each objective has an ID so it maps 1:1 to a GitHub issue; PRs group objectives per sub-section (see G2). Items marked *(added)* are suggestions beyond the original list — keep, cut, or defer as you like. Items marked *(backlog)* were imported from the 2026-10-01 ideas backlog and are just as open to change.

**Plan maturity:** Chapters 0–3 are scoped and schedulable, and so is Milestone 2 (the first pass of the loop, pulling thin slices of Chapters 5–7 forward). Chapters 4–5 are drafts. Chapters 6–8 are design exploration — context for decisions, not work to schedule.

**Settled decisions:**
- Render pipeline: **URP** (project created from the URP template). Shader work (M11, M13, M14) targets URP.
- Engine: Unity 6.3 LTS, Windows build via IL2CPP.

**Game vision:** an economic success story in a medieval world. The player starts their own venture and grows it through ore extraction, refining, crafting, and trade, until they out-compete or take control of the local towns.

**Direction of travel:** early chapters are learning/debugging labs driven from the inspector; later chapters should feel like a game demo, driven from the game window. The same core code runs underneath both — labs add debugging on top, the game leaves it off.

---

## Tags

Every objective is tagged with where it lives:

| Tag | Meaning | Reaches the Game scene? |
|---|---|---|
| `Core` | Engine code in `Clube.Core`, used by labs and game alike | Yes |
| `Config` | A value in the `WorldConfig` asset — editable live in labs, fixed at startup in the game | Yes, read-only |
| `Lab` | Debug component, gizmo, readout, or lab-scene tool in `Clube.Debug` | No |
| `Game` | Player-facing feature or setting | Yes |

A combined tag like `Core + Lab` means the logic lives in the core and a lab component drives or exposes it.

---

## Architecture

- [ ] **A1** Assemblies: `Clube.Core` (data, meshing, generation, chunk management), `Clube.Debug` (lab components, gizmos, readouts), `Clube.Game` (player, UI, settings). `Clube.Debug` and `Clube.Game` reference `Clube.Core`; **Core never references Debug or Game.** *(2026-10-05: `Clube.Debug` also references `Clube.Game`, so lab tools can tune the player (M16); Game never references Debug.)*
- [ ] **A2** `WorldConfig` ScriptableObject holds world-definition values (voxel size, chunk dimensions, iso value, generator + seed). Game reads it once at startup; labs may change it and force a regenerate.
- [ ] **A3** Player settings (render distance, brush size, sensitivity) live in a separate settings object owned by `Clube.Game`, changed through an in-game menu.
- [ ] **A4** Core exposes read-only debug data (densities, per-voxel case index, build timings) rather than calling into debug code. If debug needs something, the core exposes more data.
- [ ] **A5** Debug-only data collection (e.g. storing case indices) wrapped in `[Conditional("CLUBE_DEBUG")]` or `#if DEVELOPMENT_BUILD` so it compiles out of release builds.
- [x] **A6** Algorithm variants (interpolated vs midpoint edges, flat vs smooth shading) are chosen **once per chunk build** — strategy object, or generic type parameters under Burst — never checked per vertex. Labs can switch variants; the game locks one in.
- [x] **A7** One density-edit path in the core (write densities → mark chunk dirty → rebuild). Lab corner sliders and the in-game brush are both just callers of it.
- [x] **A8** A single voxel is a 1×1×1 chunk. No separate single-voxel meshing code.
- [ ] **A9** Scenes as stages over the same core:
  - `VoxelLab` — 1×1×1 chunk + heavy debug components
  - `ChunkLab` — one chunk + selection, generator switching, benchmarks
  - `WorldLab` — a fixed square of chunks (about 100 m), walls showing its underground at the edges, chunk focus mode, stats *(changed 2026-10-08, M27)*
  - `ProceduralWorldLab` — the streamed world: chunk manager around the focus, chunk focus mode, stats *(the old WorldLab, renamed 2026-10-08, M27)*
  - `Game` — chunk manager, player, settings menu, **no debug**
  - *(Added)* Each lab stacks the ones below it with the same scripts: ChunkLab's selected voxel gets every VoxelLab tool, WorldLab's focused chunk every ChunkLab tool (3A+).
- [ ] **A10** Release build check: `Game` scene has no `Clube.Debug` components, and the debug assembly is excluded from release builds.
- [ ] **A11** Step recording: the mesher accepts an optional recorder that logs each algorithm step as data. Compiled out of release builds (A5) and zero cost when no recorder is passed. Playback lives in `Clube.Debug` and never re-runs the algorithm itself.
- [ ] **A12** Voxel storage behind an interface (`IVoxelStorage`: get/set density and material, iterate, serialize). Meshing, editing and saving talk only to the interface, so flat array, RLE and octree implementations can be swapped and compared. *(2026-10-05, M10: material ids are kept per chunk in `VoxelMaterials`, beside the density storage, rather than in each scheme.)*
- [ ] **A13** *(added 2026-10-05)* Lab scenes and the game are built for different jobs. The labs teach and measure, and may carry overhead; the `Game` scene is lean: it picks the fastest options (e.g. the Burst mesher K12, single-byte storage M12, no step recording) and leaves out lab-only work (volume measurement, the build grid, storage views). Goes further than A10, which only keeps `Clube.Debug` out. Open: a separate game `WorldConfig` vs code defaults, and whether `Game` uses the lab prefabs at all. *(Progress 2026-10-05, K35: the streamed world now always runs the fast path, whatever the lab config says: Burst generation and meshing, byte storage, colliders near the focus only; the managed mesher and step-through stay for the single-chunk labs. See `Docs/architecture.md` §6.)* *(2026-10-08: the labs keep chunk size, build grid and voxel size adjustable for testing; the gameplay demo scenes use the settled values in GW1: 16³ chunks, 1 m build cells, 0.25 m voxels pending M25.)*
- [x] **A14** *(added 2026-10-05)* Core is layered, and each layer only uses the ones below it: data (`Voxels`, `World`, `Materials`, `Config`, `Items`, `Crafting`, `Economy`, `Quests`) → algorithms (`Generation`, `Meshing`, `Editing`, `Queries`, `Volume`, `Saving`) → runtime (`Streaming`: jobs and the per-frame policy) → presentation (`Rendering`: MonoBehaviours, meshes, colliders). Data and algorithms never reach up; lab code hooks Core's events (A4). The map, the threading rules and where upcoming systems plug in are in `Docs/architecture.md`, kept current with the code.

---

## Ground rules (apply to every chapter)

- [ ] **G1** Every runtime-editable parameter triggers a regenerate via a dirty flag (no regen every frame).
- [ ] **G2** One objective = one GitHub issue. One sub-section (e.g. 1A, 2C) = one branch = one PR, which lists the objective IDs it closes and which acceptance criteria were met. Split a sub-section into several PRs if the diff stops being reviewable in one sitting; never bundle across sub-sections.
- [ ] **G5** *(added)* Docs live in `Docs/` (capital D — CI runs on case-sensitive Linux, so match it exactly).
- [ ] **G3** *(added)* Edit-mode unit tests (Unity Test Framework) for pure `Core` logic: case index lookup, interpolation, volume math, coordinates, seams, determinism.
- [ ] **G4** *(added)* Tests run on every push/PR via GitHub Actions (GameCI).

---

## Chapter 0 — Minimal scene *(added — current step)*

- [x] **C0.1** `Lab` `VoxelLab` scene shell: camera, directional light, free-fly camera controller (orbit deferred to K6 camera focus). The controller lives in `Clube.Debug` and is the same debug camera M7 toggles to; the `Game` scene gets its own player camera in M6.
- [x] **C0.2** `Core` Folder + namespace structure and assembly definitions per A1.
- [x] **C0.3** `.gitignore`, `.gitattributes` (line endings, Unity YAML merge, LFS if needed), README with how to run.
- [x] **C0.4** *(added)* Migrate the pre-plan single-voxel prototype (PR #1): move the stateless `MarchingCubes` mesher and tables into `Clube.Core`, the gizmos into `Clube.Debug`, rename `SingleVoxel.unity` → `VoxelLab.unity`, remove the template `SampleScene`. (Rebuilding `SingleVoxel` as a 1×1×1 chunk per A8 happens in Chapter 1, not here.)

**Done when:** `VoxelLab` plays, camera moves, assemblies compile with Core referencing neither Debug nor Game, and the repo is clean on a fresh clone.

---

## Chapter 1 — Single voxel (`VoxelLab`)

Goal: fully interrogate marching cubes mechanics on one cube at runtime via the inspector. Built on a 1×1×1 chunk (A8).

Note: V1, V2 and V6 now run on the 1×1×1 chunk through the A7 edit path (the pre-plan `SingleVoxel` component is gone). A2 and A12 are started but not complete: `IVoxelStorage` has density only (materials M10, iteration and serialization K26).

### 1A — Core controls
- [x] **V1** `Config` Iso value slider.
- [x] **V2** `Core + Lab` Individual sliders for the 8 corner values (via the A7 edit path).
- [x] **V3** `Core + Lab` Interpolated edge vertices vs edge midpoints (A6 variant).
- [x] **V4** `Core + Lab` Smooth vs flat shading (A6 variant).
- [x] **V5** `Lab` Toggle triangle winding / draw direction (`FlipFaces` component).

### 1B — Visual debugging
- [x] **V6** `Lab` Corner gizmo spheres, grayscale by corner value.
- [x] **V7** `Lab` *(added)* Show current case index (0–255) and which of the 15 base configurations it maps to.
- [x] **V8** `Lab` *(added)* Normal gizmos per vertex/face (verifies V4 and V5). Drawn as a line mesh (`NormalLines`), not gizmos.
- [x] **V9** `Lab` *(added)* Highlight active edges from the edge table.
- [x] **V10** `Lab` *(added)* Preset buttons for known cases, including ambiguous ones.
- [x] **V20** `Lab` *(added)* Type a case index (0–255) to jump straight to it: corners whose bit is set become solid, the rest empty. Step one case at a time with ◀ ▶ in the Inspector or the left/right arrow keys in Play mode.
- [x] **V21** `Lab` *(added)* Teaching labels on the cube corners: each corner's index and the bit it sets in the case index (e.g. "c3 → bit 3 = 8"), marked solid or empty, alongside the resulting case index in binary and decimal. Shows how corners map to the case index and from there to the triangles.

### 1C — Volume inspection
- [x] **V11** `Lab` Approximate fill: weighted-percentage volume from corner values. *(Already in Core as `VoxelVolume`, alongside V12 and the V14 reference, since mining (I5) will need it.)*
- [x] **V12** `Lab` Exact fill via tetrahedralisation of the enclosed solid.
  - Note: the MC surface alone is open; the enclosed solid is the mesh **plus** the cube faces clipped to the inside region. Close it first, then split it into separate pieces and fan each piece into tetrahedra from one of its own solid corners, so every tetrahedron lies inside the solid (a single corner is one tetrahedron). Fanning from an arbitrary reference point also sums to the right volume, but produces inside-out tetrahedra that make the exploded view misleading.
- [x] **V13** `Lab` Exploded view of the tetrahedra (explode-distance slider, per-tet colouring).
- [x] **V14** `Lab` *(added)* Readout comparing V11 vs V12 (absolute and % error); optional Monte Carlo sample count as a third reference.

### 1D — Step-through animation
Record the real algorithm once (A11), then replay the log. Stepping back just rebuilds state up to an earlier point in the log.

- [x] **V15** `Core` Step recorder logging a struct per step: step type, voxel coordinate, corner values, case index, edge mask, vertex positions, triangle.
- [x] **V16** `Lab` Playback of the steps:
  1. Density field — the terrain data, a 3D grid of density samples in grayscale (once per build)
  2. Sample corners — sample the cube's 8 corners and colour each solid/empty against the iso value
  3. Case index — combine the 8 bits into the case index, corner by corner, shown in binary and decimal: the row to look up in the tables
  4. Edge table — highlight crossed edges
  5. Interpolate — slide each vertex along its edge (or snap to midpoint per V3)
  6. Triangle table — add triangles one at a time, showing winding order
- [x] **V17** `Lab` Controls: play/pause (Space), step forward/back (right/left arrow keys, new Input System), restart (R), speed slider, per-step-type durations. The arrows are shared with V20 case stepping through a mode switch: while step-through mode is on, they step the animation.
- [x] **V18** `Lab` Info line describing the current step, e.g. "Voxel (3,1,2): corners 0, 3, 5 inside → case 41 → edges 0, 3, 8."
- [x] **V19** `Lab` Visuals that work in the game window, not Gizmos: pooled spheres for corners, `LineRenderer` for edges, separate mesh for the partial surface, on-screen UI for the info line.
- [ ] **V23** `Lab` *(backlog)* Code panel beside the playback: show the snippet of mesher code that the current step runs, with the active line highlighted, so the animation doubles as a code walkthrough.
  - Note: show curated, simplified snippets (stored as lab text assets keyed by step type) rather than the real source, so refactoring the mesher doesn't silently break the lesson.

### 1E — Standalone build *(backlog, optional)*
- [ ] **V22** `Lab` Build `VoxelLab` as a standalone Windows exe (development build) and check it runs.
  - Note: the voxel lab panel is a custom inspector (`Clube.Debug.Editor`), which doesn't exist in a build. The exe needs in-game controls, which overlaps with V19's game-window visuals — do this after 1D. It also needs `Clube.Debug` included, so it's a development build, unlike the release check in A10.
  - *(Update)* VoxelLab is now in the demo exe: the `DemoMenu` "Single voxel" button launches it, with an Exit button and a `ControlsHint` line listing its keys (arrows step the 256 cases, fly camera, scroll explodes the volume). It's a normal build, not a development one, so step-through recording (A5) is compiled out there. Left unticked until it's been playtested in the exe.

**Done when:** every toggle updates the mesh live, V12 returns 0 for an all-outside cube and 1 (× unit volume) for all-inside, and the V16 sequence can be played through or stepped both ways for any preset case (V10).

---

## Chapter 2 — Single chunk (`ChunkLab`)

Goal: scale to an X×Y×Z grid while keeping all Chapter 1 controls reachable.

Build order *(changed)*: 2A–2C, 2E (with the first demo exe, K33), 2F, then Chapter 3A. The rest of 2D and all of 2G come after 3A: Jobs/Burst, loop speed-ups and storage schemes are measured on a multi-chunk world, where they matter.

### 2A — Structure
- [x] **K1** `Config` Chunk size (X, Y, Z integers). Changing it in a lab replaces the chunk with a new, empty one (`ChunkView.ChunkCreated`); meshes switch to 32-bit indices past 65,535 vertices.
- [x] **K2** `Config` Voxel unit size.
- [x] **K3** `Lab` Chapter 1 global toggles (iso, interpolation, shading, winding, gizmos) apply chunk-wide. `ChunkDebugView` draws the density spheres, chunk outline and optional voxel grid as real meshes (not gizmos, which Unity mis-dims on D3D12), and suppresses the spheres above 40,000 samples.
- [x] **K4** `Lab` *(added)* Stats readout: vertex count, triangle count, last mesh build time (ms), split into meshing and Unity mesh upload (`ChunkMeshStats`, shown by `ChunkStatsHud` and the `ChunkView` Inspector).
- [x] **K30** `Core + Lab` *(added)* Chunk volume readout: the solid volume inside the chunk by both Chapter 1 methods, approximate (V11) and exact tetrahedra (V12), summed over every voxel (`ChunkVolume`, shown via `ChunkVolumeStats`). The exact sum costs about 165 ms at 32³, so its *Calculate* toggle switches the measurement off when editing large chunks.
- [x] **K31** `Lab` *(added)* Lab control panel: one place for the settings used all the time, instead of hunting through every component's Inspector (colours, materials and rarely used toggles stay on the components). Probably an in-game settings GUI, which would also give the standalone exe (V22) its controls. Settings:
  - Config: voxel size, iso level, edge placement, shading
  - Test fill: shape, radius
  - Display: show samples, wireframe (none / outline / voxel grid), show face normals (vertex normals stay tucked away on `NormalLines`), flip faces
  - Triangle edges: one toggle that covers both the whole mesh's triangle edges and the selected voxel's highlighted triangles
  - Selected voxel: show corner labels
  - Volume: exact (tetrahedra) or approximate only
  - *(Progress)* In-game panels now exist for both labs on a shared `LabPanelFrame` (foldout sections, scrolling, click blocking): `VoxelLabPanel` (corners, case, presets, meshing, step-through, volume, display) and `ChunkLabPanel` (terrain, brush, meshing, step-through, display), with shared `MeshingControls` and `StepThroughControls`. Still open: voxel size, test fill, the selected voxel's controls (K7) in ChunkLab, and one triangle-edges toggle. The demo exe keeps step-through recording via `CLUBE_LAB_BUILD`.
  - Result: one `LabPanel` in every lab (M20), built from shared `*Controls` classes. It covers all of the above: voxel size and test fill in ChunkLab's Chunk section, the selected voxel's corners, case and presets, one triangle-outline control (none, the voxel's, or all) and chunk volume on or off.

### 2B — Voxel selection
- [x] **K5** `Lab` Click to select a voxel; highlight it. Picks the voxel whose surface the click ray hits first (`SurfaceRaycast` on top of `VoxelRaycast`, Core); voxels without surface can't be picked. Shown with a line-mesh wireframe.
- [x] **K6** `Lab` Focus camera on selected voxel (F, gliding the free-fly camera).
- [x] **K7** `Lab` Selected voxel exposes the Chapter 1 per-voxel controls. Editing a corner goes through A7 and updates neighbouring voxels that share it. (`SelectedVoxelEditor`: corner sliders noting how many voxels share each corner, plus the case and preset panel shared with VoxelLab via `VoxelCaseGui`.)

### 2C — Terrain surface generation
- [x] **K8** `Config` Base surface level + amplitude parameters (`TerrainSettings` in `WorldConfig`).
- [x] **K9** `Core` Generators, selected via `Config` (the lab-only `ChunkTestFill` shapes from 2A, a ball and a solid block with a shaft, stay alongside them as test fixtures):
  - [x] Flat
  - [x] Sine wave
  - [x] 2D Perlin
  - [x] 3D Perlin
  - [x] 2D Perlin with octaves (frequency, lacunarity, persistence)
  - [x] Spline-based (height remapping curve over fractal 2D Perlin)
  - [x] Heightmap import (PNG/RAW: red channel of a readable texture, or a square 8/16-bit RAW)
- [x] **K10** `Config` *(added)* Seed parameter for all noise generators (seeded `PerlinNoise`, so the same seed always gives the same terrain).
- [x] **K29** `Lab` *(backlog)* Heightmap export: write the chunk's surface height per column as a grayscale image (one byte per pixel). Round-trips with K9's heightmap import, which makes a handy test. (`HeightmapExport` in Core; "Export heightmap PNG" button on `ChunkView` in Play mode, saved to `Assets/Heightmaps/`; a round-trip test checks the surface comes back within one 8-bit step.)
  - Note: the chunk is 3D, so "height" means the topmost iso-crossing in each column; overhangs and caves are lost.

### 2D — Performance *(rest after 3A)*
K11, K32, K12 and K35 are done.

- [x] **K11** `Lab` Benchmark: `List<T>` vs preallocated arrays for mesh building.
  - Method: Stopwatch over N runs after warmup; record ms and GC allocations (Profiler / `GC.GetAllocatedBytesForCurrentThread`); test at 3+ chunk sizes.
  - Output: results table in the repo (`Docs/benchmarks.md`). Winner becomes the `Core` implementation.
  - Result: no measurable difference (within 1%), and worst-case arrays hold 60 MB at 64³, so reused `List<T>` stays in Core. The time goes into visiting voxels, not storing the mesh. (`MeshStorageBenchmark`, *Clube → Benchmarks*. Allocations come from heap growth, because `GC.GetAllocatedBytesForCurrentThread` reads 0 on Mono.)
- [x] **K12** `Lab` *(added, optional)* Third variant: `NativeArray` + Jobs/Burst — sets up Chapter 4 threading. *(Also a backlog item: "code test of Burst-compiled jobs".)*
  - Result: `BurstChunkMesher` (Core) runs the K32 loop as a Burst job over native arrays and gives the same mesh (`BurstChunkMesherTests`). `WorldConfig.Mesher` picks managed or Burst for every chunk build (A6; step-through always records the managed one), in the panel's and Inspector's Meshing section. With Burst safety checks off: 64³ in 1.35 ms (4–5× K32); a render distance 4 world meshes in 3.4 ms one chunk at a time, 1.0 ms with one job per chunk in parallel (13×). Generation (K35) is now the whole cost. Details in `Docs/benchmarks.md`. Burst, Collections and Mathematics are now listed in the manifest (they were already installed as dependencies).
- [x] **K32** `Core + Lab` *(added, from K11)* Speed up the mesher's per-voxel loop, benchmarked against the K11 baseline (77 ms at 64³). Candidates: read densities straight from the flat storage, not through `IVoxelStorage` per corner; reuse the 4 corners shared with the previous voxel; store the crossed-edge mask as a 256-entry table; and skip all-solid or all-empty runs early.
  - Result: 64³ meshing from 81 to 5.5 ms flat (15×) and 83 to 7.1 ms smooth (`MesherSpeedBenchmark`, *Clube → Benchmarks → Mesher speed*). Densities are read a Z layer at a time through a new `IVoxelStorage.ReadLayer` rather than straight from the flat array, so the mesher stays behind A12 and RLE and octree storage (K24, K25) must implement it too. The layer reads with corner reuse gave 11× on their own; the edge table and early skip about 15% and 8%. Smooth shading's `SharedVertexWriter` dictionary is now its main extra cost. Details in `Docs/benchmarks.md`.
  - At world scale (WorldLab's 16³ chunks, `WorldMeshBenchmark`): meshing 1.30 → 0.13 ms per chunk, 127 → 13 ms for the 98 chunks at render distance 4. Generation (~3.8 ms per chunk) is now 97% of loading a world: K35.
- [x] **K35** `Core + Lab` *(added, from K32)* Speed up terrain generation, now 97% of loading a world (3.8 ms per 16³ chunk vs 0.13 ms to mesh it). Heightfield generators sample the noise for every sample, but need one height per column; measure with `WorldMeshBenchmark`.
  - Result *(2026-10-05)*: generation runs as Burst jobs (`Generation/Fill`): heightfields compute one height per column, shared by every chunk stacked on it; every terrain shape is a Burst struct the managed generator classes wrap (one copy of the maths); chunks that come out all air or all solid keep no density storage (`Chunk.Uniform`), and a chunk above its column's highest point needs no job. 32³ chunks at 1/8 m: 31.5 → 0.3-0.45 ms per chunk on one thread, render distance 6 (904 chunks) in 21 ms across the workers. Densities are unchanged at 1 m voxels; at 1/8 m Burst's rounding moves 1-4 samples in 36k by one byte step. Done together with P1-P3 (streaming on jobs); details in `Docs/benchmarks.md`.

### 2E — Terrain editing
- [x] **K13** `Core + Lab` Click to add terrain (A7 path). (`TerrainBrush` in Core; `TerrainBrushTool` in ChunkLab: 1/2/3 pick Select, Dig or Add, and holding the button repeats.)
- [x] **K14** `Core + Lab` Click to remove terrain (A7 path).
- [x] **K15** `Core + Lab` Brush radius control (in world units; [ and ] in Play mode; a translucent sphere previews it).
- [x] **K16** `Core + Lab` *(added)* Brush strength / falloff. *(Changed after 3A: was a ramped hard sphere and a smooth fade.)* **Hard** adds or removes the strength at every sample within the radius. **Soft** only changes the surface layer within the radius: adding fills samples next to fully solid ones (density 1), removing empties samples next to fully empty ones (0), so holding it piles up or digs down a layer at a time. The brush runs on an `IDensityField` (a chunk, or the whole world, so it sees across borders) and returns a `BrushResult` with the density added and removed; `TerrainBrushTool` keeps running volume totals.
- [x] **K33** `Lab` *(added)* Demo exe: a Windows build of `ChunkLab` that can be played: fly around, pick a generator, dig and add terrain with the brush (K13–K16), with the stats HUD.
  - The custom inspectors don't exist in a build, so the demo needs in-game controls. This starts K31's control panel with only what the demo needs: generator and seed, brush mode, radius and strength, wireframe, and show samples.
  - Repeatable build: a menu item (*Clube → Build → Lab demo*, formerly "ChunkLab demo") writing to `Builds/` (already gitignored). It includes `Clube.Debug`, which is fine for a lab build; A10's check still applies to `Game`. Not a development build, which would open a profiler port (a firewall prompt) and add a watermark.
  - Does for `ChunkLab` what V22 planned for `VoxelLab`; V22 stays optional.
- [x] **K34** `Lab` *(added, from K33 playtest)* Demo exe window fixes:
  - Make the window resizable. Player Settings has `resizableWindow: 0` and `fullscreenMode: 1` (fullscreen window); switch to a resizable window, probably windowed by default.
  - Fix text that gets cut off in the exe. The IMGUI panel and labels assume an Editor-sized screen: check the control panel, the stats HUD and the bottom tool label at small window sizes (e.g. 1280×720, 853×480), and let wrapped or long lines grow instead of clipping.
  - Welcome screen: a `DemoMenu` scene ("Welcome to clube") opens the exe, with Play (loads ChunkLab) and Exit (closes the program). Later: Single voxel (VoxelLab, V22) and Single chunk (ChunkLab) instead of Play. In ChunkLab an "Exit to menu" button (and Esc) returns to it. (`DemoMenu`, `DemoExitButton`, `DemoScenes` in `Clube.Debug`.)
  - Result: windowed 1280×720 and resizable; the panel scrolls when the window is too short and drops its fixed-width labels; the bottom tool label wraps. Playtested in the exe.

### 2F — Step-through animation at chunk scale
Extends Chapter 1 playback (V15–V19) so the mesh can be watched growing voxel by voxel.

- [x] **K17** `Lab` Opening step: show the sampled density field as a grid of grayscale points before any voxel is processed. (One combined sphere mesh, `SampleSpheres`, shared with `ChunkDebugView`; revealed by shortening the drawn index range. Not drawn above 40,000 samples.)
- [x] **K18** `Lab` Closing step: calculate normals and show the finished shaded mesh. (`MeshingStepType.Normals`, logged once after the last voxel. The mesher doesn't compute normals, `ChunkView` does with `Mesh.RecalculateNormals`, so the step grows a line from each triangle's centre along its face normal (from its winding), then shows the mesher's real output shaded with Unity's vertex normals.)
- [x] **K19** `Lab` Granularity setting: step by sub-step, whole voxel, or whole Z slice. *(Changed from "Y layer": the mesher's outer loop is z, so a Z slice is one run of the build in true order (A11); a Y layer isn't.)* (`StepUnits` groups the steps; a voxel or slice plays its sub-steps in proportion to their timings. G cycles it. 0.8 s per voxel, so a 4×4×4 chunk plays through in about a minute.)
- [x] **K20** `Lab` Skip-empty-voxels toggle (cases 0 and 255 produce no triangles and dominate terrain chunks). (H, or the Inspector. Skipped voxels get no units and take no time inside a slice; an all-empty slice is skipped. In ChunkLab's 8³ terrain 418 of 512 voxels are empty, and voxel stepping drops from 413 s to 79 s.)
- [x] **K21** `Lab` Jump to voxel, tied to voxel selection (K5). (`StepThroughVoxelLink`: with step-through on, selecting a voxel pauses playback at the start of its build at any granularity, so Play shows it being built; also a panel button and J. While step-through is on, the selection overlay hides its corner labels and triangle edges and the selection label leaves out the case, since step-through reveals those.)
- [x] **K22** `Lab` Optional camera follow on the current voxel. (`StepThroughCameraFollow`: each new voxel glides the free-fly camera to frame it, the same glide as F (K6); panel toggle and C.)

### 2G — Voxel data storage exploration *(after 3A)*
Compare storage schemes on a single chunk, behind the A12 interface, then measured again at scale (M12). Moved after 3A so both run against the same multi-chunk world.

- [x] **K23** `Core` Flat array baseline (current storage).
- [x] **K24** `Core` Run-length encoding: runs along one axis; test at least two run orders (e.g. X-first vs Y-first) since terrain is mostly vertical layers.
  - Result: `RunLengthVoxelStorage` along X, Y or Z. X and Z beat Y: every column crosses the surface (at least three runs), while most horizontal lines lie wholly in solid or air. X serializes 12× smaller than flat at 64³; in memory it's 0.40× (per-line overhead). See K26.
- [x] **K25** `Core` Sparse octree: uniform regions collapse into single nodes; configurable max depth.
  - Result: `OctreeVoxelStorage`: power-of-two cube, uniform leaves, eight children or bricks at the maximum depth; equal children collapse back, uniform bricks turn into leaves. With 4³ bricks it's the smallest in memory (0.28× at 64³), ~35% slower per brush stroke.
- [x] **K26** `Lab` Benchmark each scheme for memory size, read speed during meshing, write speed during brush edits, and serialized size.
  - Note: smooth marching cubes needs varied densities near the surface, so compression mostly comes from solid and air regions. Also test with quantized densities (e.g. byte instead of float) to see how that changes the results.
  - Output: results added to `Docs/benchmarks.md`.
  - Result: `StorageBenchmark` (*Clube → Benchmarks → Voxel storage*), Hills and Caves at 16³-64³, in `Docs/benchmarks.md`. Flat float stays the default; flat byte and the 4³-brick octree are the M12 candidates; run-length X is the saved form. Quantizing to bytes changes RLE and octree compression by under 1%.
- [x] **K27** `Lab` *(added)* Visualize the storage: RLE runs as coloured bars, octree nodes as wireframe boxes at each depth.
  - Result: `StorageView` in the Chunk Tools prefab (so every lab, and WorldLab's focused chunk): runs as bars coloured air, surface or solid, with gaps between runs; octree leaves as boxes coloured by depth, bricks brighter. The panel's and Inspector's Storage section picks the scheme and octree depth and shows the chunk's memory and saved size.
- [x] **K28** `Core` *(backlog)* Single-byte densities: store each density as one byte to keep data tight and simple, and benchmark it against float in K26.
  - Note: density is signed around the iso surface (negative = air, positive = solid), so use `sbyte`, or `byte` with 128 as the zero point. 256 levels is plenty for smooth surfaces but coarse for gentle brush falloff (K16); check for visible stepping. Answers the "density type" open question.
  - Result: `ByteVoxelStorage`. Our densities run 0-1 with the surface at the iso level, so an unsigned byte maps them directly (0 empty, 255 solid). 0.25× memory; vertices move 0.004-0.005 voxels on average, and a sample right at the iso level can round across it (rare slivers, up to ~0.5 voxels): no visible stepping, including after soft brushing.

**Done when:** a 32³ chunk can be generated with any K9 generator and edited in play mode and in the demo exe (K33); a 4×4×4 chunk can be played through at voxel granularity in about a minute. (2D's and 2G's benchmark results are committed after 3A.)

---

## Chapter 3 — Collection of chunks (`WorldLab` → `Game`)

This is where the shift happens: lab controls stay in `WorldLab`, and `Game` gets its first playable form.

### 3A — Chunk management
After 3A: 2D (K32, K12), the stacked labs (3A+), 2G and M12 are done; next 3B.

- [x] **M1** `Core` *(added — prerequisite)* Chunk coordinate system and chunk manager (world pos ↔ chunk ↔ voxel). (`WorldGrid`: world position, global sample, chunk and local sample, floored so negative coordinates work. `World` holds the loaded chunks; `WorldView` is the chunk manager: streams chunks around a focus point, nearest first and a few per frame, and pools their `ChunkRenderer`s. Chunks are cubes on a 3D grid, stacked `WorldHeightInChunks` layers high from y = 0; see M17.)
- [x] **M2** `Core` *(added — prerequisite)* Seamless borders: chunks share edge density samples so no cracks/gaps. (Each chunk keeps its own copy of the shared border samples and `World` keeps every copy equal: generation samples world positions, `World.SetDensity` writes all copies, and a newly loaded chunk takes its border from edited neighbours. Tests check shared samples and that border vertices match. Smooth-shading normals are still computed per chunk, so lighting can show a faint line at a border.)
- [x] **M3** `Game` Render distance setting controls how many chunks are loaded (player setting per A3, not a config value). (`PlayerSettings` asset in `Clube.Game` with `RenderDistanceSetting` applying it to `WorldView.RenderDistance`, a radius in chunks horizontally; chunks one beyond it stay loaded so crossing a border back and forth doesn't reload. WorldLab's panel can override it.)
- [x] **M4** `Lab` Focus mode: select and highlight an individual chunk; show its stats. (`ChunkFocus` in WorldLab: click the terrain to focus the chunk hit; F frames it. Its edges are highlighted as part of the chunk-border grid, each grid edge drawn once so the highlight can't lose a depth fight with a coincident border line. The panel shows its coordinate, size, memory, edited state, vertex and triangle counts, build time, and exact and approximate solid volume. `WorldLabHud` shows the camera's position, voxel, chunk and voxel within it, and the brush's placed, dug and net volume.)
- [x] **M5** `Core` *(added)* Terrain edits that cross chunk borders update all affected chunks. (`World.ApplyBrush` applies the brush to every loaded chunk it reaches; the brush result depends only on a sample's value and position, so shared copies stay equal. `TerrainBrushTool` now edits any `IEditableTerrain`: a `ChunkView` or a `WorldView`. Edited chunks are kept in memory when unloaded until saving exists.)
- [x] **M12** `Lab` *(after 2G)* Storage at scale: repeat the K26 benchmark across many loaded chunks (total memory, load/unload time) and choose the `Core` storage for the game. Record the decision in `Docs/benchmarks.md`.
  - Result: `WorldStorageBenchmark` (*Clube → Benchmarks → Storage at scale*) on WorldLab's world at 16³ / 1 m and 32³ / 0.25 m. Decision: **single-byte storage** (`FlatByte`) for the game, now the `WorldConfig` default and WorldLab's setting: 0.25× memory at the same load, brush and streaming cost; run-length loses in memory at scale and the octree is second (0.38-0.58×, slower strokes). VoxelLab and ChunkLab keep exact floats. Generation dominates streaming (K35).
- [x] **M17** `Lab` *(backlog)* Chunk shape comparison: tall column chunks (Minecraft-style, full world height) vs cubic chunks stacked vertically to fill the elevation. Compare memory, mesh count and load time over the same terrain, and record the choice. Decide before M1 fixes the coordinate system. (`ChunkShapeBenchmark`, *Clube → Benchmarks → Chunk shape (M17)*; results in `benchmarks.md`. Stacked 32³ cubes against 32 x 640 x 32 columns on the same terrain: 7x less memory, 2-3.5x faster parallel generation, 6-8x faster meshing and 6x cheaper digs. A checkerboard shows the 16-bit index limit is 65,535 unique vertices: worst case 16³ unshared, 27³ shared; real terrain stays near 17k. Chunk size: 16³ is the plan of record, so every mesh fits 16-bit indices; against 32³ it stores less and digs cost half, but generation in parallel takes 3x and the same view distance 8x the chunks.)
  - Note: stacked cubic chunks allow tall peaks without paying for empty sky everywhere, which matters for mountain terrain and the mountaineering skill (SK5).
  - *(Decision for M1, 3A)* The coordinate system is 3D (`Vector3Int` chunk coordinates) with cubic chunks stacked in a fixed number of layers (`WorldHeightInChunks`), streamed horizontally by render distance. A column chunk is just a tall chunk size, so the comparison can still be run on this grid.
  - *(Decision, M17, 2026-10-08)* Keep stacked cubic chunks. Columns only win on sequential generation, which the streamer never does. Chunks are 16³ (plan of record), because not every GPU has 32-bit index buffers and 16³ meshes fit 16-bit for any terrain (`ChunkIndexBudget`, at most 61,440 vertices): the world labs use 4 m chunks in 40 layers at 4 voxels/m, and the voxels-per-metre picker keeps 16 per side. Per-chunk streaming overhead is the cost; P16 and P7 address it.

### 3A+ — Stacked labs *(added, after K32)*
Each lab contains the tools of the labs below it, built from the same scripts: `ChunkLab` has everything `VoxelLab` has for its selected voxel, and `WorldLab` has everything `ChunkLab` has for its focused chunk. Today most lab tools require a `ChunkView`, which world chunks aren't, so `WorldLab` has almost none of them, and the voxel tools exist twice (VoxelLab's and ChunkLab's own versions).

- [x] **M18** `Core + Lab` One lab chunk target. A Core interface for a displayed chunk (the chunk, its transform, mesh settings, mesh and renderer, rebuilt and replaced events, and an edit path), implemented by `ChunkView` and `ChunkRenderer`. A world chunk's edits go through `World.SetDensity`, so border copies stay equal (M2). A Debug `LabChunkTarget` gives lab tools their chunk: fixed in VoxelLab and ChunkLab, following `ChunkFocus` in WorldLab. The chunk tools (`ChunkDebugView`, `NormalLines`, `FlipFaces`, `StepThroughLab` and its follow and voxel link, `VoxelSelector`, `ChunkVolumeStats`, `ChunkStatsHud`) use it instead of requiring a `ChunkView`.
  - Result: `IRenderedChunk` (Core) and `LabChunkTarget` (Debug); `ChunkView.ChunkCreated` is now `ChunkChanged`. `ChunkStatsHud` stays a ChunkLab readout on the `ChunkView`. In WorldLab one click focuses the chunk and selects the voxel hit, and F frames the voxel. `ChunkVolumeStats` measures at most every 0.25 s while a brush keeps rebuilding.
- [x] **M19** `Lab` One lab voxel target: the selected voxel, fixed at (0,0,0) in VoxelLab. Merge `VoxelCornerEditor` with `SelectedVoxelEditor`, and `VoxelLabels` with `SelectedVoxelOverlay`, so corners, case stepping, presets, labels and the tetrahedra volume view (`VoxelVolumeLab`) work on the selected voxel in every lab.
  - Result: `SelectedVoxelEditor`, its Inspector, `SelectedVoxelOverlay` and `IVoxelCaseTarget` are gone. `VoxelCornerEditor` uses the selector above it, or keeps VoxelLab's stored corners when there isn't one. `VoxelLabels` can add densities to the corner labels and outline the voxel's triangles.
- [x] **M20** `Lab` One `LabPanel` replacing `VoxelLabPanel`, `ChunkLabPanel` and `WorldLabPanel`: a section appears when its component is in the scene (world, terrain, brush, meshing, chunk, voxel, step-through, camera, display), each drawn by a shared `*Controls` class. Finishes K31's open items (the selected voxel's controls in ChunkLab, voxel size, test fill).
  - Result: `WorldControls`, `ChunkShapeControls`, `TestFillControls`, `ChunkFocusControls`, `VoxelControls`, `VolumeControls` and `DisplayControls` join the existing ones. The key help is built from the parts present.
- [x] **M21** `Lab` Stack the scenes: "Voxel tools" and "Chunk tools" prefabs used by every lab that needs them. In WorldLab: focus a chunk, then select a voxel in it and edit its corners; step-through plays the focused chunk's build, hiding only that chunk's mesh. `ChunkFocus` reuses `ChunkVolume` rather than its own volume code.
  - Result: `Assets/Prefabs/Lab/Chunk Tools.prefab` with `Voxel Tools.prefab` nested in it. ChunkLab uses them as they are, WorldLab overrides the target to follow `ChunkFocus`, and VoxelLab removes the `VoxelSelector` and keeps its own settings as overrides. Voxel tools find their target and selector in a parent; `StepThroughMode` looks under the chunk tools.
- [x] **M22** `Lab` *(added, from playtesting)* The Inspector is a superset of the lab panel: every panel knob is also an Inspector knob, by the same code. (`LabPanelFrame.ForInspector` draws the shared `*Controls` in custom inspectors through `InspectorLabControls`, which records edits for Undo and saving. `LabPanelEditor` mirrors every panel section; the `ChunkView` and `WorldView` Inspectors draw the terrain, chunk and meshing sections above their config. The panel column has a fixed width, so changing text can't make it jitter.)
- [x] **M23** `Lab` *(added)* Build grid and small voxels, to pick the gameplay voxel size: the voxel size goes down to 1/16 m, with a voxels-per-metre picker (1, 2, 4, 8, 16) and chunk sides up to 64; a toggle shows a 1 m build grid on the terrain (cell size adjustable, optional height contours) *(contours removed 2026-10-05: the grid only drapes x and z lines over the terrain)* and how many marching voxels fit across a cell. (`BuildGridOverlay` sets globals for the `Clube/Build Grid Overlay` shader, an extra material on the lab chunks.)
- [x] **M24** `Lab` *(added)* WorldLab selects in two steps: a click focuses a chunk (its border highlighted), a click inside it selects a voxel; empty space clears the voxel, then the chunk. Separate toggles highlight the focused chunk and the selected voxel. The Spline generator's height curve is editable in the game panel (`HeightCurveControls`).

**Done when:** every VoxelLab tool works on a selected voxel in ChunkLab and WorldLab, every ChunkLab tool works on the focused chunk in WorldLab, corner edits across a chunk border leave no crack, and all three labs use the same panel code.

### 3B — Player
- [x] **M6** `Game` Basic character: walk, jump, collide with terrain (MeshCollider regenerated on edit). *(Done: `PlayerController` on a `CharacterController`, `PlayerSpawn` puts it on the ground once its chunk column loads and again after a fall; `ChunkCollider` re-bakes each chunk's collider after every rebuild, switched on by `WorldView`.)* *(2026-10-06: jumps keep their run-up; in the air Move only steers, at `airAcceleration` (4 m/s²), so there is no turning round mid-air.)*
- [x] **M7** `Lab` *(added)* Toggle between player camera and debug free-fly camera. *(Done: `PlayerCameraToggle`, P or the Camera panel section; the lab click tools switch off while walking. Debug drives the player as a plain GameObject, so it doesn't reference `Clube.Game`.)*
- [x] **M8** `Game` *(added)* In-game dig/place controls calling the A7 edit path; brush size as a player setting. *(Done: `PlayerDigTool`, left mouse digs, right mouse places (new `Place` action), 1/2 change `PlayerSettings.BrushRadius`; placing never fills the player's capsule. Left Alt frees the cursor for menus and the lab panel, pausing look and digging.)*
- [x] **M16** `Lab` *(backlog)* Gravity multiplier: debug slider scaling the player's gravity, for tuning jump and fall feel. *(Done: the panel's Player section (`PlayerControls`) scales `PlayerController.Gravity` in Play mode and sets the jump height, which stays the same at any gravity, with the gravity and jump airtime that result. The multiplier isn't saved; bake a good value into the player's gravity.)*
- [ ] **M25** `Lab` *(added 2026-10-05, after M6)* Fine marching voxels inside a coarse build grid: with a player about two cells tall, try 1, 2, 4, 8 and 16 marching voxels per 1 m build cell and judge how digging and moving feel. Builds on M23 (voxel size down to 1/16 m, the build grid overlay); M12 measured the cost (4 per metre: 6× the memory and 7× the generation of 1 m voxels). The result sets the game's voxel size. *(In progress 2026-10-05: picking voxels per metre now keeps the chunk and world size in metres (`ChunkSizing`), so the same terrain config gives the same landscape at every size, only sampled finer; the player has a brush preview sphere and the build grid has no height contours. 8 per metre led at first. At 8/m WorldLab uses 4 m chunks (32 voxels) and 8 layers; render distance 6 then loads 904 chunks, about 28 s in the Editor (generation, K35), and covers a quarter of the 1/m radius.) *(After K35 and P3: the same 904 chunks settle in 0.7 s with the streamer taking under 2 ms a frame; render distance 10, 2,536 chunks, in 1.9 s. Render distance counts chunks, though, and 10 chunks is only 40 m at 8/m: P7 (level of detail) and P16 (render distance in metres) are what make 8/m look far.)* *(2026-10-08: **4 per metre (0.25 m voxels) is now the leading choice**, 4 voxels across each 1 m build cell (the cell size is final); playtesting still has to confirm it. With 16³ chunks (M17) that is 4 m chunks, 40 layers for the 160 m world.)*
- [x] **M26** `Lab` *(added 2026-10-08)* Walls on the chunk's edges: a ChunkLab toggle that meshes the chunk's border samples as air (a curtain of empty corners), so marching cubes closes the ground with walls and the chunk looks like a slice cut out of the world, its underground on show. Only the mesh changes, not the data. *(Done 2026-10-08: `MeshSeal` (Core/Meshing) in `ChunkMeshSettings.Seal`; the managed mesher zeroes sealed samples in each layer it reads, the Burst job in its density copy, and the material pass treats them as air, so both paths match (`ChunkMeshJobTests.SealedChunk_MatchesManagedPath`). `WorldConfig.SealEdges`, the panel's "Walls on the chunk's edges".)*
- [x] **M27** `Lab` *(added 2026-10-08)* A fixed WorldLab: rename the streamed lab to `ProceduralWorldLab`, and add a new `WorldLab` that loads a fixed square of chunks (about 100 m by 100 m) once and streams nothing as the player walks. With M26's toggle its edge chunks draw walls down their outer faces, showing the cross-section of the ground. *(Done 2026-10-08: the old scene and config renamed with their GUIDs (`ProceduralWorldLab`, `ProceduralWorldLabWorldConfig`); the new `WorldLab` and `WorldLabWorldConfig` are copies with `FixedArea` (100 m, 13 x 13 chunk columns, 104 m) and `SealEdges` on. `WorldStreamer` loads `StreamingSettings.FixedColumns` whole, nearest the first focus first, never unloads, and seals each edge chunk's outside faces and the bottom (`StreamingArea.FixedSeal`); a uniform solid chunk with a sealed face is meshed for its wall.)*
- [x] **M28** `Lab` *(added 2026-10-08)* Settle the chunk size: **16 x 16 x 16**, final. Decided from M17's results: every 16³ mesh fits 16-bit indices for any terrain and materials (`ChunkIndexBudget`, at most 61,440 vertices), which every GPU supports, and digs are cheapest. The labs keep chunk size adjustable for testing; the gameplay demo scenes use 16³ (GW1, GW3).
- [x] **M29** `Lab` *(added 2026-10-08)* Settle the build grid cell: **1 m**, final. `WorldConfig.BuildCellSize` (piles, GL17; placement, GL23) is 1 m in both world labs; the labs keep it adjustable, the gameplay demo scenes use 1 m. Building on the grid or off it is still open (PK17).
  - Voxel size is the third of these and stays with M25: 0.25 m (4 voxels per build cell) leads, pending playtests.

**Checkpoint 3.1 — playable terrain (after 3A + 3B):** in WorldLab, the player can walk across a multi-chunk area and dig through a chunk border without seams, using a single placeholder material. *(Changed 2026-10-05: the player walks in WorldLab first; the lean `Game` scene, with no debug components (A10) and baked-in choices, is the last section of Chapter 4.)*

### 3C — Materials
- [x] **M9** `Core` Material registry: one definition per material (id, category aggregate/ore, texture, colour, hardness, drop item). Starts with aggregates **grass, dirt, stone** and ores **gold, silver, copper**; built so more of each can be added later without code changes, and so a material can later carry hidden mineral species (PR2). *(Done: `VoxelMaterial` assets (id, name, category, albedo, debug colour, hardness, drop item) in `Assets/Config/Materials/`, listed by `MaterialRegistry`, which also holds the texture array and the two render materials. Ids: stone 0, dirt 1, grass 2, copper 3, silver 4, gold 5.)*
- [x] **M10** `Core` Per-voxel material ID stored alongside density in voxel storage (A12). Aggregates assigned by depth below the surface: grass on top, a dirt band, stone below. *(Done: ids live per sample in `VoxelMaterials` beside the density storage rather than inside each 2G scheme; `Chunk.SetMaterial` and `World.SetMaterial` keep border copies in step. Generators now report depth below the surface, and `TerrainLayers` (in `TerrainSettings`) picks grass, dirt and stone from it. The brush gives new ground its material.)*
- [x] **M11** `Core` Terrain shader (URP — Shader Graph or hand-written URP HLSL) using a texture array indexed by material ID, so one material handles all six types. *(Done: hand-written `Clube/Terrain Materials`, with shadows; *Clube → Materials* builds the texture array and placeholder textures.)*
- [x] **M13** `Core` Blending where materials meet: marching cubes vertices sit on edges between voxels, so pass per-vertex material weights (e.g. vertex colours or UV channels) and blend in the shader instead of hard seams. *(Done: a material pass after either mesher gives each vertex the material of its edge's solid end (`VertexMaterialSampler`); each vertex carries its triangle's three ids (UV2) and weights (UV3).)*
- [x] **M14** `Core` *(added)* Triplanar mapping so steep faces don't stretch. *(Done, in the terrain shader.)*
- [x] **M15** `Core + Lab` Toggle between material display modes in `WorldLab` and `ChunkLab`, as an A6 mesh variant chosen once per chunk build:
  - **Hard seams:** each triangle takes one material (e.g. the dominant material among its voxel's corners), with vertices split along material borders so there's no bleeding.
  - **Blended:** per-vertex material weights blended in the shader (M13).
  - *(added)* **Debug colours:** flat colour per material ID, no textures, to check material assignment and ore placement at a glance.
  - Readout of vertex count per mode, since hard seams duplicate vertices along borders.
  *(Done: `MaterialDisplay` (None, Hard seams, Blended, Debug colours) in `WorldConfig`, picked in the panel's Meshing section; `HardSeamSplitter` and `BlendedSplitter` are the A6 strategies, and the HUD's vertex count is the readout. With flat shading no vertex is shared, so the counts only differ with smooth shading.)*

### 3D — Ore generation
Ore nodes are generated as a procedural centroid with a 3D Gaussian falloff. Each voxel near a node rolls against the falloff probability to decide whether ore replaces its aggregate material.

- [x] **O1** `Core` Ore definition per ore type (added to the M9 registry): nodes per region, depth range, peak probability at the centroid, spread (σ, optionally separate σx, σy, σz for flattened or elongated veins), host materials it may replace (e.g. stone only). *(Done 2026-10-05, with a change: the ore is a registry material (category Ore), but where it generates is an `OreSpec` in the world's `TerrainSettings` (`OreGeneration`), so labs tune it per world and in Play mode without editing the material asset (A2). Depth is metres below the surface; ores also have a priority (O5).)*
- [x] **O2** `Core` Centroid placement: divide the world into fixed-size cells, and derive each cell's node centroids from the world seed and cell coordinate. Deterministic, and independent of which chunks are loaded. *(Done: `OreField.NodesInCell`, hashed with `VoxelHash`; centroids must fall in the spec's depth range.)*
- [x] **O3** `Core` Cross-chunk nodes: when generating a chunk, check centroids in neighbouring cells within ~3σ so nodes near borders spill correctly into adjacent chunks. *(Done: `OreField.CollectNodes`.)*
- [x] **O4** `Core` Replacement roll: probability = peak × exp(−d² / 2σ²), compared against a hash of the voxel's world position and seed (not `Random`), so the same world always regenerates identically. *(Done: hashed on the global sample coordinate, so border copies agree.)* *(Changed 2026-10-05, K35: the chance is 0 beyond 3σ, the reach chunks gather nodes by, which also stops the roll scattering single ore voxels far from a node, and could disagree across a border; and ore only replaces samples below the surface. The roll (`OreRoll`) is shared by `OreField.Pick` and the generation jobs.)*
- [x] **O5** `Core` Overlap rule when two ore nodes reach the same voxel (e.g. highest probability wins, or fixed ore priority). *(Done: higher priority wins, then the higher probability.)*
- [x] **O6** `Core` Ore items: one item definition per ore and aggregate type (id, name, icon), linked from the material registry. Chapter 5 inventory and mining build on these. *(Done: `ItemDefinition` assets in `Assets/Config/Items/`, linked by `VoxelMaterial.Drop`; icons are the placeholder textures.)*
- [x] **O7** `Lab` Ore visualization: centroid markers, translucent spheres at 1σ/2σ/3σ, and an X-ray mode that hides aggregates to show ore voxels only. *(Done: `OreDebugView` in WorldLab: crosses and 1σ/2σ/3σ rings per node, and X-ray hides the terrain and draws each solid ore sample as a cube.)*
- [x] **O8** `Lab` Live ore parameter tuning in `WorldLab` with regenerate, plus a readout of ore voxel count per type per chunk. *(Done: the panel's Ores section (`OreControls`); counts are solid samples, from `MaterialCensus`, for the loaded world and the focused chunk.)*
- [x] **O9** `Core` Unit tests: same seed gives same centroids and ore voxels; probability falls off correctly with distance; border chunks agree. *(Done: `OreTests`.)*
- [ ] **O10** `Core` *(backlog)* Rarity from real crustal abundance: set each ore's O1 parameters (nodes per region, peak probability) so overall abundance loosely follows real composition rates in Earth's crust.
  - Note: the real spread is enormous (iron ~5% vs gold at parts per billion), so compress it on a log scale: keep the real ordering while keeping rare ores findable. O8's per-chunk counts verify the result.

**Checkpoint 3.2 — materials and ore (after 3C + 3D), completes Milestone 1:** the Checkpoint 3.1 scene now shows all six materials, with ore nodes spanning chunk borders consistently. Still no debug components (A10).

---

## Milestone 2 — First gameplay loop *(added 2026-10-06 — next after Checkpoint 3.2)*

Goal: play the first pass of the core loop end to end: **collect → refine → craft → sell**. Test case: mine copper ore with a pickaxe, smelt it in a furnace into a copper **bun** (the rough, dome-shaped ingot a smelt leaves), strike the bun on the anvil into a refined **ingot**, and sell the ingot at a merchant table for coins.

This milestone comes before the rest of Chapter 4 (P4–P16, the lean game world). Each GL objective is the thinnest version of an objective that already lives in Chapters 5–7 (named in brackets); the full version stays there and builds on this one. Keep every GL piece behind the same seams the full version needs (data in `Clube.Core`, player-facing parts in `Clube.Game`, A1), but don't build the full version early: no drag and drop, no supply and demand, no fuel tiers, no quality.

Where it runs: in WorldLab with the player (P), like Checkpoint 3.1; it moves into the lean `Game` scene with GW3 (see Open questions). The loop is `Clube.Game` code, so it must not depend on any lab component.

### LA — Tools and breaking
- [x] **GL1** `Core` Tool model *(first pass of TL1)*: a tool is an item (I1) with an impact shape given in sample corners around the aimed corner, and a power per material category (aggregate, ore, soft). Two tools: **hand** and **pickaxe**. *(Done 2026-10-06: `ToolDefinition` (an `ItemDefinition`) with a reach of `ImpactSize` voxels per side around the aimed voxel (hand 1, pickaxe 3x3x3; changed 2026-10-06 from corner shapes so the reach matches the cursor), power and hits per second; `Hand` and `Pickaxe` in `Assets/Config/Items/`. One power for every material for now; per-category power is TL4.)*
- [x] **GL2** `Game` Use a tool: the hotbar's tool (GL7) acts on the corners under the crosshair on Attack, with a placeholder swing (MF2 later). Edits go through the A7 path (`World`, so borders stay seamless, M2). In the loop this replaces `PlayerDigTool`'s sphere for mining; the sphere brush stays for the labs and for placing. *(Done: `PlayerToolUser` (Game) aims at the voxel hit and, at the tool's rate while Attack is held, hits every solid corner of the voxels it reaches; the tool comes from the hotbar (GL7). No swing visual yet. Edits go through `ToolStrike` on `World`.)*
- [x] **GL3** `Core` Breaking stages *(first pass of MF5)*: a hard sample is not removed outright. Pickaxe hits move it **solid → cracked → loose** (stone → cracked stone → loose stone; copper ore → cracked copper ore → loose copper ore), and the mesh updates at each stage. Hits per stage come from the material's hardness (M9) against the tool's power, so the hand barely dents stone. Simplest form first: cracked and loose are extra registry materials (ids, textures), so the mesher and material pass need no changes. *(Done: `VoxelMaterial.BreaksInto` chains the stages (cracked stone 6, loose stone 7, cracked copper 8, loose copper 9, with placeholder textures); `ToolStrike` adds the tool's power to every solid corner of the reached voxels, buried ones too, so it works in 3D (`StrikeDamage`) and breaks it at the material's hardness. Pickaxe: stone in 2 + 1 + 1 hits.)* *(Playtest 2026-10-06: the cracked and loose textures are painted at 512 px, 64 px per voxel at 4/m, with even, angular cracks and spaced rubble.)*
- [x] **GL4** `Game` Tool cursor *(first pass of TL3)*: a soft glow on the corners the tool will hit, replacing `PlayerBrushPreview`'s sphere while a tool is held. *(Done, changed 2026-10-06 from glowing corners: `ToolCursor` outlines the surface mesh's triangle edges inside the reached voxels (`SurfaceOutline`), one voxel for the hand and 3x3x3 for the pickaxe, warming from pale yellow to orange as damage builds. Cracked stone and copper textures have bold angular fractures.)* *(Changed again 2026-10-06: a small cross lies on the mesh at every triangle corner the targeted samples control (`SurfacePoints`: the vertices on each sample's edges to air, normals from the density gradient), instead of outlining the voxels.)*
- [x] **GL5** `Game` Collect loose material *(first pass of I5)*: hitting a loose sample with the pickaxe or the hand removes it and adds its drop item (O6) to the inventory. No world pickups yet (I2, MF3). *(Done with LB: loose samples' drops go through `PlayerToolUser.Collected` into the `PlayerInventory`.)*

### LB — Inventory
- [x] **GL6** `Core` Inventory model *(first pass of N1, with N4's tests)*: slots, stacks with a max size, add and remove, full-inventory handling. Holds material drops (O6), the hand and pickaxe, and non-material items: copper bun, copper ingot (I1's first non-material items). *(Done 2026-10-06: `Inventory` + `ItemStack` (Core/Items), `ItemDefinition.MaxStack` (64, tools 1); add tops up stacks then fills empty slots and returns what didn't fit, remove is all or nothing from the last stacks, move merges or swaps; `InventoryTests`. `Copper bun` and `Copper ingot` items exist.)*
- [x] **GL7** `Game` Hotbar and inventory screen *(first pass of N3, N2)*: number keys pick the hand or pickaxe; a simple screen lists the stacks and counts. *(Done: `PlayerInventory` (9 hotbar + 18 backpack slots, starts with the pickaxe) takes what tools collect; `Hotbar` (1-9, scroll wheel, Q) picks the held item, and with no tool selected the player uses the hand; `InventoryScreen` (Tab) shows every slot, click one then another to move. Brush size moved to [ and ]. `PickupNotice` says "Inventory full" for what didn't fit.)* *(2026-10-06: copper bun and copper ingot have painted icons (*Clube → Items → Generate icons*). Stacks: dirt, stone and grass 1024, ores 256. G drops the selected item back into the ground, see I4.)*

### LC — Refining and crafting
- [x] **GL8** `Core` Recipes and stations *(first pass of CR1)*: a recipe has inputs, outputs, the station that runs it, and a cost in time (furnace) or strikes (anvil). Two recipes: copper ore → copper bun (furnace), copper bun → copper ingot (anvil). *(Done 2026-10-06: `Recipe` assets (`Assets/Config/Recipes/`: station, inputs, output, work in seconds or strikes) and `CraftingStation` (Core/Crafting), which takes the inputs from an `Inventory`, works by `Tick` or `Strike`, and holds the output until taken; finished outputs of one item pile up. `CraftingStationTests`.)*
- [x] **GL9** `Game` Interaction *(first pass of I3)*: look at a station, a prompt shows ("E — use furnace"), and E opens it. *(Done: `PlayerInteractor` raycasts colliders within 3 m for an `IInteractable` and shows "E  Use furnace"; while something is targeted the tools leave the ground behind it alone. Interact is now a plain press.)*
- [x] **GL10** `Game` Furnace: load copper ore, smelt for a set time, take out a copper bun. Fuel is left out of the first pass (wood and charcoal are HF1); stations are placed by hand in the scene, not built (PK17). *(Done: a `StationObject` (placeholder box, `SnapToGround` drops it on the terrain near spawn) and the player's `StationPanel`: 8 copper ore smelt into 1 copper bun in 6 s, and the furnace keeps working with the panel closed.)* *(2026-10-06: low-poly furnace and anvil models, built by *Clube → Models → Build station models*.)*
- [x] **GL11** `Game` Anvil: strike the bun a set number of times (a hammer held or supplied by the anvil, CR2) to get a refined copper ingot. *(Done: the same panel on the anvil station: place a bun, press Strike 6 times (the anvil supplies the hammer), take the copper ingot.)*

### LD — Selling
- [x] **GL12** `Core` Currency and wallet *(first pass of TR3)*: one coin balance with earn and spend, unit tested (G3). *(Done 2026-10-07: `Wallet` (Core/Economy): earn, spend only what it has, change event; the player's `PlayerWallet` shows the coins above the hotbar.)*
- [x] **GL13** `Core` Price list *(first pass of TR1)*: a fixed price per item in one asset; supply, demand and town prices come later (TR1, TR2). *(Done: `PriceList` asset (`Assets/Config/Economy/Merchant prices.asset`): copper ore 1, copper bun 12, copper ingot 30; `PriceList.Sell` moves items from an inventory to a wallet. `EconomyTests`.)*
- [x] **GL14** `Game` Merchant table *(first pass of TR4)*: sell items from the inventory at the listed price; the wallet shows on the HUD. *(Done: `MerchantTable` (low-poly stall near spawn, an `IInteractable`) opens the player's `MerchantPanel`: what it buys, price, how many you have, Sell 1 and Sell all.)*

### LE — Loop playtest
- [x] **GL15** `Game` Loop setup: the player starts with the hand and a pickaxe near copper they can reach (a shallow copper `OreSpec`, or a node placed near the spawn), with a furnace, anvil and merchant table close by. *(Done 2026-10-07: WorldLab's spawn and stations moved to a flat valley 16 m from the origin, over a copper node 4.7 m down, found by scanning the seed's copper nodes; the fly camera starts there, so P drops the player in with the pickaxe. See `Docs/20261007 loop playtest.md`.)*
- [x] **GL16** Playtest and notes in `Docs/` *(first pass of EC6)*: time spent on each leg, what felt tedious or unclear, and what the next pass should change. *(Done: `Docs/20261007 loop playtest.md`. The loop runs end to end: 14 copper ore, a bun in 6 s, an ingot in 6 strikes, 36 coins. Main findings: the player gets stuck on lips in their own shaft, pillaring with dirt is the only way out, and each swing yields too many items. A hand playtest of the controls is still owed.)*

**Done when:** in one session the player mines copper ore with the pickaxe (seeing it crack, loosen and land in the inventory), smelts it into a bun, hammers the bun into an ingot and sells it for coins, with the inventory and wallet right at every step and no lab component needed.

### Second pass *(added 2026-10-07, after the first playtest)*
Changes asked for after `Docs/20261007 loop playtest.md`. The playtest's own findings (lips in shafts, getting out of holes, items per swing) stay open alongside these.

- [x] **GL17** `Core + Game` Piles by material *(first pass of SM2)*: dropped soft and medium material (dirt, grass) piles up as a cone at its angle of repose; hard material (stone, ore) stacks as a cube inside the build cell it lands in, filling the next cell over once that's full. The shape is a per-material setting. *(Done 2026-10-07: `VoxelMaterial.PileShape` (Cone or Block) and repose angle; `TerrainPile` places a cone (lowest score r + rise / tan(angle) first) or fills `BuildGrid` cubes level by level, always the cell lowest under a stepped cone (height in cells + distance in cells), so a full cell spills to its sides, then corners, then up. Stone, the ores and their cracked and loose forms are Block.)*
- [x] **GL18** `Game` Tool cursor, perimeter only *(refines GL4)*: a small right-angle marker at each of the 4 corners of the reached area's face on the surface, for the hand and the pickaxe alike, instead of a cross on every vertex. *(Done: `ToolCursor` takes the reach box's face across the axis the player looks along most, drops each corner and its two arm ends onto the surface along that axis, and draws the brackets; they warm with the most damaged sample in reach.)*
- [x] **GL19** `Core + Game` The furnace takes a stack *(refines GL10)*: stations get input slots; the furnace works through what's in them one recipe at a time and stacks the results in its output; the anvil does the same, a strike at a time. *(Done 2026-10-07: `CraftingStation` has 2 input slots and 1 output slot; time recipes start by themselves when their inputs are loaded and the result fits, strike recipes on the next strike. `Inventory.MoveTo` moves stacks between the player and a station.)*
- [x] **GL20** `Game` Slot screens *(first pass of N2 for stations)*: the furnace, anvil and merchant screens show item slots like the hotbar: the station's input and output slots, the player's inventory, and the merchant's prices on the slots it buys. *(Done: `InventoryGridGui` draws the player's slots the same in the inventory, station and merchant screens; station screens show input slots, the work (bar or Strike) and the output slot; the merchant shows prices in the corner of the stacks it buys; screens have an opaque backing.)*
- [x] **GL21** `Core + Config` Stone on steep slopes: where the surface is steeper than a set angle above a set height, the top layers are the base material (stone). Slope comes from the column heights, with a one-column border so chunks agree (M2). *(Done 2026-10-07: `TerrainLayers.SteepAngle` / `SteepMinHeight`; WorldLab: 35° above 24 m. Heightfield jobs take bordered column heights (`ChunkSampleGrid.BorderedColumns`) and pass a steep flag per column to `ChunkFillKernel`; 3D terrain has no steep rule yet. `TerrainSurfaceTests`.)*
- [x] **GL22** `Core + Config` Surface rocks *(first step of collecting)*: a Rock material scattered on grassy ground as clusters of 1–3 samples just above the surface, small bumps in the mesh, placed by a hash of the column so every chunk agrees; mined by hand into a Rock item. *(Done: `TerrainLayers.Rock` / `RockChance` (WorldLab 0.004 per column); Rock material id 10 drops a Rock item, one hand hit. `ChunkFillKernel.IsRockColumn` hashes the global column, so chunks agree.)*
- [x] **GL23** `Core + Game` Placeable stations *(first pass of PK17)*: the player starts with a furnace, an anvil and a merchant table as items. With one selected, build mode shows the build grid locally (the aimed cell lit, the grid fading out radially) and a preview snapped to the cell; B places it, holding B while looking at a placed one picks it back up. The build cell size moves into `WorldConfig` so Core and the game share it. *(Done: `WorldConfig.BuildCellSize` and `BuildGrid` (Core); `PlaceableDefinition` items with a prefab (`Assets/Prefabs/Stations/`); `PlayerBuilder` previews, places (`TryPlace`) and picks up (`TryPickUp`, which also returns what a station holds, via `PlacedObject`); `BuildGridDisplay` drives the build grid shader's focus fade and lit cell. The fixed loop stations are gone from WorldLab.)*

### Third pass *(added 2026-10-07, from `Docs/20261007b clube-backlog.md`)*
- [x] **GL24** `Core + Game` Tool reach as faces, pickaxe as a ball *(refines GL18, GL1)*: the cursor shades the surface mesh's triangles inside every voxel the next hit reaches, instead of corner brackets. The pickaxe reaches a ball of voxels inside a 4x4x4 cube: every voxel but the cube's edges and corners (32); the hand stays one voxel. *(Done 2026-10-07: `VoxelReach` (Core/Voxels) is a `VoxelBox`, whole or rounded; `ToolDefinition.ImpactShape` (Cube or Ball), the pickaxe a 4-voxel Ball; `ToolStrike.FindTargets` takes the solid corners of the voxels in the reach. `ToolCursor` polygonises each reached voxel from the world's densities with `MarchingCubes` and draws the triangles shaded and outlined.)*
- [x] **GL25** `Core + Game` Pick up small rocks *(first pass of I5 for loose pieces)*: a hit on a small connected cluster of a pick-up material (the surface rocks, GL22) takes the whole cluster at once, one item per sample, so stones on the grass are picked up without cracking them. The largest cluster size is a per-material setting. *(Done 2026-10-07: `VoxelMaterial.PickUpPieceSize` (Rock: 6); `ToolStrike.Hit` flood-fills the reached sample's solid samples of the same material, face to face, and when the piece is no bigger than that it removes it all and collects a drop per sample, spending the hit.)*
- [x] **GL26** `Core + Config` Terrain at a real scale *(first pass of P12; pulls ahead of Chapter 4)*: wide flat plains broken by ranges of hills and ~100 m mountains, from a few seeded noise fields (a low-frequency mask choosing plains or mountains, ridged noise for the peaks, gentle noise for the plains), with the world tall enough to hold them. Seeing a mountain from far away still needs P7 (LOD) and P16 (render distance in metres). *(Done 2026-10-07: `LandscapeHeight` (Generation/Fields) and the Landscape generator: a warped low-frequency mask picks plains or ranges (Mountain coverage), ridged fractal noise makes the peaks (Amplitude = their height, Mountain width = their size, Octaves = ridge detail) scaled by a broad massif field, softer hills in between and a few metres of plains noise. WorldLab: plains at 20 m, mountains 110 m, 250 m wide, 35% coverage, world 20 layers (160 m). Over 2 km the land is about half plains below 26 m with peaks near 120 m. At render distance 6 (48 m) only the near slopes stream in.)*

### Fourth pass *(added 2026-10-08, from `Docs/20261008b clube-backlog.md`)*
- [x] **GL27** `Core + Game` Pour to size *(refines GL7, GL17)*: holding Drop pours the selected material, faster the longer it's held, onto one pile that grows from where the pour began, so a cone's size follows the amount dropped and any part of a stack can be dropped; a tap still drops one. *(Done 2026-10-08: `TerrainPile.Pour` with a `PilePour` (first point, cone base, total); later batches score the cone from the first base and start from the pile's surface within the radius the total needs, so batches build the same cone shells one drop would. `ItemDropper`: a tap drops one, a hold pours from 8 items/s, doubling each second up to 256/s.)*
- [x] **GL28** `Game` Rotate while placing *(refines GL23)*: in build mode, R turns the preview a quarter turn, on top of facing the player. *(Done: `PlayerBuilder.Rotate`, the Rotate action on R; the hint names it.)*
- [x] **GL29** `Game` Keybind cheat sheet *(first pass of UI help)*: in player control, a small list of the keys in the lower left of the screen, read from the input bindings so it stays accurate. *(Done: `KeybindSheet` (Game/Hud) on the Player, rows of action names shown with their Keyboard&Mouse binding strings (or set text), plus scene lines such as the labs' P; Help (F1) hides it. Centre-screen texts now each have a row: interact prompt, build hint, notices.)*

### Fifth pass *(added 2026-10-08)*
- [x] **GL30** `Core` Trees *(first pass of PK7; builds trees from marching voxels, as MX5 asks to test)*: an algorithm that grows similar but slightly varied trees on flat ground: a trunk, several large branches, smaller branches off those, and clumps of leaves at the branch ends, written into the density data as wood and leaves so they mesh, collide and dig like the terrain. *(Done 2026-10-08: `TreeGeneration` in `TerrainSettings` (cell size, chance, max slope, height, trunk radius, branch count, thinnest branch, leaf size, wood and leaves); `TreeField` places at most one tree per 10 m cell from the seed, where the ground within 1 m is under 12°; `TreeShape` builds it as tapered capsules and leaf balls; `TreeStamp` writes the parts at the end of `ChunkFillKernel`, so streaming, the labs and benchmarks all get them, and chunks above the ground aren't skipped when a tree reaches them. Both world labs grow them at chance 0.35. Thinnest branch 0.22 m: thinner ones break into floating bits at 0.25 m voxels. `TreeTests`: same tree per cell, different between cells, every part within the gathered spread, no trees on a 27° slope, stamping independent of part order, chunk borders agree.)*
- [x] **GL31** `Core + Config` Wood and leaves *(first pass of PK7's wood)*: wood and leaves as terrain materials and items, with placeholder textures for the mesh and icons for the inventory. *(Done: `VoxelMaterial` Wood (id 11, hardness 2, drops Wood, piles in build cells) and Leaves (id 12, hardness 0.3, drops Leaves, cones at 55°), a new `Plant` category; items Wood (stacks of 256) and Leaves (1024); bark and leaf textures from *Clube → Materials → Generate placeholder textures*, and log and leaf icons from *Clube → Items → Generate icons*. Next pass candidates: an axe for wood (TL2), felling (a cut trunk dropping its tree), leaves that see through, prices at the merchant.)*

### Sixth pass *(added 2026-10-09, from `Docs/20261009 clube-backlog.md`)*
Volumes: one mined sample is 1 unit of volume; items carry a unit volume, and stations turn volumes into volumes, so yields and losses multiply through the chain.

- [x] **GL32** `Core + Config` Mixed ore *(first pass of PK1)*: a mined ore item holds contents by volume, e.g. copper ore that is 80% rock and 20% copper. The split comes from the grade of the node it spawned in, rolled per node from the seed within its spec's range, and can hold more than two contents (a spec lists extra contents, e.g. a trace of silver). Stacks of one ore blend their contents by volume. Separating ore from rock is PR7; seeing the contents depends on prospecting later (PS6). *(Done 2026-10-09: `Composition` (Core/Items) holds shares by volume; `ItemStack.Contents`, and `Inventory` blends them when a stack is topped up. `OreSpec.Grade` (range), `Gangue` (default: first host) and `ExtraContents`; each `OreNode` rolls its contents from the seed and its cell. `ToolStrike.Hit` reports where each drop came from (`MinedSample`), and `PlayerToolUser` asks `OreField.ContentsAt`, which follows the break chain (loose copper → copper) to the spec and takes the likeliest node, or the spec's typical mix where none reaches (ore poured back out). World labs: copper 12–30% with 0–3% silver, the rest stone. Slot tooltips show volume and contents. Ore poured back into the ground loses its node's grade (it comes back typical); saving contents is S3.)*
- [x] **GL33** `Core + Game` Yield and scale loss *(first pass of PR4)*: smelting turns the ore content of what's loaded into metal at a yield (copper: 50%), and hammering a bun into an ingot loses scale (20%), which the blacksmithing skill reduces. 1 volume of 80/20 copper ore → 0.2 copper → a 0.1 bun → a 0.08 ingot. Stations keep the part of a volume too small for a whole item until more comes in. Skill levels are a placeholder on the player until SK1. *(Done: `ItemDefinition.UnitVolume`; a `Recipe` by volume takes its `Content`'s share of the inputs' volume times its `Yield`, which a `CraftSkill` lerps to `skilledYield`; `CraftingStation.Held` keeps what's short of a whole item. Smelt copper bun: 1 ore, 50% of its copper, 0.75 s; a bun is 0.5, an ingot 0.4 (hammering keeps 80%, 95% at full blacksmithing). `PlayerSkills` (Game) holds the blacksmithing level, set in the Inspector. The furnace screen shows the held volume. `CraftingStationTests` runs the 1 → 0.2 → 0.1 → 0.08 example.)*
- [x] **GL34** `Core + Config + Game` Clay and bricks *(first pass of X2 for clay, HF2's bricks)*: clay deposits at the surface, flush with the terrain and reaching a little way down; clay piles in build cells like rock. Clay dropped on the ground dries into brick after 2 in-game days, or is fired into brick in the furnace. *(Done: Clay (id 13, hardness 0.8, piles in build cells, `VoxelMaterial.DriesInto` Brick after 2 days) and Brick (id 14, hardness 2.5) materials, items, textures and icons. `TerrainLayers.Clay` (cell size 24 m, chance 0.3, radius 3 m, depth 0.75 m): `ChunkFillKernel.ClayDepthAt` gives a lens per cell and only the material changes. `GroundDrying` (Core) tracks samples a drop placed (`TerrainPile.LastFilled`, `ItemDropper.Piled`) and turns those still there; `GameClock` (Game, 20-minute days, a time scale for testing) and `DroppedGroundDrying` on the world labs' World. Fire brick: 1 clay → 1 brick in 4 s. Drying isn't saved yet (S2).)*
- [x] **GL35** `Core + Game` Furnace fuel *(first pass of HF1)*: the furnace burns fuel from a fuel slot. Wood burns long, leaves burn out fast; it pulls the next fuel item into its burn-down bar as long as there is something to work. *(Done: `ItemDefinition.BurnSeconds` (wood 20 s, leaves 2 s); a `CraftingStation` made with `burnsFuel` has a `Fuel` slot and works time recipes only while burning, lighting the next item when the last burns out and there's work; the fire burns down when idle. `CraftingStation.Load` sends fuel to the fuel slot unless a recipe takes it. The furnace prefab burns fuel; its screen has the fuel slot and an orange burn-down bar; picking a furnace up returns its fuel.)*

### Seventh pass *(added 2026-10-12, asked during play on 2026-10-12)*
- [x] **GL36** `Core + Config + Game` Stump worktop *(first pass of WS1)*: a felled tree leaves a stump, a much harder material that needs a later tool upgrade to remove, and the stump is the first crafting worktop: wood chopped from the tree crafts into sticks there. *(Done 2026-10-12: Stump material (id 15, hardness 100000, no drop) stamped as the trunk's foot, `TreeGeneration.StumpHeight` 0.75 m (3 voxels at 4/m); `VoxelMaterial.IsWorktop`; `StumpWorktops` (Game) turns the connected worktop samples under the crosshair into a `StationObject` (new `StationKind.Worktop`) that keeps its contents; Whittle sticks: 1 wood → 4 sticks in 2 strikes.)*
- [x] **GL37** `Core + Game` Tool durability *(first pass of CR5)*: the pickaxe breaks after a number of landed swings. *(Done: `ItemStack.Wear` follows a tool between slots, `Inventory.Wear` empties the slot at `ToolDefinition.MaxHits` (pickaxe 200, shovel 100, hand never); the crosshair shows hits left and slots a wear bar.)*
- [x] **GL38** `Core + Game` A merchant at the world origin that sells tools *(first pass of TR4's buying)*: one build square at chunk (0, 0) is levelled and a merchant table stands on it, selling a replacement pickaxe; prices can be per lot of units (1 coin per 1000 dirt). *(Done: `MerchantSite` levels the square through `World.SetDensity` once the column loads; `MerchantTable.Offers` (pickaxe and shovel, 100 coins); `ItemPrice.Units`.)*
- [x] **GL39** `Core + Game` Shovel, and reach by ground *(first pass of TL2, TL4)*: the shovel digs soft ground (grass, dirt, clay, leaves) with the pickaxe's wide reach and hits one voxel on stone and ore; the pickaxe is the reverse. Both have placeholder icons. *(Done: `VoxelMaterial.IsSoft`, `ToolDefinition.WideOn` (Anything, Soft, Hard) and `ToolStrike.AimedMaterial`; the Shovel item; icons painted by *Clube → Items → Generate icons*.)*
- [x] **GL40** `Core + Game` Selling by the item *(refines GL14)*: Sell 1 or the stack, each showing its payout; prices can be by volume (copper ingot 1 coin per 4 volume), parts of a coin add up in the wallet, and the merchant buys worn tools for less (50 coins new). *(Done: `PriceList.PriceOf`, `ItemPrice.ByVolume`, `Wallet.EarnFraction`; the lab panel's Player section has a dev button for 100 coins.)*
- [x] **GL41** `Core + Config` Two copper ores *(first pass of PR2)*: chalcopyrite (brass-yellow) and malachite (brass-green) replace copper ore; malachite has twice the copper by volume; both smelt into the copper bun. *(Done: chalcopyrite is the old copper family renamed (ids 3, 8, 9), malachite ids 16–18; grade 12–30% vs 24–60%; one node per cell each; both smelt recipes in the furnace. Species are named from the start; hiding them until alchemy (PR2) is still open.)*

Also in this pass, without IDs: render distance works at run time in both world labs (up to 64 chunks; the fixed WorldLab streams within its square), coyote time and hold-to-repeat jumping, surface rocks are stone with about one in ten a boulder too big to lift, holding B picks up a placed station even with a placeable in hand, and the generated material texture array is no longer tracked in git.

### Eighth pass *(added 2026-10-12, from `Docs/20261012 clube-backlog.md` — next)*
- [x] **GL42** `Core + Game` Pay to unlock sections of the world *(first pass of EC7)*: the world is divided into sections of chunks; the player starts in one and pays coins to unlock a neighbouring section for digging and building. *(Done 2026-10-12: `LandParcels` (Core/Economy): parcels 8 × 8 chunk columns (32 m at 4 m chunks), numbered over x and z from the origin; a parcel sharing a side with owned land can be bought at 100 coins, then 100 more for each one after. `PlayerParcels` (Game) grants the parcel the player first lands in; looking at the ground of a buyable parcel, E buys it ("Buy this land: 100 coins"). The pickaxe and shovel skip samples on locked land (they dig right up to the border), and the brush, drops and building are refused there. `ParcelBorders` draws a translucent wall, fading upwards, around the owned land. Locked land still streams in and is visible; ownership isn't saved yet (S2). `LandParcelsTests`.)*
- [x] **GL44** `Core + Config` Each terrain generator keeps its own shape *(fixes K9's shared parameters)*: switching generators no longer carries Landscape's 110 m amplitude over to the others. *(Done 2026-10-12: `TerrainSettings` stores surface level, amplitude, frequency and octaves per generator and swaps them on a change (the `Generator` setter, or an Inspector edit through `WorldConfig.OnValidate`); the world labs' other generators start from their pre-Landscape values (ground at 28 m, ±13 m). `TerrainSettingsTests`.)*
- [x] **GL45** `Core + Config` A stocked start parcel: the land the player starts on has at least 2 trees and a clay deposit, added by world generation where the seed gave none. *(Done 2026-10-13: `StartArea` in `TerrainSettings` (the square, minimum trees, clay); `TreeField` grows the missing trees on the area's flattest spots, `LayerTable.From(layers, start, seed)` adds a clay lens in its middle when no deposit reaches in. The world labs' start area is parcel (-1, 0), where P drops the player; with this seed it already had clay and gets 2 trees. `StartAreaTests`.)*
- [x] **GL46** `Config` Prices by volume for raw materials *(refines GL40)*: 1 coin for 10 volume of grass, dirt or leaves; 1 coin per volume of clay, wood or stone; 2 per volume of chalcopyrite or malachite; 20 for silver ore, 200 for gold ore. Stumps drop wood. *(Done 2026-10-13: `Merchant prices` entries by volume; the Stump material's drop.)*
- [x] **GL43** `Lab + Game` Cheat commands: give the player any amount of coins and of any item, from a command line or the lab panel. Kept out of the `Game` scene's release build (A10). *(Done 2026-10-12: the lab panel's Cheats section (`CheatControls`, in the Inspector too): +100, +1,000 and +10,000 coins, an item picker over every item with Give 1, 10, 100 or a stack, and a command line (`CheatCommands`: `coins 500`, `give chalcopyrite 300`, `help`). Items come from `ItemCatalog` (Core/Items, `Assets/Config/Items/ItemCatalog.asset`), which the editor keeps listing every item asset (`ItemCatalogBuilder`); saves can use it later (S3). Typing in the command line still moves the player, since the lab panel needs the cursor freed.)*

### Ninth pass *(added 2026-10-13, from `Docs/20261013 clube-backlog.md`)*
The beginning path: the player starts with bare hands and grows four skills, Crafting, Woodcutting, Mining and Smelting, which unlock better tools, stations and recipes. Hand → pointy stick → stone pick; campfire → furnace; tin → copper (once it can be seen) → bronze.

- [x] **GL47** `Core + Game` Skills and experience *(first pass of SK1)*: four skills with experience and levels. Breaking wood and leaves gives woodcutting xp; stone, ore and soil give mining xp; finishing a recipe gives the xp its recipe names (crafting at the stump, smelting at the campfire or furnace). Levels show on screen, with a note when one goes up. Done: `SkillSet` (Core/Skills; level n at 10 n (n + 1) xp), `PlayerSkills` (xp per mined sample, recipe xp through `CraftingStation.Completed`), `SkillsHud`; cheats `level` and `xp`.
- [x] **GL48** `Core + Game` Recipes unlocked by skill level *(first pass of CR1's required skill, T2)*: a recipe can need a level in a skill; until then it shows locked on the station screen and never starts. Done: `Recipe` unlock skill and level, checked against `CraftingStation.Learner`; locked rows greyed with "Needs Crafting 2".
- [x] **GL49** `Core + Config` Pointy stick: the first tool, whittled at the stump; it harvests better than the hand (wood, soil, and stone and ore slowly). Done: 1 stick at the stump; hit rules wood 2, soil 1, stone and ore 4.
- [x] **GL50** `Config` Stone pick: a stone on a stick, unlocked by crafting level; it gathers stone and ore better than the pointy stick. Done: Crafting 2; stone and ore 2 hits.
- [x] **GL51** `Core + Config` Tin: cassiterite ore, in shallow deposits, some at the surface; it smelts into a tin bun, which hammers into a tin ingot (first tin of X1, ML4). Done: material id 19, ore spec 0-6 m deep in stone, dirt and grass; 40% yield at the campfire, 50% in the furnace.
- [x] **GL52** `Game + Config` Campfire: the first smelter, built from wood and stone at the stump and placed like the other stations; it burns fuel and smelts cassiterite into tin buns. Done: 4 wood + 6 stone, Crafting 1; `StationKind.Campfire`, stand-in model.
- [x] **GL53** `Config` Furnace by crafting: the furnace is no longer a starting item; it's built from stone at the stump once crafting is high enough, and smelts tin as well as copper. Done: 24 stone, Crafting 3; the world labs start with only the anvil and merchant table.
- [x] **GL54** `Game` Copper ore hidden until mining is high enough *(first pass of PS6)*: chalcopyrite and malachite look like the stone around them, and mined they give "? ore"; at the mining level that unlocks them they show, and the "? ore" the player holds turns into what it is. Done: Mining 3; `OreKnowledge` remaps the hidden layers in the terrain shader (`MaterialRegistry.ShowAs`), disguises drops and identifies held "? ore" by its composition.
- [x] **GL55** `Config` Bronze, the first alloy: a tin bun and copper buns make bronze buns, tin and copper ingots make bronze ingots, and a bronze bun hammers into a bronze ingot. Unlocked by smelting level. Done: 1 tin + 3 copper make 4 bronze, Smelting 3.

### Tenth pass *(added 2026-10-19, from `Docs/20261019 clube-backlog.md` — next)*
Fixes and comforts after playing the beginning path.

- [x] **GL56** `Game` Parcel fence *(refines GL42)*: the border of the player's land is a fence-high band that follows the ground, about a metre above the surface, and carries on down through the ground to the bottom of the world, so it shows in the walls of a hole dug next to it. Posts and a top rail make it read as a fence. *(Done 2026-10-19: `ParcelBorders` steps along each side every 0.5 m at the generated ground height (`TerrainSurface.HeightAt`; edits aren't followed) plus 0.75 m, a band from y = 0 to there, a top rail and posts every 2 m; rebuilt when the terrain is regenerated.)*
- [x] **GL57** `Game` Split a stack when selling *(first pass of N1's split)*: the merchant screen has a slider for how many of the picked stack to sell, with the payout, besides Sell 1 and Sell stack (sell 100 of 109 stone for exactly 100 coins). *(Done: a slider with - and + buttons and a Sell N button showing the payout; picking a stack sets it to the whole stack.)*
- [x] **GL58** `Game` Recipe preview: with a recipe picked (or the one that would start next), the empty output slot shows its result greyed out, so the player sees what they're about to make. *(Done: `CraftingStation.Upcoming`, drawn by `ItemSlotGui.Ghost`.)*
- [x] **GL59** `Game` Made tools go to the inventory: an item that doesn't stack (a tool, a station) shows in the output slot for a moment, then moves into the player's inventory when there's room. *(Done: after 0.6 s with the screen open, or when it closes.)*
- [x] **GL60** `Game` Leftovers come back: walking away from a worktop or anvil (or closing its screen) moves what's left in its input slots back into the player's inventory when there's room. Smelters keep theirs, since they work on with the screen closed. *(Done: `CraftingStation.ReturnInputs`; a station that `WorksUnattended` has a time recipe.)*
- [x] **GL61** `Core + Config` Fixed batches, no work in progress *(replaces GL33's held volumes, fixes held tin blocking a different ore at the campfire)*: every recipe takes a fixed count of inputs and makes a fixed count of outputs; nothing is held between runs. Smelting ratios come from the average ore grades (e.g. 5 chalcopyrite or 3 malachite make a copper bun), and hammering a bun makes one ingot (its smaller volume is the scale lost). The output slot holding a different item shows as the reason a recipe waits. *(Done: `Recipe` lost its by-volume fields and `CraftSkill`; campfire 4 cassiterite, furnace 3 cassiterite, 5 chalcopyrite or 3 malachite make a bun; smelting xp stays 3 per ore. Ore composition still identifies "? ore" (GL54) but no longer changes what a smelt makes.)*
- [x] **GL62** `Config` Rebalanced bun and ingot prices *(refines GL40, GL46)*: each step of refining is worth more than what went into it, so smelting and hammering pay (they were worth less than the ore). *(Done: per item, ore staying 2 per volume: tin bun 12, tin ingot 18, copper bun 14, copper ingot 20, bronze bun 18, bronze ingot 26. 5 chalcopyrite (10) → bun (14) → ingot (20).)*
- [x] **GL63** `Config` Bronze tools *(first pass of ML4's bronze tools, CR3)*: a bronze pick, hatchet and shovel, hammered at the anvil from bronze ingots and a stick, unlocked by crafting level; stronger and longer-lasting than stone, below the merchant's iron tools. *(Done: 2 bronze ingots + 1 stick, 8 strikes, Crafting 4. Pick: 2x2x2 on hard ground, 1 hit on stone, ore and clay, 300 swings; hatchet: 2x2x2 on wood, 300; shovel: 3x3x3 on soft ground, 200. The merchant pays 70, 70 and 60.)*
- [ ] **GL64** `Game` A smaller inventory *(to be specified: "reduced, like what we added to the dev tracker for next steps")*.

### Eleventh pass *(added 2026-10-19, from `Docs/20261019b clube-backlog.md` — next)*
Looks, the inventory, and knowing the ores.

- [x] **GL65** `Shader` Break up the terrain's tiling: the material textures stop repeating visibly (each texture sampled twice at different scales and rotations, mixed by low-frequency noise, with a slight tint variation). *(Done: `SampleBroken` in `TerrainMaterials.shader`, `_Breakup` and `_TintVariation` on the material.)*
- [x] **GL66** `Scene` A dawn sky *(first look at PK4)*: a skybox with the sun low over the horizon, and the sun light matching it, in the world labs. *(Done: `Assets/Materials/DawnSky.mat` (Skybox/Procedural), the sun at 10° and warm, three-colour ambient and a light exponential fog; the world labs' cameras clear to the sky.)*
- [x] **GL67** `Core + Rendering` Low-poly grass *(first pass of PK7's plants)*: small 3D grass tufts stand on upward-facing grass ground near the player, scattered per chunk from the seed, and go when the grass is dug away. *(Done: `GrassScatter` (Core/Rendering) reads the meshes the pipeline built, about 6 tufts per m² within 28 m, drawn with `Graphics.RenderMeshInstanced` and `Clube/Grass` (wind sway, shrinking away at the edge); 0.24 ms a frame steady in the Editor.)*
- [x] **GL68** `Game` Instant pickup *(refines GL59)*: a made tool shows in the output slot for a single frame, then moves to the inventory.
- [x] **GL69** `Config` Malachite looks green: its item icon is its own, not chalcopyrite's. *(Done: it uses its own terrain texture, like the other ores.)*
- [x] **GL70** `Game` Ore readout: the item tooltip lists the name and count, the volume, then one line per component and its share.
- [x] **GL71** `Game` Moving items *(first pass of N2's drag and drop)*: in the inventory screen, clicking a backpack item moves it to the first open hotbar slot, and a hotbar item to the first open backpack slot; holding and dragging puts an item in a chosen slot, swapping with what's there, or stacking onto the same item up to a full stack and leaving the rest where it came from. *(Done: `Inventory.MoveInto` tops up the same item first, then the first empty slot; `InventoryScreen` drags past 6 px and drops with `Inventory.Move`.)*
- [x] **GL72** `Game + Config` Silver and gold hidden *(refines GL54)*: like the copper ores, silver and gold look like stone and drop as "? ore" until higher mining levels. *(Done: `OreKnowledge` holds groups of hidden ore, each with a level: copper ore 3, silver 5, gold 7.)*
- [x] **GL73** `Core + Game` Mining journal *(ties to PS6)*: Q opens a field journal on graph paper, three ores to a page. An ore's entry is blank until it's discovered (mined while known); then it shows a hand sketch (black ink outline, speckles in the ore's colour) and notes: the depth it was first found at, its grade, hardness, luster, colour, feel, and its crafting uses once those recipes have been made. *(Done: `MiningJournal` (Core/Skills), `PlayerJournal`, `JournalScreen`, `OreSketch`, and `MineralNotes` on each ore's `VoxelMaterial`. Next slot moved from Q to X.)*
- [x] **GL74** `Lab` Why the Ores dropdown in the lab panel opens slowly (asked 2026-10-19): find out and fix it if it's cheap. *(Done: opening the section counted every sample of all 25,000 loaded chunks in one frame, 630 ms; `OreDebugView` now counts 2 ms of chunks a frame with running totals.)*
- [x] **GL75** `Config` *(added 2026-10-19, after the playtest)* "? ore" looks like its host rock: the stone texture, filled to the edges like the other ore icons, with a large question mark. *(Done: `ItemIconTools.PaintUnknownOre`.)*
- [x] **GL76** `Core + Game` *(added 2026-10-19)* "? ore" hides what it holds: its tooltip says "Contents unknown" instead of listing stone and the ore by share. *(Done: `ItemDefinition.HidesContents`, set on Unknown ore.)*
- [x] **GL77** `Game` *(added 2026-10-19)* Unknown ores in the journal: mining an ore before the player can tell it apart writes its entry as "Unknown ore", with an uncoloured sketch and only the depth it was first found at; the rest fills in once it's known, keeping that depth. *(Done: `PlayerJournal` records every find, `JournalScreen` shows it by `OreKnowledge`, `OreSketch.For(ore, coloured)`.)*

### Twelfth pass *(added 2026-10-19, from `Docs/20261019c clube-backlog.md` — next)*
Keys, cleaner digging, crafting fixes, building rules, and a campfire made of the ground.

- [x] **GL78** `Lab + Game` F3 hides the lab's FPS and position box; the key sheet lists F2 (lab panel) and F3 in the labs.
- [x] **GL79** `Core + Game` No floating slivers *(refines GL25's pieces)*: after a hit, any small piece of solid ground left with no connection to the rest near the hit (a few samples floating in the air) is removed too and collected. *(Done: `ToolStrike` checks the 26 samples around each one a hit removes; a solid piece of 3 or fewer joined to nothing else is removed and collected.)*
- [x] **GL80** `Core + Game` Click a recipe to load it: picking a recipe at a station moves one batch of its ingredients from the player's inventory into the input slots; loading by hand still works. *(Done: `CraftingStation.LoadFor`, `Inventory.MoveItemTo`; every unlocked recipe row is a button.)*
- [x] **GL81** `Core` The best-matching recipe *(fixes ML1's default)*: with nothing picked, the recipe that uses the most of what's loaded starts (sticks and stone make a stone pick, not a pointy stick). *(Done: most different inputs, then most items; ties go to the first listed.)*
- [x] **GL82** `Config` Stone pick icon: a stick handle with a stone head coming off the right side only.
- [x] **GL83** `Game` Stations can't be placed on a stump (or any worktop).
- [x] **GL84** `Game` Building on slopes: the build grid patch and preview stay level, floating at the placement height, instead of running up a wall; on ground steeper than allowed the preview turns red and can't be placed. Flat ground works as before. *(Done: steep when the ground rises more than 0.5 m across the cell (`PlayerBuilder.maxRise`); `BuildGridDisplay.ShowFloating`.)*
- [x] **GL85** `Core + Game + Config` A campfire made of the ground *(first pass of PK17's built structures)*: placing a campfire stamps a ring of stone and crossed wood into the terrain, with a warm glow at its centre, and that arrangement works as the campfire. Mining any of its stone or wood breaks it back into plain ground. A campfire mined away yields less stone and wood than it cost. *(Done: `TerrainStructure` writes 6 stone and 3 wood samples (density 0.8, each settled onto the ground in its column), checks they still stand 4 times a second, and hands a broken station's contents to the player; the prefab keeps the old model only for the build preview, an ember bed and a point light. The recipe is now 4 wood + 8 stone.)*

### Thirteenth pass *(added 2026-10-19, from `Docs/20261019d clube-backlog.md` — next)*
The chisel and square voxels, and the first stone worktop.

- [x] **GL86** `Core` Square voxels in the terrain *(first pass of MX2)*: a sample can be marked square; a voxel whose eight corner samples are all square is a sharp cube filling that voxel, on the marching cubes' grid, while the rest of the chunk stays marching cubes. So chiselling digs in about half a voxel from the rounded surface *(changed 2026-10-20, `Docs/20261020 clube-backlog.md`; at first each square sample was a cube centred on it)*. Marching cubes treats square samples as air, except where they touch unchiselled solid ground, so the two surfaces meet. Square marks are kept per chunk only once something is chiselled, copied across chunk borders like materials, and cleared when the sample is dug out. *(Done: `Chunk` square marks (`IsSquare`, `SetSquare`, `World.SetSquare`), `ChunkMeshInput.Squares`, `SquareVoxels.AddCubes` in the Burst mesh job. The managed mesher (step-through) doesn't draw squares. Not yet: a chunk loaded next to an edited one doesn't take square marks on its border.)*
- [x] **GL87** `Core + Game + Config` Bronze chisel *(first pass of TL2's chisel; SK8 later)*: made at the anvil from a bronze ingot and a stick. A hit squares the solid samples it reaches (any material); a hit on samples already square cuts them away and collects them, so a rounded lump can be carved into blocks. *(Done: `ToolDefinition.Chisels`, `ChiselStrike`; the chisel reaches one voxel (up to 8 samples), 400 hits, 3 a second; Crafting 4, the merchant pays 35.)*
- [x] **GL88** `Core + Game + Config` The Flat Rock *(first pass of WS1's large stone)*: chiselling a flat, level top of at least 3 × 3 square samples onto a surface rock turns that top into Flat Rock, a worktop like the stump: the player uses it where it stands, to hammer metal more slowly than at the anvil and to work stone. *(Done: `FlatTop`, `FlatRockCarver`; Flat rock is material 20 (worktop); `StumpWorktops` kinds open it as `StationKind.FlatRock` with three hammer recipes at 12 strikes. The surface boulders are stone, so stone and rock are hosts.)*
- [x] **GL89** `Lab` Debug material chunk: a cheat that levels a square of ground at the player and pours a pile of every material item onto it, to see and test them side by side. *(Done: `DebugMaterialChunk`, a button in the panel's Cheats section: a 10 m pad a metre ahead, a pile of each of the 21 materials with a drop.)*
- [x] **GL90** `Game` One "? ore" per hidden ore *(added 2026-10-20, from `Docs/20261020 clube-backlog.md`)*: ore mined before it's known no longer shares one "? ore" stack; silver, gold, chalcopyrite and malachite mined unknown take four slots, and each turns into its own ore when its level is reached. *(Done: `ItemDefinition.Variant` makes a run-time copy of Unknown ore per real ore item; `OreKnowledge` remembers what each copy stands for.)*
- [x] **GL91** `Game` Unknown ore in the journal *(added 2026-10-20, from `Docs/20261020b clube-backlog.md`)*: an ore found before it can be told apart is an entry with only the outline of its sketch, the name "Unknown Ore" and a note saying which Mining level discovers it. *(Done: `OreSketch.For(ore, known: false)` draws the outline alone; `OreKnowledge.LevelToKnow`, `PlayerJournal.HowToKnow`.)*
- [x] **GL92** `Core + Game` The chisel carves voxels *(added 2026-10-20, from `Docs/20261020c clube-backlog.md`; replaces GL86's square samples and GL87's square-then-cut)*: each hit carves the one voxel the player looks at out of the ground, on the voxel grid: its piece of surface goes, and the ground round it shows flat walls towards it, cut to the shape of its own surface, so a lump is cut into blocks one voxel at a time. The chisel never adds ground. Ground put back into a carved voxel (a pile, a brush) fills it in again. *(Done: carve flags on each carved voxel's lowest and highest corner (`CarveFlags`, `Chunk.IsCarved`, `World.SetCarved`, copied with borders, including for chunks loaded later); `SurfaceExtraction` skips carved voxels and `CarvedVoxels.AddWalls` adds the walls (marching squares on the shared face) in the Burst job; `SurfaceRaycast` hits the walls; samples with carved voxels all round become air. One drop per voxel, of the material most of its solid corners hold. The Flat Rock (GL88) is now a 3 × 3 level patch of carved voxels floored with stone or rock. The managed mesher (step-through) ignores carving.)*
- [x] **GL93** `Lab` A chisel icon that reads as one *(added 2026-10-20)*: a chunky handle and a broad bronze blade flaring to a bright edge. *(Done: `ItemIconTools.PaintChisel`.)*
- [x] **GL94** `Game` The chisel's cursor *(added 2026-10-20)*: instead of shading the ground it reaches, the chisel shows the voxel it will carve as a faint cube on the voxel grid with its edges traced, see-through. *(Done: `ToolCursor.AddVoxelBox`, faces at 5% and edges at half the cursor colour.)*

### Fourteenth pass *(added 2026-10-20, from `Docs/20261020d clube-backlog.md` — next)*
Collecting mechanic testing: ways to show and to take what a hit reaches, switchable in play to compare them.

- [x] **GL95** `Game + Lab` Cursor styles to test: a small dot only; a dot and a black outline of the reached voxels; today's shaded faces; soft glows at the reached corners; a translucent sphere showing the area of impact. Picked in the lab panel and the Inspector. *(Done: `ToolCursorStyle` on `ToolCursor` (the panel's Collecting section, `CollectingControls`); the dot replaces `PlayerDigTool`'s cross; the glows draw through the ground with a copy of the cursor material (`_ZTest` Always); the outline traces only voxels with a surface. The chisel keeps its voxel cube (GL94) in every style.)*
- [x] **GL96** `Core + Game + Lab` Collection modes to test: today's binary voxel grab over several hits; an area-of-impact sphere where each hit lowers every sample inside by the tool's impact amount (the hand 0.2, a tool 1), so interpolated edges show the ground giving way gradually, with an item for each whole unit taken; an area-of-impact sphere where each hit takes every solid sample inside at once, the hand's sphere smaller than a tool's. Picked in the lab panel and the Inspector, with the radii and amounts. *(Done: `SphereStrike` (Core: `FindTargets`, `Lower` with material owed between hits, `Take`); `CollectingSettings` on `PlayerToolUser` (defaults: hand 0.3 m and 0.2, tools 0.6 m and 1). The sphere modes skip hardness, break stages, loose-piece pick-up and slivers; the chisel keeps carving voxels.)*
*(2026-10-20, `Docs/20261020e clube-backlog.md`: going with the dot and the gradual sphere for now, the defaults; the other styles and modes stay switchable.)*

### Fifteenth pass *(added 2026-10-20, from `Docs/20261020e clube-backlog.md` — next)*
Furnaces from the ground, then a volume inventory test and what mixing material by volume means.

- [x] **GL97** `Config` Dropped stone looks like loose cobbles: the Loose stone material (what stone items become when dropped, and the stone furnace) gets a cobblestone texture. *(Done: `MaterialTextureTools.PaintCobbles`: rounded, lit stones of a few tints with soil between.)*
- [x] **GL98** `Game + Config` Furnaces built from the ground, like the campfire (GL85), shaped as primitive shaft furnaces (changed 2026-10-20 from the old model's 2.1 m block): the Stone furnace (cobbles) first, then the Clay furnace, hotter. Mining any of its samples breaks it. *(Done: *Clube → Models → Build furnaces* (`FurnaceStructures`, shape `LowPolyModels.FurnaceParts`: a round stack tapering from about 1.3 m across to an open flue about 1.75 m up, with a fire chamber and a low arched mouth, 91 samples whose densities shape the taper (`TerrainStructure.Part.Density`), built for 100 stone or clay); `TerrainStructure` now turns its parts with the placed object and settles each column on its ground; `StationObject.heat` → `CraftingStation.Heat` (time recipes go that many times faster on the same fuel; the clay furnace is ×1.5, unlocked at Crafting 5).)*
- [x] **GL99** `Game + Lab` Volume inventory test: a single tool slot and 4 material slots, where material slots are volume slots or item slots; to start, one volume slot that mixes every mined voxel together. Switchable, like the collecting test. *(Done, decided 2026-10-20: 1 volume slot and 3 item slots; spare tools go in item slots; it replaces the hotbar and backpack, which stay switchable (the panel's Collecting section, `PlayerInventory.UsesVolume`). Slot 1 is the tool slot (tools only, `Inventory.Accepts`): its tool is always in hand, and picking an item slot holding a tool swaps it in; slot 2 is the mix (`VolumeMix`, drawn by `MixSlotGui` as a bar of shares); 3-5 are items. Every mined sample goes into the mix as the material it was, with what it held (`PlayerToolUser.Unearthed`), hidden ore shown as "? ore".)*
- [x] **GL100** `Core + Game` Crafting from a mix: a quality multiplier from the share of the wanted material, e.g. a clay furnace from 76% clay, 15% dirt and 9% stone works at 76%. *(Done: picking a recipe at a station takes a cut of the whole mix for each material ingredient (`CraftingStation.LoadFromMix`), refused below a minimum share (50%, a panel knob); the loaded items carry the mix's contents. Something built (a placeable) keeps what it was made of (`PlacedObject.Contents`), and a station works as well as its own material's share (`MaterialRegistry.Quality`, `StationObject.MadeOf`: heat × quality).)*
- [x] **GL101** `Core + Game` Dropping from a mix: each sample put back into the ground is a material picked at random, weighted by the mix. *(Done: with the mix selected, Drop (G) pours samples whose materials are picked at random by the mix's shares (`VolumeMix.TakeOne`, `ItemDropper`), each put down as that material drops (mined stone as loose stone).)*
- [x] **GL102** `Lab` Hard material transitions: a terrain display that switches abruptly from one sample's material to the next, so it's clear what a hit will take. *(Done: `_HardMaterialEdges` in `Clube/Terrain Materials` takes only the strongest material at each point, so materials meet in a sharp edge halfway between samples; on by default, switched in the Collecting section (`ToolCursor.HardMaterialEdges`).)*
- [x] **GL103** `Core` Collecting from shared corners: a hit on one voxel lowers corners its neighbours share. Work out whether materials per sample (as now) settle it, or what else does. *(Answer, 2026-10-20: materials already are stored in the shared corners. Each sample carries one material and is a corner of up to 8 voxels; what a hit takes is samples (the gradual sphere lowers the samples inside it), not voxels, so there's no neighbouring voxel volume to leak into. The confusion comes from seeing blended voxels. With hard edges (GL102), each spot shows the material of the sample it's nearest, which is what a hit there lowers first. If it still feels off, the next step would be for the sphere to take the sample nearest the dot first, or alone with the hand.)*
- [ ] **GL104** `Core + Game` Low-grade ore in a mix: ore keeps its composition (90% stone, 10% cassiterite) inside the mix and when dropped again; smelting works it at a yield debuff. *(Part done: ore keeps its own composition inside the mix (`VolumeMix.Entry.Contents`), a station's inputs carry it, and dropping puts the ore back as ore. Not yet: the ground doesn't keep a dropped sample's grade (mined again, it's graded by the vein it's in), and smelting doesn't take a yield debuff from it yet.)*
- [ ] **GL105** `Core + Game` *(later)* Crude separation: crushing ore with rocks to split ore from host rock.
- [x] **GL106** `Game` *(added 2026-10-20, from `Docs/20261020f clube-backlog.md`)* The hand takes only the voxel under the dot: in the sphere modes a bare-hand hit lowers (or takes) just the corners of the voxel the player looks at, not a sphere. *(Done: `CollectingSettings.HandTakesAimedVoxel`, on by default, switchable in the Collecting section; `PlayerToolUser.AimHandVoxel`. Each hit still lowers by the hand's amount, 0.2.)*
- [x] **GL107** `Core + Game + Config` *(added 2026-10-20, planned)* Tools by material: a tool reaches a bigger area than the hand, and how much a hit lowers each sample there depends on the sample's material and the tool. A shovel takes dirt at 1 but stone at only 0.2; a pickaxe the other way round. First pass of the mixing fix: what a tool's made for comes away, the rest mostly stays. *(Done: in the gradual sphere a tool takes `CollectingSettings.ToolAmount` (1) from the kind of ground it reaches wide on (`ToolDefinition.ReachesWideOn`: the pickaxe rock, the shovel soil, the hatchet plants) and `ToolOffAmount` (0.2) from the rest, and what isn't the material under the dot is scaled by `OtherMaterialFactor` (0.5); `SphereStrike.Lower` takes an amount per material)*

### Sixteenth pass *(added 2026-10-20, from `Docs/20261020g clube-backlog.md` — next)*
Bins by kind, tools by material, and the mix usable everywhere.

- [x] **GL108** `Core` Grass keeps growing round the player wherever they walk (it stopped after a distance). *(Done: `GrassScatter` scanned every loaded renderer in load order within a 1 ms budget, so in a streamed world it ran out of time on old far chunks before reaching the new ones by the player; it now looks only at the chunk coordinates within its radius of the focus.)*
- [x] **GL109** `Core + Game` The bare hand picks up a loose surface stone whole: one click takes the stone sample under the dot and up to 2 neighbouring stone samples. *(Done: `HandPickup.TakeStone`: a pick-up material (stone, rock) with air above it, under the dot, comes away whole with up to 2 touching samples of it, nearest the dot first; otherwise the hand lowers the voxel as before)*
- [x] **GL110** `Core + Game` Crafting with mixed material: no minimum share; what's made from a mix works at its share (quality), so an impure mix still crafts, just worse. *(Done: the minimum share is 0 by default (still a knob); `CraftingStation.LoadFromMix` takes from the bin holding the most of each ingredient, and what's built keeps its quality)*
- [x] **GL111** `Game` The merchant buys mixed material: a bin sells for the value of what's in it, by volume. *(Done: `PriceList.ValueOf(VolumeMix)`: each material's volume at its item's price; the merchant panel has a Sell all row per bin)*
- [x] **GL112** `Core` No floating slivers in the sphere and hand modes either: a hit takes any tiny piece left hanging next to it (as GL79 does for voxel hits). *(Done: `ToolStrike.ClearSliversAround`, called after every sphere and hand hit with the samples it reached)*
- [x] **GL113** `Game` The inventory screen (Tab) shows the volume slots and what each holds, by share. *(Done: `InventoryScreen` draws each bin's slot and what it holds by share)*
- [x] **GL114** `Core + Game + Config` Bins by kind: the volume slot becomes three, Rock (stone, ores), Soil (dirt, grass, clay) and Plant (wood, leaves), each mined sample going to its kind's bin, so purity matters only within a kind (ore grade, clay purity). *(Done: `VoxelMaterial.Kind` (`MaterialKind`: plants by category, soil when soft, otherwise rock); `PlayerInventory.Bins`; the hotbar's cells are the tool slot, the three bins, then the item slots; Drop pours from the selected bin; `MixSlotGui` names each bin)*
- [x] **GL115** `Core + Game` *(added 2026-10-20, from `Docs/20261020h clube-backlog.md`)* Mixed material shows as mixed: loading a recipe from a bin takes a cut of the whole bin (60% wood, 40% leaves), not just the wanted material, and now says so. *(Done: `CraftingStation.LoadFromMix` reports what it took and how pure; `ItemSlotGui` shows a purity badge (e.g. 60%) on any material stack that isn't all its own material, ore grade included; `MaterialRegistry.IsMaterialItem`.)*
- [x] **GL116** `Config` *(added 2026-10-20)* No Rock material: surface rocks were already stone; Rock (id 10) and its item are gone from the registry, the item catalog and the Flat Rock hosts, and Flat rock drops stone. The bins keep their names. *(Done; the placeholder texture list keeps a "rock" entry so later entries keep their seeds.)*
- [x] **GL117** `Game` *(added 2026-10-20)* A spare tool works from an item slot: a tool in the selected slot is the one in hand and wears there (scrolling onto it doesn't swap it in), and when the tool slot's tool breaks the next of the same tool from the item slots takes its place. *(Done: `Hotbar.ToolSlot`, `Hotbar.WearSelected`.)*
- [x] **GL118** `Core + Game` *(added 2026-10-20, from `Docs/20261020i clube-backlog.md`)* Quality carries through crafting: material taken from a bin is as good as it is pure, whatever's made from it is as good as its inputs (weighted by count), and a tool lasts its quality's share of its hits, so sticks of 60% wood make a pointy stick that lasts 36 of 60 hits. *(Done: `ItemStack.Quality` (blended when stacks merge) and `ItemStack.MaxHits`; `CraftingStation` sets input quality from bins and output quality from inputs; `Inventory.Wear` breaks at the stack's hits; the slot shows a quality badge and a shorter wear bar; tool prices count hits left of a full tool's; built stations take the placed item's quality.)*

### Twenty-first pass *(added 2026-10-27, from `Docs/20261027 clube-backlog.md`)*
Playtest fixes: inventory drops, smaller hand and hand axe hits, the stump, quality text, the keybind sheet.

- [x] **GL153** `Game` Dragging a stack in the inventory drops it on the slot it's released over. *(Done 2026-10-27: a release in the gap between slots lands on the nearer one (`InventoryGridGui.PlayerSlotNear`), and a release over another slot counts as a drop even when no drag event came in between.)*
- [x] **GL154** `Game` The bare hand takes only the ground sample under the dot, 0.1 a hit. *(Done 2026-10-27: of the aimed voxel's corners, the solid one nearest the hit point; the voxel's other corners are still searched for a loose stone to lift whole.)*
- [x] **GL155** `Core + Game + Config` The hand axe takes only the ground sample under the dot, 0.2 a hit. *(Done 2026-10-27: `ToolDefinition.AimedOnly`; the hand axe's efficiency is 0.2.)*
- [x] **GL156** `Config` A stump can't be chopped with a hand axe from any distance. *(Done 2026-10-27: within 3 m the stump is a worktop and tools ignore it, but from 3-4 m away the hand axe (tier 1) could cut it; Stump needs tier 3 (a bronze hatchet) now.)*
- [ ] **GL157** `Rendering` Speckles of light in dug tunnels. *(Not reproduced 2026-10-27: the mesh has no holes (checked on noisy ground) and turning shadows off changes nothing. The buried stones (GL149) show as soft pale patches on tunnel walls; waiting to hear whether those are what was seen.)*
- [x] **GL158** `Game` Every stack shows its quality, 100% included, on the slot and in the tooltip. *(Done 2026-10-27.)*
- [x] **GL159** `Game` The keybind sheet lists J, the crafting journal. *(Done 2026-10-27: the row was in `KeybindSheet.DefaultRows` but the world lab scenes had saved their rows before it; their rows were reset.)*
- [x] **GL160** `Core + Config` *(added 2026-10-28, from `Docs/20261028 clube-backlog.md`)* Maples stand on maple stumps, not birch ones. *(Done 2026-10-28: Maple stump (id 23, maple bark, drops maple wood, a worktop like the stump), `TreeGeneration.MapleStump`.)*
- [x] **GL161** `Rendering` *(added 2026-10-28)* Grass goes with the ground it stood on when it's dug. *(Done 2026-10-28: a rebuilt chunk drops its tufts at once, and rebuilt chunks in reach grow again nearest the focus first; before, in a streamed world the newly loaded chunks took `GrassScatter`'s millisecond a frame and an edited chunk could keep its old tufts floating over the hole for a long time.)*
- [x] **GL162** `Core + Lab` *(added 2026-10-28, from `Docs/20261028b clube-backlog.md`)* Trees in the distance, not of voxels: past the world's chunks every tree the voxel world would grow is drawn as a low-poly mesh out to 3 km (`DistantTreeView`, `DistantTreeBuilder`, `DistantTreeMeshes`, `Clube/Distant Trees`). The same placement as the voxel trees (`TreeField.TryGetSpot`, `IsFlatEnough`; `TreeShape.HeightOf`, `IsMaple`), with heights from a job (`HeightfieldGenerator.SchedulePointHeights`, `PointHeightsJob`) per 128 m tile; birch and maple colours; a detailed mesh within 400 m, a plain one beyond; drawn instanced, and dropped in the vertex stage over the chunk columns the world draws itself (`ChunkCoverageMask`). The coarse terrain no longer stamps voxel trees. *(Done 2026-10-28.)*
- [x] **GL163** `Core` *(added 2026-10-28, from `Docs/20261028c clube-backlog.md`)* Grass came back slowly after leaving the spawn area and returning: `GrassScatter` regrew the chunks round the focus one at a time from a corner of its box (bottom layer first), so at a low frame rate the ground by the player waited about a minute, and an edit (a picked-up stone) jumped its chunk to the front. Chunks now regrow nearest the focus first (offsets sorted once per radius). The low frame rate itself was P17. *(Done 2026-10-28.)*
- [x] **GL164** `Core + Config` *(added 2026-10-28, from `Docs/20261028c clube-backlog.md`)* Forests and open fields: `TreeForests` splits the land with a smooth two-octave value noise about `TreeGeneration.ForestScale` wide (300 m in the world labs) into forest, where a cell has `Chance` of a tree (0.75), and open field (`FieldChance`, 0.03), with soft edges; `ForestCover` is roughly the forest's share (0.45, so about as many trees as the old even 0.35). The voxel trees and the distant ones read the same rolls. Defaults (cover 1) keep the old even spread. *(Done 2026-10-28.)*
- [x] **GL165** `Core + Config` *(added 2026-10-28, from `Docs/20261028c clube-backlog.md`)* Tree ages: every tree has an age hashed per cell that scales its whole shape (height, trunk, leaf balls and their spread; `TreeForests.SizeOf`, used by `TreeShape.Build` and `HeightOf`) from `YoungestSize` (0.45 in the world labs) to full-grown. Deep forest leans old; at its edges and in the fields young trees are as common as old. *(Done 2026-10-28.)*
- [x] **GL166** `Core + Config` *(added 2026-10-28, from `Docs/20261028d clube-backlog.md`)* Taller trees, each kind its own size: full-grown birch 11-16 m with a 0.25 m trunk radius (0.5 m across), maple 14-20 m with 0.4 m (`TreeGeneration.Height`/`TrunkRadius` for birch, `MapleHeight`/`MapleTrunkRadius`; `HeightFor`, `TrunkRadiusFor`, `TallestHeight`), about 70% of real birches and maples; ages still scale both (GL165), so trees run from about 5 m to 20 m. Leaf balls and their scatter grow with the tree's height (`TreeShape.LeafReferenceHeight`, 8.5 m), so every tree keeps the old proportions and the voxel crowns still match the distant ones; crown shapes per kind were left out on purpose. The distant trees take each kind's trunk against its height. *(Done 2026-10-28.)*
- [x] **GL167** `Core` *(added 2026-10-28, from `Docs/20261028e clube-backlog.md`)* Grass disappeared after switching between the player and the fly camera (P). Placing the player starts it at the world's top, and the fly camera is often high up, so the focus left the grass's reach and every tuft was dropped; it grew back a chunk or two a frame (about 30 tufts), which at 20 fps took a minute. Now `GrassScatter` keeps each chunk's tufts while the focus is within three radii of it across the ground, at any height, and only draws the chunks within the radius: back on the ground the grass is there in the first frame (12,400 tufts at once in the test, against 0-1,500 after 120 frames before). *(Done 2026-10-28.)*
- [x] **GL168** `Config` *(added 2026-10-28)* Maple leaves less red: a muted amber with a little red, in the terrain texture (`MaterialTextureTools`, *Clube → Materials*) and the distant trees (0.54, 0.34, 0.17). *(Done 2026-10-28.)*
- [x] **GL169** `Core + Game` *(added 2026-10-28)* The first clicks on grass or dirt gave nothing: a hand hit takes 0.1 of a sample, and what a hit gathers waited until a whole sample's worth had built up (ten hits). Now every hit's share goes to the inventory at once as a part item (`Gathering.TakeAll`, `GatheredStack.Part`), so the stack grows 0.1 at a time; the pickup notice shows when a whole item is made. A part added to a stack now blends its contents and quality in by amount (`Inventory.BlendPart`); before, a part kept the stack's old quality. *(Done 2026-10-28.)*
- [x] **GL170** `Config` *(added 2026-10-28)* The stone pick and stone hatchet are made from a stick and a hand axe (they took a stick and two stones); the stone hammer still takes a stick and three stones. *(Done 2026-10-28.)*
- [x] **GL171** `Core` *(added 2026-10-28)* Grass flickered when mining just under it, and jumped to new places when the mesh changed. A rebuilt chunk dropped its tufts and grew them again a frame or more later, from the 1 ms budget, and each tuft's place was hashed from its triangle's centre, which moves with every vertex. Now tufts stand on a fixed world grid (one per cell, about 0.4 m, jittered by a hash of the cell) at the height of the mesh under the cell's point, so a rebuilt mesh keeps every tuft whose ground is still there, and a rebuilt chunk with grass scatters again in the same frame. *(Done 2026-10-28.)*
- [x] **GL172** `Game` *(added 2026-10-28)* Materials didn't mix when gathered with the hand or the hand axe: since GL154 and GL155 they take only the one sample under the dot, so nothing came along with it. Now the aimed voxel's other ground corners of another material the tool can take give up, between them, up to `CollectingSettings.AimedMixFactor` (0.5) of what the hit took, by their share of the voxel's other ground corners: dirt dug next to grass comes up with grass in it, wood by leaves with leaves (`PlayerToolUser.MixInFromVoxel`; the lab panel's Collecting section has the slider). Tools in the sphere modes already mixed. *(Done 2026-10-28; not checked by hand.)*
- [x] **GL173** `Game` *(added 2026-10-28)* A trash slot on the inventory screen: a stack dragged onto it is destroyed. *(Done 2026-10-28: `InventoryScreen`, `Inventory.TakeAll`.)*
- [x] **GL174** `Core + Game` *(added 2026-10-28)* Sort on the inventory screen: the backpack's stacks that stack are merged, then put in order (tools, then things to build, then the rest, each by name, better quality and bigger stacks first), empty slots last; the hotbar is left as the player set it. *(Done 2026-10-28: `Inventory.Sort`.)*
- [x] **GL175** `Config` *(added 2026-10-28)* The merchant buys tools: it bought the bronze ones, the pickaxe and the shovel already; the hand axe (4), stone pick (12), stone hatchet (12), stone hammer (10) and spear (8) are now on its price list, worn tools for their hits left. *(Done 2026-10-28.)*
- [x] **GL176** `Core + Game` *(added 2026-10-28)* Part of a stack into a station: on a station's screen Shift-click loads half of a stack (rounded up) and Ctrl-click one item; a plain click still loads it all. *(Done 2026-10-28: `CraftingStation.Load(from, slot, count)`, `Inventory.MoveTo(slot, target, count)`.)*
- [x] **GL177** `Core + Game` *(added 2026-10-28)* Refining by hand at a boulder: holding Place (right click) on a boulder with a mixed stack selected (clay with dirt and grass in it) picks it over a pass at a time, 1 s plus 0.04 s per item. Each pass takes out 30% of what isn't the stack's own material (all of it once under half a percent is left), so the stack shrinks to what's left and its quality rises, and what came out goes back to the inventory as stacks of its own (dirt, grass); 0.5 Crafting experience a pass. Stone and crushable ore are bashed instead, as before. *(Done 2026-10-28: `Refining` (Core/Items), `BoulderBashing`, `Inventory.Replace`; not checked by hand.)*
- [x] **GL178** `Config + Game` *(added 2026-10-28)* Furnace tiers: a 100% clay furnace yielded the same as a 100% stone furnace (yield was the build quality times the ore's richness; heat only sped it up). Stations now have a recovery (`StationObject.Recovery`): campfire 50%, stone furnace 75%, clay furnace 100%, times the build quality, shown as the efficiency on the station screen; the campfire also works slower (heat 0.75, stone 1, clay 1.5). *(Done 2026-10-28.)*

### Twenty-fourth pass *(added 2026-10-30, from `Docs/20261030d clube-backlog.md` and `Docs/20261030e clube-backlog.md` — done)*
Demo polish after the first exe: defaults, a field log with quests, day and night, a brick furnace, and fixes. GL216 is
the thinnest first pass of 5J (achievements) and GL227 of PK4 (day/night).

- [x] **GL210** `Config + Game` *(added 2026-10-30)* Defaults: render distance 32 chunks, the fixed area 300 m, field of view 80°. *(Done 2026-10-30: `PlayerSettings`, `WorldLabWorldConfig`, `GraphicsSettings`.)*
- [x] **GL211** `Game` *(added 2026-10-30; P10)* The graphics popup has a render distance slider (8-64 chunks), kept between runs. *(Done 2026-10-30: `GraphicsSettings.RenderDistance`, applied to the `WorldView` by `GraphicsSettingsApplier` in a menu game or the exe.)*
- [x] **GL212** `Debug` *(added 2026-10-30)* The lab panel starts hidden (F2 shows it). *(Done 2026-10-30: `LabPanel.startOpen`.)*
- [x] **GL213** `Game` *(added 2026-10-30)* Place (right click) no longer adds a ball of dirt in play (the lab panel's brush does that); the key sheet lists the right mouse button as "Interact with boulder", and drops the brush size row. *(Done 2026-10-30: `PlayerDigTool.placesGround`, off; `KeybindSheet.DefaultRows`.)*
- [x] **GL214** `Game` *(added 2026-10-30)* The tool journal's Crafting chapter opens with the tree stump and the stone boulder, the two places to craft nobody builds. *(Done 2026-10-30: `CraftingJournalScreen.PlacesEntries`.)*
- [x] **GL215** `Game` *(added 2026-10-30)* One field log: Q opens the graph-paper notebook, with tabs on its left for the quests, the ore journal and the tool journal (J opens it on the tools). *(Done 2026-10-30: `FieldLogScreen`; `JournalScreen` and `CraftingJournalScreen` draw their pages into it; `JournalBook.Area` leaves room for the tabs.)*
- [x] **GL216** `Core + Game` *(added 2026-10-30; first pass of 5J)* Quests: a checklist in the field log, "Main Objectives" (pick up a stone; a hand axe on a boulder; wood; a campfire; the first ore; a bun; bronze; a flat rock) and "Side Quests" (sell to the merchant; 10k coins; another parcel; a clay furnace over 90%; a bun from crushed ore; gold; an ingot of each metal at once). The HUD shows the active one under the lab HUD box, as "Active Quest"; a notice says when one is done. Saved with the game. *(Done 2026-10-30: `QuestLog` (Core/Quests, tested by `QuestLogTests`), `PlayerQuests`, `QuestHud`, `HudLayout`; save format version 3.)*
- [x] **GL217** `Game` *(added 2026-10-30)* The player starts with no anvil or merchant table; the merchant's table stands inside the starting parcel. *(Done 2026-10-30: `PlayerInventory.startingItems` empty, `MerchantSite.cell` (-7, 13).)*
- [x] **GL218** `Config` *(added 2026-10-30)* The merchant buys campfires (10) and anvils (60). *(Done 2026-10-30: `Merchant prices`.)*
- [x] **GL219** `Game` *(added 2026-10-30)* C crouches (a lower capsule and head, half speed, stays down under a low ceiling); pressed while sprinting it slides about 5 m. *(Done 2026-10-30: `PlayerController` crouch and slide.)*
- [x] **GL220** `Game` *(added 2026-10-30)* Refining on a boulder takes 0.25 s per item's volume. *(Done 2026-10-30: `BoulderBashing.secondsPerVolume`.)*
- [x] **GL221** `Game` *(added 2026-10-30)* Crushing raw ore is press-and-hold like refining: an item every 0.25 s, with a progress bar. *(Done 2026-10-30: `BoulderBashing.UpdateHold`.)*
- [x] **GL222** `Game` *(added 2026-10-30)* The quality % in item slots was cut off by the label's padding. *(Done 2026-10-30: `ItemSlotGui` purity style.)*
- [x] **GL223** `Config` *(added 2026-10-30)* The flat rock makes the bronze pick, hatchet, shovel and chisel (twice the anvil's strikes). *(Done 2026-10-30: `Make bronze * (flat rock)` recipes in `StumpWorktops`.)*
- [x] **GL224** `Game` *(added 2026-10-30)* The station screens have the trash slot under your items. *(Done 2026-10-30: `InventoryGridGui.DrawTrash`/`Trash`, shared with `InventoryScreen`.)*
- [x] **GL225** `Config` *(added 2026-10-30)* Silver and gold smelt: every furnace has silver ore → silver bun and gold ore → gold bun, hammered into ingots at the anvil or the flat rock; buns and ingots sell (silver 90/110, gold 900/1100). *(Done 2026-10-30: items, recipes, icons; `Smelt silver ore`/`Smelt gold ore` extract the metal, typical grade 22%.)*
- [x] **GL226** `Config` *(added 2026-10-30)* The exe keeps running when another window has the focus: furnaces smelt on. *(Done 2026-10-30: Player Settings, Run In Background.)*
- [x] **GL227** `Game` *(added 2026-10-30; first pass of PK4)* Day and night: 20 minutes of daylight and 20 of night (an in-game day is 40 minutes, from 08:00 in a new game); the sun crosses the sky, warm and low at the ends of the day, a dim blue moon at night, and the ambient light, fog and sky darken. The graphics popup can freeze the sky at an hour (drying and smelting go on). *(Done 2026-10-30: `DayNightCycle` on World, `GameClock` day 2400 s, clay dries in 1 day (the same 40 minutes), `GraphicsSettings.FreezeDay`/`FrozenHour`.)*
- [x] **GL228** `Game` *(added 2026-10-30)* Clay drying on the ground steams, so it's plain it's still curing into brick. *(Done 2026-10-30: `DryingClayEffect`, `Drying steam.mat`.)*
- [x] **GL229** `Config + Debug` *(added 2026-10-30)* The brick furnace: built of 100 bricks at a stump (Crafting 6), heat ×2.25 against the clay furnace's 1.5. *(Done 2026-10-30: *Clube → Models → Build furnaces* makes it beside the clay one.)*
- [x] **GL230** `Game` *(added 2026-10-30, during the pass)* The load list deletes the selected save, after asking. *(Done 2026-10-30: `LoadGamePage`, `SaveSlots.Delete`.)*
- [x] **GL231** `Game` *(added 2026-10-30, from `Docs/20261030e clube-backlog.md`)* A stump can be used without clearing the wood off it: at this voxel size the trunk covers most of the stump, so E on the foot of the trunk finds the stump under it; tools still chop the trunk there. *(Done 2026-10-30: `StumpWorktops.Find(hit, out onTrunk)` looks up to 2.5 m under a wood hit; `PlayerInteractor.TargetIsGround` lets tools work.)*- [x] **GL232** `Game` *(added 2026-10-30)* The tool journal's stump and boulder entries have sketches of a stump and a boulder, not a log end and a stone texture. *(Done 2026-10-30: `PlaceSketch`, drawn in code in the journal's ink style.)*- [x] **GL233** `Game` *(added 2026-10-30)* "Gather your first ore" is done with any amount of ore, a part item included (0.56 of cassiterite gathered by hand didn't count: the quest waited for the mining journal). *(Done 2026-10-30: `PlayerQuests` also checks the inventory for ore stacks.)*

### Twenty-third pass *(added 2026-10-30, from `Docs/20261030 clube-backlog.md`, `Docs/20261030b clube-backlog.md` and `Docs/20261030c clube-backlog.md` — done)*
A standalone demo: a main menu, a new-game screen, and saving. GL205 is the thinnest first pass of 5C (S1-S4),
built early because the demo needs it, as Milestone 2 did for Chapters 5-7: one save slot, saved on the main thread.

- [x] **GL200** `Debug` *(added 2026-10-30)* The demo exe is the game: *Clube → Build → Game demo* builds the main menu and WorldLab (the World mode) to `Builds/Demo/`. *(Done 2026-10-30: `DemoBuild`, scenes `MainMenu` and `WorldLab`; the labs are no longer in the exe.)*
- [x] **GL201** `Game` *(added 2026-10-30)* The exe opens on a splash screen for the main menu, showing the build's version from git: the project version, the commit count and short hash, or the tag when the commit has one. *(Done 2026-10-30: `MainMenu` over a render of the valley (`Assets/Textures/Menu/MainMenuSplash.png`); `DemoBuild.Version` writes `Resources/BuildInfo.txt` for the build, read by `BuildInfo`.)*
- [x] **GL202** `Game` *(added 2026-10-30)* The main menu has New, Load (greyed out without a save; says when it was saved) and Exit. *(Done 2026-10-30: `MainMenu`; since GL208 Load opens the list of saves.)*
- [x] **GL203** `Game` *(added 2026-10-30)* New opens a new-game screen: the game mode (Voxel, Chunk, World, Procedural; only World can be picked, the rest greyed out as WIP) and the world seed, typed or rolled. *(Done 2026-10-30: `NewGamePage`; any text makes a seed (a number as it is, other text hashed). `GameSession` carries the mode and seed into the scene, and `GameSessionHost` (an `IWorldConfigOverride`) gives the seed to the world's play copy of its config before it starts. A menu game starts as the player.)*
- [x] **GL204** `Game` *(added 2026-10-30)* Esc in the game opens a small pause popup (the game stops under it): Back, Save and Exit, Exit Without Saving (back to the main menu). Esc closes an open screen first. *(Done 2026-10-30: `PauseMenu`; `Time.timeScale` is 0 under it.)*
- [x] **GL205** `Core + Game` *(added 2026-10-30; first pass of S1-S4)* Save and Exit writes the game to the local drive and Load brings it back: the seed and mode, the edited chunks (densities, materials, carves), the player's position and view, inventory, hotbar slot, coins, skills, journals, land, the clock, drying clay, and placed objects with what their stations hold. One slot, with a version number. *(Done 2026-10-30: `Core/Saving` (`GameSave`, `GameSaveFile`, `WorldSaving`, `StackSaving`, tested by `SaveTests`); in Game `GameSessionHost`, `PlayerSaveState`, `PlacedObjectSaving`, `ItemLookup`. Edited chunks go back with `World.Keep` before the world streams; structures keep their saved samples (`TerrainStructure.Restore`). Not saved yet: a station's work in progress and fuel burning, what is pending in a gather, the lab panel's settings.)*
- [x] **GL206** `Game` *(added 2026-10-30)* The new-game page names the world (a field beside the seed, "Hello World" by default); the name is saved with the game. *(Done 2026-10-30: `NewGamePage.WorldName`, `GameSession.WorldName`, `SaveHeader.WorldName` (save format version 2; version 1 saves still read).)*
- [x] **GL207** `Core + Game` *(added 2026-10-30; S4)* Save and Exit asks for a save name, filled in with the default `<world name> - <game mode> - <timestamp>`. Saves are kept side by side, one file each; saving under an existing name replaces that save. *(Done 2026-10-30: `PauseMenu` asks, `SaveNames` (Core) makes the default and the file name, `SaveSlots` keeps one file per save in `Saves/`.)*
- [x] **GL208** `Game` *(added 2026-10-30; S4)* Load opens a list of the saved games, newest first, the newest selected; pick one and press Load. *(Done 2026-10-30: `LoadGamePage`; Enter or a second click on the selected save loads it, arrows move the selection.)*
- [x] **GL209** `Game` *(added 2026-10-30; first pass of P10)* Esc → Graphics opens a smaller popup: field of view on a slider (60-120°, vertical) and the frame rate limit (30 FPS, 60 FPS, VSync, unlimited), kept between runs. The main menu never runs unlimited (it idled at 25% CPU drawing as fast as it could). *(Done 2026-10-30: `GraphicsSettings` (PlayerPrefs; VSync by default), `GraphicsSettingsApplier` (on World: the field of view on `Camera.main`, the frame rate; in the Editor only for a menu game), `GraphicsSettingsPage`; `MainMenu` applies the limit with unlimited as 60.)*

### Twenty-second pass *(added 2026-10-29, from `Docs/20261029 clube-backlog.md`)*
Playtest fixes and balance: journals, experience, refining, stacks and splitting, smelting yield, tools, coins.

- [x] **GL179** `Game` *(added 2026-10-29)* Looking at land more than a parcel from the player's own shows "You don't own this land" (no key: Interact does nothing there). *(Done 2026-10-29: `PlayerParcels.Find`, `INoticeOnly`.)*
- [x] **GL180** `Game` *(added 2026-10-29)* The stone and clay furnaces had no sketch in the crafting journal: their icons are renders on an opaque black background, so the outline (drawn where the icon's alpha changes) found no edge. `ItemSketch` now reads an icon with opaque corners by its colour against the background. *(Done 2026-10-29.)*
- [x] **GL181** `Game` *(added 2026-10-29)* The crafting journal follows the player's level: an unlocked recipe or furnace has its full sketch and notes (made or not); one a level away is only outlined, with its name and the level it unlocks at; anything further off is a big question mark and nothing else. *(Done 2026-10-29: `CraftingJournalScreen.LevelsAway`, `Entry.Mystery`.)*
- [x] **GL182** `Game` *(added 2026-10-29)* The crafting journal (J) has no Mining chapter: what each Mining level shows is in the mining journal (Q). Smelting is chapter two. *(Done 2026-10-29.)*
- [x] **GL183** `Game + Lab` *(added 2026-10-29)* Mining levels took a tenth of the work they should: picking up surface stones nearly reached Mining 3. Mining experience per sample is now stone 0.08, ore 0.16, soil 0.02 (was 1, 2, 0.25) and a crush 0.1 (was 1), so with GL184's curve a level takes about ten times the work. *(Done 2026-10-29; not checked by hand.)*
- [x] **GL184** `Core` *(added 2026-10-29)* Each level costs 50% more experience than the last: 20 for level 1, then 30, 45, 67.5 ... (totals 20, 50, 95, 162.5, 263.8; `SkillSet.ExperienceFor`). The old curve's steps grew by 20 a level. *(Done 2026-10-29.)*
- [x] **GL185** `Lab` *(added 2026-10-29)* The stone and clay furnaces' build preview is the surface their samples make once in the ground, meshed by the managed marching cubes (*Clube → Models → Build furnaces*), not a lump per sample. The menu also keeps the body-sized collider from GL143 now; it had reset it to the mouth. *(Done 2026-10-29.)*
- [x] **GL186** `Core + Config` *(added 2026-10-29)* Refining crushed cassiterite dropped it to 0% and lost volume: no material drops crushed ore, so none counted as its own, and a pass took the cassiterite out as an impurity (back as raw cassiterite). Items can now measure their purity as another item's (`ItemDefinition.PurityAs`; the crushed ores as their raw ore), which `MaterialRegistry.IsSourceOf` follows, so refining keeps the ore and takes out the rock. *(Done 2026-10-29.)*
- [x] **GL187** `Game` *(added 2026-10-29)* A discovered ore's notes in the mining journal sometimes came up blank until it was reopened. No error was logged; the likely cause is the dynamic font rebuilding its atlas when it first meets the journal's glyphs, leaving text already laid out blank. Both journals now put all their text's characters into the font before laying anything out (`JournalBook.Prepare`). *(Done 2026-10-29; the cause is a best guess, not reproduced.)*
- [x] **GL188** `Core` *(added 2026-10-29)* A mixed stone stack could reach 1024.7: a part item sat on top of a full stack. Part items now fill a stack to its `MaxStack` exactly and no further (1023.7 takes 0.3 more, to 1024; the rest goes to the next slot), and a whole item no longer goes on top of a part that would pass it. The tooltip shows how much of the stack each material is: "92% Stone (942.1)". *(Done 2026-10-29: `Inventory.RoomFor`, `WithPart`, `ItemStack.Space`.)*
- [x] **GL189** `Core` *(added 2026-10-29)* Leaves replaced wood up tree trunks: a sample took the part it lay deepest in, and a leaf ball's centre is deeper than a thin trunk's. Now a sample inside any wood segment is wood (the deepest wood part's), whatever leaves overlap it (`TreeStamp`, `TreePart.IsSegment`). *(Done 2026-10-29.)*
- [x] **GL190** `Game` *(added 2026-10-29)* Refining a big stack crawled (a pass was 1 s plus 0.04 s an item: 17 s for 400 clay). Every pass is now 1 s and takes out 1 item's worth of impurities (`BoulderBashing` refine seconds and volume), 0.1 Crafting experience a pass. *(Done 2026-10-29.)*
- [x] **GL191** `Core + Game` *(added 2026-10-29)* Splitting stacks: dragging with Shift carries half a stack and with Ctrl one, and a right click splits a stack in half into an empty slot, in the inventory and on station screens; a split dragged onto a station's input or fuel slot loads just that much (`Inventory.Move(from, to, count)`, `Inventory.Split`, `InventoryDrag`). *(Done 2026-10-29.)*
- [x] **GL192** `Core + Config` *(added 2026-10-29)* A good clay furnace yielded 118% from crushed cassiterite: yield was the efficiency times the ore's richness against a typical grade (up to 2x). A smelt's count is now what pure mineral gives, so the yield is the ore's mineral share times the efficiency and never passes 100%. Counts were raised so typical ore makes what it did (tin 6 → 5 buns, campfire 8 → 5, chalcopyrite 5 → 5, malachite 5 → 4). *(Done 2026-10-29.)*
- [x] **GL193** `Core + Config` *(added 2026-10-29)* Hatchets take 15% of a hit from leaves (`ToolDefinition.SoftFactor`, on soft material of the kind a tool is made for), so chopping wood brings up little leaf. *(Done 2026-10-29.)*
- [x] **GL194** `Game` *(added 2026-10-29)* With under one raw ore left, Place on a boulder switched from crushing to refining. Raw ore stays something to crush, and a bash crushes the part that's left (with its share of crushed stone). *(Done 2026-10-29.)*
- [x] **GL195** `Game` *(added 2026-10-29)* On a station's screen the player's stacks can be dragged between slots and swapped as in the inventory (`InventoryDrag`, shared by both screens). *(Done 2026-10-29.)*
- [x] **GL196** `Core + Config` *(added 2026-10-29)* Bronze tools beat stone: in the sphere modes every tool had the same reach. Tools now scale it (`ToolDefinition.ReachScale`): the bronze pick, hatchet and shovel reach 1.3x (about 2.2x the volume) and swing 3 times a second (stone 2). *(Done 2026-10-29.)*
- [x] **GL197** `Core` *(added 2026-10-29)* Two ores in a furnace's two input slots: a picked recipe kept the furnace waiting once its ore ran low, never starting the other. A smelter now goes on to whatever else is loaded when the picked recipe's inputs run out; worktops and anvils still wait for the picked recipe. *(Done 2026-10-29: `CraftingStation.Ready`, `Upcoming`.)*
- [x] **GL198** `Config` *(added 2026-10-29)* The merchant buys sticks: 1 coin for 2. *(Done 2026-10-29.)*
- [x] **GL199** `Core + Game` *(added 2026-10-29)* Coins are decimal, to the hundredth (`Wallet.Coins` is a float, `Wallet.Format`); Sell stack sells the part item too, so nothing is left behind. *(Done 2026-10-29.)*

### Twentieth pass *(added 2026-10-22, from `Docs/20261022 clube-backlog.md` — done)*
The tool path: hands, the hand axe, crushing ore; loose stones in the dirt; birch and maple.

- [x] **GL145** `Core + Config` Each material has a tool tier it needs (`VoxelMaterial.ToolTier`): dirt, grass, clay and leaves need none, so bare hands take them; wood, stone and ore need at least a hand axe. Hands still pick up loose stones (GL133). *(Done 2026-10-23.)*
- [x] **GL146** `Game` Bashing (right click) a stone on a boulder or rock face makes a Hand axe from 1 stone, for a little Crafting experience. The hand axe takes wood, stone and ore, slowly (`ToolDefinition.Efficiency`). *(Done 2026-10-23.)*
- [x] **GL147** `Config` The pointy stick is gone; the hand axe is the first tool. *(Done 2026-10-23.)*
- [x] **GL148** `Core + Game + Config` At Mining 2, bashing ore on a boulder crushes it: crushed ore with half the stone it held, at a better quality, and crushed stone as a byproduct (part items). Crushed ore sells for more and smelts with a better yield. *(Done 2026-10-23.)*
- [x] **GL149** `Core` Small stones (1 to 3 samples) lie in the dirt layer. Bare hands take a stone of 3 samples or fewer when everything around it is soil or air; a stone touching ore or rock stays. *(Done 2026-10-23.)*
- [x] **GL150** `Core + Config` Two kinds of tree: birch and maple, picked per tree. Maple wood (with its own leaves) sells for more. *(Done 2026-10-23.)*
- [x] **GL151** `Core + Config` Maple sticks are stronger: whittled at 140% quality, so tools made with them last longer. Quality can go above 100%. *(Done 2026-10-23.)*
- [x] **GL152** `Core + Game` Maple needs a bronze hatchet and Woodcutting 3 (`VoxelMaterial.ToolTier`, `RequiredLevel`); birch falls to a hand axe. A tool that can't take what it's aimed at says why. *(Done 2026-10-23.)*

### Nineteenth pass *(added 2026-10-21, from `Docs/20261021c clube-backlog.md` — done)*
Playtest fixes, decimal yields, the crafting journal.

- [x] **GL131** `Core` Crafted items of different quality don't stack (97% sticks stay apart from 98%). Gathered material, which carries its mix as contents, still blends into one stack (GL120). *(Done 2026-10-21.)*
- [x] **GL132** `Core` A gathered ore's quality is its share of the target ore itself (18% cassiterite), not the share of ore samples in it. *(Done 2026-10-21.)*
- [x] **GL133** `Core + Game` Bare hands never take hard materials: not by digging, not as slivers next to dug soil. They pick up only loose stones: a small surface rock whole, or from a pile of dropped stone or ore. *(Done 2026-10-21.)*
- [x] **GL134** `Config` A shovel recipe at the worktop. *(Done 2026-10-21.)*
- [x] **GL135** `Game` Station screens show "Efficiency xx%" at the top and what it works on (Heat and yield for the campfire and furnaces), not quality. *(Done 2026-10-21.)*
- [x] **GL136** `Core + Game` Yield comes out as a decimal amount of the item (0.48 tin bun), not as a part carried in the station. Part items add up when stacked; recipes use whole ones. *(Done 2026-10-21.)*
- [x] **GL137** `Game` A second journal, the crafting journal: chapter one Crafting (each recipe, 3 to a page, sketched like the ore journal), chapter two Mining (what ore each level lets you see), chapter three Smelting (the smelting recipes and their unlocks, and the kinds of furnace). *(Done 2026-10-21.)*
- [x] **GL138** `Game` A broken tool is not replaced from the inventory; the player swaps in a spare. *(Done 2026-10-21.)*
- [x] **GL139** `Config` The furnaces list the tin bun first, then copper; the bronze ingot alloy is off the furnace (ingots are hammered on the Flat Rock or the anvil). *(Done 2026-10-21.)*
- [x] **GL140** `Core` Picking a recipe loads a full stack of each input, not one batch. *(Done 2026-10-21.)*
- [x] **GL141** `Game` E works looking at a furnace's (or campfire's) body, not only near its base. *(Done 2026-10-21.)*
- [x] **GL142** `Core` A station with a picked recipe moves on to another recipe when that one's inputs are swapped in (chalcopyrite after cassiterite). *(Done 2026-10-21.)*
- [x] **GL143** `Config` A bronze chisel from a bronze bun and a stick at the worktop: the way to the Flat Rock without an anvil. *(Done 2026-10-21.)*
- [x] **GL144** `Config` Ingots sell for more than their buns at any Blacksmithing level. *(Done 2026-10-21.)*

### Eighteenth pass *(added 2026-10-21, from `Docs/20261021b clube-backlog.md` — done)*
Tools by material, smelting yield, blacksmithing.

- [x] **GL126** `Game` In the gradual sphere a tool lowers the kind it's made for by 1 and every other kind by 0.1 (both still halved off the material under the dot, GL107): the pick takes stone and ore, the shovel soil, the hatchet plants. Less soil in ore stacks. *(Done 2026-10-21.)*
- [x] **GL127** `Core + Game` A furnace's quality sets its speed (GL100) and its yield: a 50% furnace makes half as many buns from the same ore. Part outputs carry over to the next run. *(Done 2026-10-21.)*
- [x] **GL128** `Core + Config` Ore concentration sets smelting yield: a smelt recipe names the mineral it extracts and a typical grade; ore richer than that yields more buns, leaner ore fewer. Smelted metal comes out pure (quality 1): impurity costs yield, not quality. *(Done 2026-10-21.)*
- [x] **GL129** `Core + Game` A Blacksmithing skill: anvil and flat-rock recipes give Blacksmithing experience, and what they make is no better than the smith (50% at level 0 up to 100% at level 10), nor than what went in. Ingots and tools carry it as their quality. *(Done 2026-10-21.)*
- [x] **GL130** `Core` Quality sets sell prices: an item sells at its quality's share of the listed price (a tool already did, through its hits). *(Done 2026-10-21.)*

### Seventeenth pass *(added 2026-10-21, from `Docs/20261021 clube-backlog.md` — done)*
Sorted stacks instead of bins, hands for soft ground, grass as fuel.

- [x] **GL119** `Game` Bare hands take soft ground only (soil, plants) and pick up loose surface stones whole; a hit on hard ground otherwise does nothing. Tools stay best at their own kind (hatchet plants, shovel soil, pick rock). *(Done 2026-10-21.)*
- [x] **GL120** `Core + Game` Sorted stacks: what a hit gathers goes to a stack of the material under the dot, mixing in whatever else came with it (grass with a little dirt is a grass stack of that mix, at that purity); looking at dirt fills a dirt stack. Replaces the bins (GL114); purity still feeds quality (GL118). *(Done 2026-10-21.)*
- [x] **GL121** `Core` A pile of hard material (stone, ore) fills one build cell before starting the next. *(Done 2026-10-21.)*
- [x] **GL122** `Game` Items with no volume of their own (tools, sticks) don't show one. *(Done 2026-10-21.)*
- [x] **GL123** `Config` Grass is one voxel thick over dirt, and a fuel for the campfire and furnace, burning about as long as leaves. *(Done 2026-10-21.)*
- [x] **GL124** `Core` Mixed fuel burns worse: a fuel stack burns for its quality's share of its time (grass with dirt in it). *(Done 2026-10-21.)*
- [x] **GL125** `Game` The inventory is the classic hotbar and backpack again, any item in any slot; the tool-slot layout (GL99) stays switchable in the lab panel, off by default. *(Done 2026-10-21. The slots no longer show numbers, and a broken tool is replaced by a spare of the same tool in either layout.)*


---

## Chapter 4 — Procedurally generated chunks (`Game`) *(draft — to be expanded)*

*(2026-10-06)* The rest of this chapter comes after Milestone 2.

- [x] **P1** `Core` Chunks stream in/out around the player (load/unload radius from render distance). *(Done 2026-10-05: `WorldStreamer`, nearest first, one chunk of slack before unloading, the wanted area's offsets sorted once per render distance.)*
- [x] **P2** `Core` Chunk object pooling. *(Done 2026-10-05: `ChunkRendererPool` (renderers only for chunks with a surface, about 1 in 6 at 8/m) and `VoxelArrayPool` (density and material arrays come back when the world drops a chunk).)*
- [x] **P3** `Core` Background mesh generation (Jobs/Burst or threads); no frame hitches on load. *(Done 2026-10-05: `ChunkPipeline` runs generation, meshing (materials, normals and the vertex buffers, written into `Mesh.MeshData`) and collider cooking (`Physics.BakeMesh`) as jobs across frames; `WorldStreamer` takes their results within a main-thread budget (3 ms), meshes before new chunks; edits rebuild their mesh and collider in the same frame. WorldLab at 8/m was ~5 fps while loading; the streamer now averages 0.8-3 ms a frame. Remaining hitches are scheduling, listed in `Docs/architecture.md` §8.)*
- [ ] **P4** `Core` Deterministic world from seed.
- [ ] **P5** `Core` Caves via 3D noise.
- [ ] **P6** `Core` Persist player edits for unloaded chunks.
- [ ] **P7** `Core` Level of detail for distant chunks. *(Note 2026-10-05: needed for M25's 8 voxels/m, where a chunk is 4 m wide. The pipeline is ready for it: generation is a pure function of position and `ChunkSampleGrid` carries the voxel size, so a coarse chunk is the same jobs on bigger voxels, with no data kept and no collider. Open: seams between levels (skirts or Transvoxel) and how edits show in coarse chunks.)* *(2026-10-27: the far distance is P7a; what's left of P7 is the middle distance, coarse voxel chunks between the full chunks and the far terrain, where caves and edits would still show.)* *(2026-10-28: the middle distance is P7c, coarse voxel chunks; still open from P7: edits in the coarse levels, and true seam stitching (Transvoxel) in place of the per-pixel overlap.)*
  - [x] **P7a** `Core + Lab` *(added 2026-10-27, from `Docs/20261027b clube-backlog.md`)* Far terrain to 10-50 km: a geometry clipmap of the generator's heightfield (`FarTerrainView`, `FarTerrainBuilder`, `FarTerrainGrid`, `FarTerrainMeshJob`, `Clube/Far Terrain`), nine levels of 128 quads from 2 m to 512 m apart reaching 33 km by default (60 km at most). Each level is centred on a multiple of twice its spacing, so its edges sit on the next level's grid lines; odd edge vertices take their neighbours' mean, so the rings close (tested). The levels a move shifts are built as one batch in jobs (heights spread over the workers, `ColumnHeightRowsJob`) and swapped in the same frame: 0.2 ms on the main thread. The shader leaves out every chunk column the voxel world draws in full (`ChunkCoverageMask`), so caves and edits show and the far terrain fills in wherever chunks are still streaming; it colours by slope and height (grass, rock, snow) under the scene's fog, casts no shadows and sits 0.3 m low. It pushes the camera's far plane out. Heightfield generators only. *(Done 2026-10-27; open: the far terrain doesn't see edits, a thin line of sky can show under the edge of the voxel area where it meets the lowered far terrain, and the voxel world's top (160 m) still cuts off the great ranges up close.)*
  - [x] **P7b** `Core + Config` *(added 2026-10-27)* Great ranges for the horizon in the Landscape generator (`DistantRanges`: height, width, start): ridged mountains kilometres tall that rise only beyond a distance from the origin, so the land the player works keeps its 110 m mountains. The world labs use 1.5 km tall, 5 km wide, from 2.5 km; the scenes' fog is thinner (density 0.00003) so they show through haze at 30 km. *(Done 2026-10-27.)*
  - [x] **P7c** `Core + Lab` *(added 2026-10-28, from `Docs/20261028 clube-backlog.md`)* Coarse voxel terrain between the world's chunks and the far terrain, so the land steps down in detail instead of switching at a seam: eight levels (`TerrainLodView`, `TerrainLodBuilder`, `TerrainLodRing`), level l with voxels 2^l times the world's (0.5 m to 64 m), each a square of 16 × 16 of its own 16³ chunks round the focus (64 m to 8 km), less the four middle columns. Each level is a `ChunkPipeline` on its own `WorldGrid`, so it is the world's own generation and Burst meshing, and draws in the world's terrain material: the same textures (trees out there are meshes, GL162). Its fill widens the density ramp and every layer band by the level's scale (`ChunkFillSettings.LodScale`, `ForLod`), so coarse surfaces are exact and still grassy; rocks and buried stones are left out. Chunk layers come from each column's height range (`ChunkPipeline.TryGetColumnRange`), so the voxel world's 160 m top doesn't apply. A level swaps to its new square in one frame once all of it is built, and recentres every second of its chunks. Per pixel, every level leaves out the columns the world's chunks cover in full (`ChunkCoverageMask`, running 0.6 m on under their edge) and the square of the level inside it less one of that level's voxels, through its own copy of the material (`_LodClip`, `_LodHole` in `Clube/Terrain Materials`); each level sits 0.15 of its voxel low so the finer one wins. The far terrain now starts past the outermost level (2 levels, to 33 km, clipped to its square, sunk half a coarse voxel). About 2,500 coarse chunks; 1 ms a frame on the main thread with Burst off in the Editor. Heightfield generators only; edits don't reach the coarse levels (the world's chunks draw over them). *(Done 2026-10-28; open: the far terrain's snow doesn't continue into the coarse levels, so the great ranges lose their snow as you come within 8 km; levels pop when they swap (no geomorphing); one draw call per coarse chunk.)*
- [ ] **P8** `Core` Biomes / region-based generator blending.
- [ ] **P9** `Core` Ore generation (O1–O5) works with streaming: unloading and reloading a chunk gives identical ore placement.
- [ ] **P10** `Game` *(added)* Settings menu: render distance, sensitivity, graphics options, and *(backlog)* rebinding the player controller's key binds (Input System rebinding, saved with the player settings, A3).
- [ ] **P11** `Core` *(backlog)* GPU marching cubes: run chunk meshing as a compute shader to speed up world generation. Benchmark against the CPU and Burst paths (K11, K12).
  - Note: colliders (M6) and volume math (I5) need the mesh back on the CPU, and that readback can eat the gain; measure end to end, not just the dispatch.
- [ ] **P12** `Core + Config` *(added)* Multi-noise spline terrain, Minecraft style: grows K9's single-curve Spline generator into several independent noise fields, each with its own spline, combined into the final height (and shape, for 3D density):
  - Continentalness: ocean ↔ coast ↔ inland ↔ far inland, at a very low frequency; sets the base height.
  - Erosion: how worn down the land is; high erosion flattens, low erosion allows mountains.
  - Peaks & valleys (folded "weirdness" noise): adds ridges and river valleys on top.
  - Splines that depend on more than one input (e.g. the peaks curve chosen by erosion), editable in `WorldConfig` and previewable in a lab (a 2D map of each field, like K29's export).
  - The same fields can drive biomes (P8, PK5) and caves (P5). Builds on P4 (seeded fields); needs a world larger than one chunk to show anything, which is why it sits here and not in 2C.
- [ ] **P13** `Core + Lab` *(added 2026-10-05)* Midpoint edges and binary density, built for speed: midpoint-only edge placement (V3) over a solid/empty field (one bit per sample, or bytes holding only 0 and 1), with a mesher and storage made for it. Blockier and faceted, but a bit-packed field is 1/32 of flat floats, uniform regions are trivial and the mesher needs no interpolation. Loses the smooth surface and K16's soft brush, and edits snap to whole samples. Compare look and cost against the smooth path (K12, M12) as another storage scheme (2G) and mesher variant (A6).
- [ ] **P14** `Core` *(added 2026-10-05)* Cellular (Worley/Voronoi) noise generator imitating the cracked, bubbly cooling surface of slag: another K9 generator, or a detail layer on top of others, seeded like the rest (K10). Could also shape slag heaps around smelting sites (PR6, 6D).
- [ ] **P15** `Lab` *(added 2026-10-05)* Infinite vs bounded world: compare the current endless streamed world (M1) with a size-limited map holding a limited set of biomes, and so a limited set of tribes and races (Chapter 8). Compare generation cost, how exploration feels, save size (S2), and how deliberately biomes (P8, PK5), tribe territories and settlements (7B) can be placed. Decision recorded in `Docs/` (see Open questions).
- [ ] **P16** `Core + Game` *(added 2026-10-05)* Render distance in metres, not chunks: *(More pressing since M17 made chunks 16³: 4 m wide at 4 voxels/m.)* the chunk size now changes with the voxel size (M25, `ChunkSizing`), so "6 chunks" is 96 m at 1/m and 24 m at 8/m. Store the player setting (M3) in metres, turn it into chunk rings in `WorldStreamer`, and pair it with P7's levels.
- [x] **P17** `Core` *(added 2026-10-28, from `Docs/20261028c clube-backlog.md`)* Streaming and LOD costs at long render distances. Found by timing each system while flying 1.5 m a frame at render distance 64 (the maximum): the streamer's area step took 240 ms a frame (up to 880 ms), the 5 fps, because every chunk-border crossing re-collected, re-hashed and re-counted all 514,120 wanted chunks and scanned every loaded one for unloading, and put the load cursor back to the start, so new chunks at the edge never came up while moving. Now the area is kept per column: `StreamingArea.CollectColumns`, an `IsWanted` test instead of a set, per-column loaded counts in `World` (`LoadedColumns`, `LoadedLayersIn`), unloading by column, and the cursor passes a full column in one step: streaming 177 → 12 ms a frame on average while flying, the area 240 → 4 ms. The coarse terrain now starts at the first level past the render distance (`TerrainLodView.FirstLevelPast`, now `FirstLevelFor` (P19): 6 levels at distance 64, none hidden under the world's chunks). Distant trees keep each tile's matrices from when it arrives and draw only the tiles in the main camera's view (about a quarter), picking near or far mesh per tile; before, all 74,000 matrices were rebuilt every 10 frames while tiles arrived, and every tree was drawn. The far terrain, coarse terrain and distant trees report their main-thread time (`LastUpdateMilliseconds`, shown in their panel sections). *(Done 2026-10-28; open: at distance 64 the world wants half a million chunks and loading still falls behind when moving fast, so with the coarse terrain beyond it 16-24 is the sensible setting (P16); standing still, the 7,700 world chunk renderers cost about 11 ms of rendering in the Editor.)*
- [x] **P18** `Core + Game + Lab` *(added 2026-10-28, from `Docs/20261028e clube-backlog.md`)* 20 fps standing still in WorldLab at render distance 64 over a 400 m area, everything loaded: 35,000 chunk renderers with 57 million vertices (19 million triangles) were drawn every frame. Now the world draws its chunks only within a **detail distance** (`WorldView.DetailDistance`, a player setting, 24 chunks = 96 m; 0 draws all), past which the chunks stay loaded but their renderers are switched off and the coarse terrain and distant trees draw instead; columns holding an edited chunk always draw (`World.HasEditsIn`). The coverage mask counts only drawn columns, and the world hides nothing until the coarse terrain's first level is shown (`CoarseTerrainReady`). In that test 5,600 renderers draw instead of 35,000, and a frame's render submission went from 8.9 to 2.9 ms (eye level) and 5.5 to 1.3 ms (from above) in the Editor; the GPU's share isn't measurable there. The lab panel's World section has the slider. *(Done 2026-10-28; not timed in a real frame.)*
- [x] **P19** `Core` *(added 2026-10-28)* A rectangle of missing ground west of spawn in WorldLab at render distance 64. Since P17 the coarse terrain started at the first level past the render distance, but its first level leaves its middle 2 × 2 chunks for the world's to cover, and a fixed area's world stops at its edge: with a 104 m area and spawn at x = -19.5 the strip from x -64 to -52 had nothing on it. The first level is now the finest whose middle is sure to lie within the world's reach, which is nothing in a fixed area (level 1) and the detail distance within the render distance when it streams (`TerrainLodView.FirstLevelFor`, tested in `TerrainLodTests`). *(Done 2026-10-28.)*

### Material-driven meshing *(added 2026-10-06)*
Materials that mesh differently: smooth soil, faceted rock, square chiselled blocks, Voronoi deep rock and oversized crystal voxels side by side. Rule for the whole section: **no gaps at any seam**. Every mixed seam gets a test that the vertices on both sides match, like M2's border tests, and a lab view to inspect it. Each mode is looked up per material from data (a table indexed by material id), not a branch per vertex, so A6 still holds.

- [ ] **MX1** `Core + Lab` Edge steps per material: how many positions a vertex may take along an edge depends on the material's softness. Stone: midpoint only (1 position, 1 bit: solid or not); clay: 3 positions (2 bits); dirt: 7 (3 bits); sand: 15 (4 bits). Hard rock stays faceted, soft ground rolls smoothly.
  - Open: snap the interpolated position to the nearest of the 2^b − 1 evenly spaced points, or store the density in b bits per sample (which also shrinks storage, P13)? And which material decides an edge between two materials (the solid end, as `VertexMaterialSampler` does)?
  - Generalises P13 (midpoint and binary density, stone's case) and V3 (interpolated vs midpoint, now per material).
- [ ] **MX2** `Core + Lab` Chiselled voxels: a voxel flagged as chiselled meshes as a cube with square edges, inside the marching cubes surface, with no gaps where the two meet. The mesh side of the chisel skill (SK8) and tool (TL2).
- [ ] **MX3** `Core + Lab` Voronoi deep rock: rare deep-earth materials mesh as Voronoi cells within the build grid (M23) instead of marching cubes. A transition helper blends the marching cubes surface into the cells: only slightly gradual, never gapped. Cells seeded per build cell from the world seed (like O2), so borders agree. Related: P14 (cellular noise).
- [ ] **MX4** `Core + Lab` Large-voxel veins and crystals: rare formations whose material uses marching voxels 2× or 4× the normal size, with a seam helper where they meet normal voxels. The same problem as P7's level-of-detail seams (Transvoxel or skirts): solve it once for both.
- [ ] **MX5** `Lab` Trees from marching voxels: test building trees in the marching voxel grid, with finer edge steps than the midpoint (possibly all 255, the full 8 bits that `FlatByte` already stores, MX1), and judge the look and cost against a separate tree mesh. Feeds PK7. *(2026-10-08: GL30 builds them from marching voxels with the interpolated edges the world uses; at 0.25 m voxels a branch needs a radius of about a voxel to stay whole. The comparison against a separate tree mesh is still open.)*
- [ ] **MX6** `Core + Game` *(added 2026-10-12)* Precise placement: once the player has precision and measurement tools (brass instruments, ML7), placed material can go down as full voxel cubes, so some chunks (or regions of them) mesh cubes instead of marching cubes. MX2's square-edged voxels, reached by placing rather than chiselling; the same no-gaps rule applies where cube regions meet the smooth surface.

### Lean game world *(added 2026-10-05; final section of Chapter 4)*
Once the player walks around a world (3B), build a new world scene that is not a lab: the architectural choices the labs compared are baked in and can no longer be changed at run time. Fixing them lets the code drop the variant switches, lab hooks and copies the labs need, so it's simpler and faster. This is A13 made concrete, and the `Game` scene the later chapters build on.

- [ ] **GW1** Decision record in `Docs/`: the choices to bake in, each from its lab result. Starting point: single-byte storage (M12), the Burst mesher (K12), the edge placement and shading the game locks in (A6), the voxel size (M25) and chunk size and shape (M17), streaming on jobs (P3).
  - *(Settled so far, 2026-10-08)* **Chunk size 16 x 16 x 16** (final, M17: every mesh fits 16-bit indices). **Build grid cell 1 m** (final). **Voxel size 0.25 m, 4 voxels per build cell** (leading, to confirm by playtesting, M25). The lab scenes keep all three adjustable for testing; the gameplay demo scenes (`Game`, GW) use the fixed values.
- [ ] **GW2** `Core` Game-only paths: a mesher, storage and streaming path with no variant switches, recorder or lab events (A6 picks once; here there's nothing to pick). The lab code stays for the labs; the game path doesn't reference it. *(Progress 2026-10-05: streaming is already the game path (Burst jobs, byte storage, pooled renderers, `Docs/architecture.md`); left is locking the A6 choices and dropping the switches.)*
- [ ] **GW3** `Game` The new world scene (`Game`): player (3B), streaming, digging and placing, with no `Clube.Debug` components (A10) and no run-time knobs beyond player settings (A3).
- [ ] **GW4** `Lab` Benchmark the lean path against the lab path on the same world (memory, load, meshing, frame time), so the simplification's gain is measured, not assumed.

**Done when:** the `Game` scene runs the baked-in path with no lab components or variant switches, and GW4 records what it saved.

---

## Chapter 5 — Game demo features (`Game`) *(draft — to be expanded)*

Features that make the procedural world feel like a game.

### 5A — Items and player interaction
- [ ] **I1** `Core` Extend the ore and aggregate item definitions from O6 into a general item system as ScriptableObjects (add max stack size, world model, non-material items).
- [ ] **I2** `Game` Items exist in the world as pickups; player picks up by walking over them or pressing an interact key.
- [ ] **I3** `Game` Interaction system: raycast from camera, highlight what's under the crosshair, prompt shown on screen ("Press E to pick up").
- [ ] **I4** `Game` Drop items from inventory back into the world. *(First pass 2026-10-06: `ItemDropper`, G taps one or holds for the stack; each item becomes one sample of its material (the end of its break chain, so loose stone), piled where the player looks or at their feet (`TerrainPile`), lifting the player out if buried. Tools and crafted items can't be dropped until pickups exist (I2).)*
- [ ] **I5** `Game` *(added)* Mining produces items: removed terrain adds material to the inventory based on material type and volume removed (reuses V11 volume math — move it to `Core` at this point).
- [ ] **I6** `Game` *(added)* Placing terrain uses up material from the inventory.
- [ ] **I8** `Game` *(added 2026-10-19)* Dropped items as objects *(refines I4, I2; feeds MF3)*: an item dropped from the inventory falls as a 3D object with physics and collides with the ground, to be picked back up. Each item needs a world model; one too small or too large to show well drops as a nondescript sack.
- [ ] **I7** `Game` *(backlog)* Consumables with timed effects. First one: coffee, a daytime stimulant boost (pairs with PK4 day/night). See the setting-fit open question.

### 5B — Inventory management
- [ ] **N1** `Core` Inventory data model separate from UI: slots, stacking, add/remove/move/split, capacity limits. Keep capacity rules pluggable so tribe progressions and mixed-item bundles (Chapter 8) can slot in later.
- [ ] **N2** `Game` Inventory UI (the inventory screen): grid of slots, drag and drop, stack counts.
- [ ] **N3** `Game` Hotbar with number-key and scroll-wheel selection; selected item determines what the player does (dig, place, use).
- [ ] **N4** `Core` Unit tests for inventory rules (stacking, overflow, split, capacity) per G3.

### 5C — Save and load
- [ ] **S1** `Core` Save format with a version number so older saves can be upgraded later.
- [ ] **S2** `Core` World save: seed plus only the chunks the player modified (unmodified chunks regenerate from the seed). Builds on P6 and uses the storage scheme chosen in M12.
- [ ] **S3** `Core` Player save: position, rotation, inventory contents, hotbar selection.
- [ ] **S4** `Game` Save slots, main menu load, and autosave on a timer and on quit.
- [ ] **S5** `Core` Save and load off the main thread, with no hitch during autosave.
- [ ] **S6** `Core` Round-trip tests: save → load → compare gives identical world and inventory state.

**Done when:** the player can mine materials into the inventory, rearrange them, place terrain from them, quit, and load back into the same world with edits and inventory intact.

### 5D — More materials *(placeholder)*
- [ ] **X1** `Core` Additional ore types (e.g. tin, iron, lead, coal) — new entries in the M9 registry, likely driven by what the tech tree needs. *(2026-10-12: tin first, as an early, low-skill ore (ML4); bauxite nodes for alumina (HF8). The full list is the 6H ladder.)*
- [ ] **X2** `Core` Alternative aggregates (e.g. sand, gravel, clay, granite, limestone), with rules for where each appears.
- [ ] **X3** `Core` *(backlog)* Gemstones as a drop chance from certain host ores, with rarity set the same way as O10. Feeds jewelry crafting (CR8). *(2026-10-12: gemstone tiers and organic gems in ML17.)*

### 5E — HUD *(backlog)*
Other screens are already planned elsewhere: inventory (N2), settings and key binds (P10), skill tree (SK4), tech tree (T3).

- [ ] **U1** `Game` Mini map HUD element.
- [ ] **U2** `Game` Ore gathered readout: a short pickup notice and running totals as material enters the inventory (I5).

### 5F — Mining feel *(backlog)*
- [ ] **MF1** `Core + Game` Hardness and tool upgrades: material hardness (M9) sets how fast a voxel can be extracted, and an upgrade path for extraction tools raises extraction efficiency. Ties to CR3 (better metals give better tools).
- [ ] **MF2** `Game` Test different ore-cracking and pickaxe-swinging animations.
- [ ] **MF3** `Game` Dropped material behaviour: compare the feel of loose dirt and ore clumping back into the terrain mesh (an A7 edit, like PK9) vs dropping as items to pick back up (I2). Decision recorded in `Docs/`.
- [ ] **MF4** `Game` *(added 2026-10-05)* Mining particle effects: dust, chips and debris when terrain is dug, scaled by the amount removed (`BrushResult`) and coloured by the material (M10).
- [ ] **MF5** `Core + Game` *(added 2026-10-06)* Breaking stages for hard materials: the pickaxe turns rock into cracked rock, then into loose rock; loose rock can be picked up with a shovel, a pickaxe or by hand (TL2, TL4). First pass in Milestone 2 (GL3, GL5); the full version covers every hard material and tool, and how loose material behaves when left (SM1, MF3).

### 5G — Tools *(added 2026-10-06)*
Tools act on marching voxel **corners** (density samples), not on a world-space sphere like the lab brush: each tool reaches a cube of voxels (*changed 2026-10-06*: 1 for the hand, 3x3x3 for the pickaxe) and hits their corners. A corner's size is the voxel size, so M25's choice sets how big each tool feels (at 8 per metre a corner is 12.5 cm).

- [ ] **TL1** `Core` Tool model: reach (a cube of voxels around the aimed one), strength per material category, and which materials it works on. First pass: GL1.
- [ ] **TL2** `Core + Game` The tool set:

  | Tool | Impact | Works on | Notes |
  |---|---|---|---|
  | Hand | 1 corner | Soft material | Scoops single corners; very inefficient on hard material; picks up loose rock |
  | Trowel | 1 corner (shared by up to 8 voxels) | Soft material | Fine shaping |
  | Shovel | 4+ corners, maybe 12 in a solid plus shape | Soft and loose material | Picks up loose rock; carries material to drop (SM2) |
  | Pickaxe | About the shovel's area | Hard material | Cracks and loosens rock (MF5); picks up loose rock |
  | Mattock | Between shovel and pickaxe | Hard soil, roots | |
  | Rake | A whole build-grid cell (M23) of corners | Soft material only | Smooths the surface flat |
  | Chisel | One whole marching voxel | Rock | Squares it off (MX2, SK8) |
  | Axe, hatchet, saw | — | Wood | Woodcutting (PK7, MX5) |

- [ ] **TL3** `Game` Tool cursor: a soft glow on the corners within the tool's impact; the chisel's cursor outlines the whole voxel. First pass: GL4.
- [ ] **TL4** `Core` Tool efficiency: tool against material (hardness, M9) sets how much each use does, so the hand is slow on stone and the rake does nothing to it. Ties to MF1 (tool upgrades) and CR3 (better metals). *(2026-10-12: first pass GL39, the shovel and pickaxe reaching wide only on soft or hard ground.)*

### 5H — Soft ground *(added 2026-10-06; promotes PK9)*
- [ ] **SM1** `Core + Lab` Stability: soft materials get a stability from the voxels beneath them; stone is highly stable, sand barely. After an edit, unstable edges slump (move density and material downhill through the A7 path, M5) until every sample is above its material's threshold, like an angle of repose. Answers PK9's question of what "unsupported" means for densities. A lab view colours samples by stability.
- [ ] **SM2** `Core + Game` Dropping material: material poured from a bucket or shovel at a target surface falls with gravity and settles until stable (SM1). Pairs with I6 (placing uses inventory material).

### 5I — Workstations *(added 2026-10-12)*
Stations grow from what's lying around: a stump, then a large stone, then a bench the player builds. GL36's stump is the first pass.

- [ ] **WS1** `Core + Game` Workstation ladder: the tree stump is the first worktop (GL36); a large stone comes next, for sharpening tools, breaking rock and hammering on; after that a wood plank workbench. Each unlocks its own recipes.
- [ ] **WS2** `Core` Tools kept at the worktop: tools crafted there are stored on it, and the better their quality, the better what's crafted there (CR3). A grind wheel made with high-quality tools sharpens better still.
- [ ] **WS3** `Core` Hidden workplace bonus: a station on flat ground works better, and better again on a hard floor (stone, brick). Measured from the samples under it; not shown to the player.
- [ ] **WS4** `Core + Game` Stations built into the terrain: equipment and work areas are made of material in the terrain mesh itself (a stone furnace is cobblestone on the ground), like the stump (GL36, `VoxelMaterial.IsWorktop`), rather than separate objects (GL23). Replaces placed prefabs for stations that suit it.
- [ ] **WS5** `Core + Game` Protected stations: digging leaves a station's samples alone; the player clicks to unlock one, which removes E (use) from it, and can then dig it up for its raw materials.

### 5J — Achievements *(added 2026-10-12)*
- [ ] **AC1** `Game` *"Well, that's the hard way to do that"*: build a furnace by placing its voxels by hand in exactly the right places (WS4) and light it.

---

## Chapter 6 — Progression, processing, and crafting *(design exploration)*

**Setting:** stay in a medieval age, with a few forward-looking, Da Vinci-like inventions: glass and crude optics, water and steam mechanical power, and basic chemistry framed as **alchemy**.

**Design principle:** inspired by real metallurgy but not tedious. Each processing step should be a meaningful choice that changes yield or quality, not busywork. Steps the player has mastered can later be automated (e.g. water power).

### 6A — Skill progression feel test
- [ ] **SK1** `Game` Prototype a minimal skill system (XP from an activity, levels, one visible effect per level) and playtest whether skill progression feels right for the demo, for later gameplay, or not at all.
- [ ] **SK2** `Lab` Toggle skills on/off and override skill levels, so the same world can be compared at novice vs expert.
- [ ] **SK3** Decision record in `Docs/` on whether skills stay, and in which chapter they land.
- [ ] **SK4** `Game` *(backlog)* Skill tree screen, if skills stay (SK3).
- [ ] **SK5** `Core + Game` *(backlog)* Mountaineering skill: higher skill lets the player walk up steeper gradients and climb more mountains. A good candidate for the SK1 feel test, since the effect is immediately visible.
  - Note: compare the surface normal angle against a max slope that rises with skill; it can drive `CharacterController.slopeLimit` directly. Benefits from tall terrain (M17).
- [ ] **SK6** `Game` *(backlog)* Skill books hidden in merchant shops (7C) and blacksmith camps (ST2) that raise a skill once read.
- [ ] **SK7** *(added 2026-10-05)* Recyclable skills: decide what this means before prototyping it (reusing a learned skill in a new line, e.g. forging know-how speeding up recycling in PR6, or regaining spent progress). Part of the SK3 decision.
- [ ] **SK8** *(added 2026-10-06)* Chisel skill: lets the player shape marching voxels into square-edged blocks with the chisel (TL2), meshed by MX2. Skill could set how cleanly or quickly a voxel squares off.

### 6B — Prospecting skill
Prospecting reads the same probability field that ore generation uses (O1–O4). Skill controls how clearly the player sees it.

- [ ] **PS1** `Core` Ore node hints: low skill gives vague direction or a wide area; high skill narrows it to an approximate centroid and depth.
- [ ] **PS2** `Core` Soil/rock sampling: reports the composition of a sampled spot, with more accurate percentages at higher skill.
- [ ] **PS3** `Core` Detection threshold: trace amounts (e.g. a few % gold) only show above a skill level or with better tools.
- [ ] **PS4** `Game` Prospecting tools that scale with tech: bare hands and eyes → pan and sieve → magnifying lens (ties to 6E optics).
- [ ] **PS5** `Lab` Side-by-side view of the true ore field vs what the player's skill reveals.
- [ ] **PS6** `Core + Game` *(added 2026-10-09; parked until skills)* Seeing ore: below a prospecting level, ore in rock looks like its host rock, and mined ore shows a ? for its contents (GL32) until the skill can tell them.

### 6C — Ore processing
Replace the usual "1 ore in, 1 bar out" with a short chain loosely based on the real thing.

- [ ] **PR1** Research pass: pick 3–5 steps worth making gameplay from crushing → washing/sorting → roasting → smelting (with fuel and flux) → refining.
- [ ] **PR2** `Core` Mineral identity: each ore material has a true mineral species (e.g. copper ore as **malachite** or **chalcopyrite**). Before the alchemy unlock the player just sees "copper ore"; after it, the species is revealed. The true identity is always stored; only the display depends on knowledge. (Requires M9 registry to support sub-species.) *(2026-10-12: GL41 splits copper into chalcopyrite and malachite as separate materials, named from the start; hiding the species is still to do.)*
- [ ] **PR3** `Core` Species-specific routes: e.g. malachite smelts fairly directly with charcoal, while chalcopyrite needs roasting first to drive off sulphur. Knowing the species lets the player pick the better route and get higher yield.
- [ ] **PR4** `Core` Quality and yield as outputs of process choices (temperature, fuel, flux, number of refining passes), feeding into crafted item quality.
- [ ] **PR5** Precious metal refining: panning for gold, and cupellation to separate silver from lead ores (needs lead from 5D).
- [ ] **PR6** `Core` *(added 2026-10-05)* Recycling: save iron forge scale (and slag) to reprocess later as a kind of ore. Historically accurate: forge scale is mostly iron oxide and can go back into the bloomery (HF4), and slag still holds some metal. Open: whether scale is its own item or joins mixed ore (PK1).
- [ ] **PR7** `Core` *(added 2026-10-09)* Separating ore from rock: skills and steps (sorting, crushing and washing, PR1) that raise the ore share of mixed ore (GL32) before smelting, and so its yield (GL33).

### 6D — Heat and fuel progression
Furnace temperature is the key that unlocks metals.

- [ ] **HF1** `Core` Fuel types with burn temperatures: wood → charcoal (charcoal kiln / clamp) → later coal or coke. *(First pass 2026-10-09: GL35, burn time only.)*
- [ ] **HF2** `Core` Furnace tiers: open fire / pit → clay-built furnace → fired brick furnace (clay → bricks via a kiln). *(2026-10-12: the ladder is now loose stone → clay → brick (sand + clay) → silica brick → ceramic (alumina + silica), each hotter and more durable; see HF7.)*
- [ ] **HF3** `Core` Airflow: hand bellows → water-powered bellows (6E) for higher temperatures.
- [ ] **HF4** `Core` Temperature gates: copper and bronze first; iron via a bloomery (makes a spongy bloom that must be hammered, not poured); steel later via carburizing iron.
- [ ] **HF5** `Lab` Readout of furnace temperature against each metal's threshold for tuning.
- [ ] **HF6** `Core + Game` *(added 2026-10-12)* Furnace state of health: a furnace wears with every cycle it runs. Repairing it with its own building material restores its health, but only up to a maximum that fades from 100% with cycle use, the CR5 model applied to stations.
- [ ] **HF7** `Core` *(added 2026-10-12)* Furnace build material: what a furnace is built of sets how fast it wears, its highest temperature, and what repairs it.

  | Built of | Wear | Heat | Repaired with | Unlocks |
  |---|---|---|---|---|
  | Loose stone | Fastest | Lowest | Stone | Hot forging copper (ML2), pottery (ML3) |
  | Clay | Slower | Hotter | Clay | Iron (ML6) |
  | Brick (sand + clay) | More durable | Hotter again | Brick | |
  | Silica brick (sand refined to silica) | A slight boost | A slight boost | Silica brick | |
  | Ceramic (alumina + silica) | Most durable | Hot enough for steel | Ceramic | Steel (ML10) |

- [ ] **HF8** `Core + Config` *(added 2026-10-12)* Refractory supply chain: bauxite ore nodes in the world (X1), refined to alumina; additives to alumina improve particular properties for particular uses (heat, wear, strength). Sand refined to silica feeds silica bricks, glass (ML14) and cast iron (ML11).

### 6E — Da Vinci-like inventions *(select few)*
- [ ] **DV1** Glass: sand + heat → glass → crude lenses. Lenses improve prospecting (PS4) and alchemy analysis.
- [ ] **DV2** Water power: water wheel driving bellows, trip hammers, and stamp mills (automates crushing from PR1).
- [ ] **DV3** Steam power: a late, limited invention, e.g. pumping water out of deep mines.
- [ ] **DV4** Alchemy workbench: the unlock that reveals mineral species (PR2) and enables chemical refining steps.

### 6F — Crafting
- [ ] **CR1** `Core` Recipe data model: inputs, outputs, required station (workbench, anvil, …), required tools in hand *(backlog)*, required tech or skill.
- [ ] **CR2** `Core` Medieval item set: tools (pick, shovel, hammer), weapons (sword, axe, spear), armour (mail, plate pieces).
- [ ] **CR3** `Core` Item quality inherited from metal quality (PR4); better metals and processes give better tools, which can mine harder materials (M9 hardness).
- [ ] **CR4** `Game` Crafting stations: forge, anvil, kiln, alchemy bench.
- [ ] **CR5** `Core` *(backlog)* Item state of health: tools, weapons and armour lose durability with use, faster or harder use wearing them down quicker. Repairs can't restore them to 100%: irrecoverable damage builds up slowly, modelled on battery capacity retention over cycle life.
  - Implementation sketch: two values per item, current durability and max capacity (SOH). Repairs refill durability up to max; max only ever decreases. Saved with the item (S3).
  - *(2026-10-12)* First pass: GL37 (hits until a tool breaks, no repair). Furnaces get the same model in HF6.
- [ ] **CR6** `Core` *(backlog)* Repair: costs some time and base materials, with the cost driven by a repair, blacksmith, or general crafting skill (6A).
- [ ] **CR7** `Core` *(backlog)* Recycling: break items and tools back down to base materials. Yield depends on a recycling (or general crafting) skill and scales with the item's state of health (CR5), so worn items return less.
- [ ] **CR8** `Core` *(backlog)* Jewelry crafting from precious metals (PR5) and gems (X3), sold alongside blacksmithed weapons and armour (TR5).

### 6G — Tech tree
- [ ] **T1** Research pass: map the historical order from surface gathering and native metals → copper smelting → bronze (needs tin) → iron bloomery → steel, alongside clay/brick, charcoal, glass, water power, and alchemy. Choose which steps become nodes.
- [ ] **T2** `Core` Tech tree data model: nodes, prerequisites, unlock costs, what each unlocks (items, recipes, stations, ore identification).
- [ ] **T3** `Game` Tech tree UI.
- [ ] **T4** `Core` Tech and skill progress included in the save file (extends S3).

### 6H — Materials and tools ladder *(added 2026-10-12)*
The order materials unlock in, and what each is for. It feeds T1's research pass and the T2 tree; furnace gates are HF7's. Several later metals sit past the medieval setting (see the setting-fit open question).

- [ ] **ML1** `Core` Primitive tools: wood makes a basic stick and spear; stone with those wooden pieces makes a stone hatchet and hammer; leather strips for strapping add a little durability (CR5). Bone is sharper than stone but less durable, and makes a bone sewing needle (TX2). *(First pass 2026-10-12: at the stump worktop, 2 sticks make a spear (an item, no use yet), a stick and 2 stone a stone hatchet, a stick and 3 stone a stone hammer. The hatchet reaches a 3-voxel ball on wood and leaves (`ReachOn.Wood`; wood is no longer hard ground, so the pickaxe hits one voxel of it); the hammer a 2-voxel cube on hard ground; both last 80 hits. Several recipes share inputs, so a station screen with more than one recipe lets the player click the one to make (`CraftingStation.Chosen`). Icons for the stick, spear, hatchet and hammer. Still to come: leather strapping (needs hide, TX6) and bone (needs animals).)* *(Hit counts for a playtest, 2026-10-13: tools can set exact hits per material (`ToolDefinition.HitsFor`: an entry covers a material's break chain, or a whole category such as ore; 0 = can't break), and a broken material comes off whole. Hand: wood 4, leaves 1, dirt, grass and clay 2, no stone or ore. Spear (now a tool, 60 hits): 1 hit on anything but stumps. Stone hatchet (180 hits): wood, leaves and stumps 1, clay 2. Stone hammer: stone and ore 1, clay 2. All three hit one voxel. The pickaxe and shovel still use power against hardness. The cracked and loose stages are switched off for the playtest (`PlayerToolUser` "Break in stages"); `ToolStrike.Hit` keeps them by default.)*
- [ ] **ML2** `Core` Copper tools: cold forging gives a very low yield; hot forging a higher one, and needs at least a stone furnace (HF7). Builds on GL33's yields.
- [ ] **ML3** `Core` Pottery: clay fired in a stone furnace makes basic pottery.
- [ ] **ML4** `Core + Config` Tin, an early low-skill ore: alloyed with copper into bronze tools; alone, basic jewellery and decorative plates.
- [ ] **ML5** `Core` Coal as smelting fuel (HF1).
- [ ] **ML6** `Core` Iron: needs at least a clay furnace, with coal for fuel (HF4).
- [ ] **ML7** `Core` Zinc: alloyed with copper into brass, for technical implements, measuring pieces, clockwork and instruments (TK3, MX6), and later galvanized steel.
- [ ] **ML8** `Core` Lead: weights for scales (and cupellation, PR5).
- [ ] **ML9** `Core` Silver: jewellery and plateware (CR8).
- [ ] **ML10** `Core` Steel: needs at least a ceramic furnace, coal, and bellows (HF3).
- [ ] **ML11** `Core` Cast iron from iron, silica and coal (carbon): a lower melting point, for complex moulds.
- [ ] **ML12** `Core + Game` Gold: jewellery, and it unlocks travelling merchants between cities (7C).
- [ ] **ML13** `Core` Later metals: nickel (advanced alloys, stainless steel), cobalt (stronger equipment), platinum (high-value crafting; unlocks stock market manipulation), aluminium from bauxite (HF8), titanium (lightweight equipment), tungsten (heavy-duty tools and alloys, tool steel), iridium (ultra rare).
- [ ] **ML14** `Core` Non-metals: glass once silica can be refined from sand (DV1); limestone, which unlocks concrete with sand, crushed stone and water; soft rubber from rubber trees; sulfur, which unlocks vulcanized rubber; gunpowder (from sulfur and coal, or by the potassium nitrate route; see Open questions); mercury for advanced scientific instruments.
- [ ] **ML15** `Core` Wood tiers, soft wood to hard wood: harder woods raise the value of what's crafted from them (PK7, TR5).
- [ ] **ML16** `Core` Stone tiers: each unlocks different masonry items and infrastructure.
- [ ] **ML17** `Core` Gemstone tiers (X3): garnet and diamond for tool hardness, the rest for jewellery (CR8). Organic gems tracked too: ivory, jet, amber, pearls and mother of pearl; clams drop pearls, and mother of pearl as a rarer drop.

### 6I — Textiles *(added 2026-10-12)*
- [ ] **TX1** `Core` Three base materials for textiles: fibres, wool and hide.
- [ ] **TX2** `Core` Fibres from plants (straw grass, tall grass, bamboo): fibres → twine → rope (on a rope spinner, TX5); fibres → cloth; stitching from fibres and leather (with the bone needle, ML1).
- [ ] **TX3** `Core` Wool from animals: raw wool → yarn → string and cloth. Rabbit fur, goat, sheep, yak and alpaca wool are each their own raw material.
- [ ] **TX4** `Core` Plant fibres by crop: cotton plants → cotton bolls → yarn; hemp → twine (with an Easter egg: hemp grows twice as much on seed 420).
- [ ] **TX5** `Core + Game` Textile equipment: a rope spinner makes rope, an improvement on twine; a loom makes improved cloth; gold thread unlocks luxury garments.
- [ ] **TX6** `Core` Hide: leather → straps (ML1's strapping).
- Needs plants and animals beyond trees, which aren't in the plan yet (PK13). Linens and carpets feed the shop display bonus (TR7).

### 6J — Timekeeping *(added 2026-10-12)*
Builds on the game clock (GL34's `GameClock`) and the day/night cycle (PK4).

- [ ] **TK1** `Game` Sleeping roll, from a bunch of cloth (TX2): sleep through the night.
- [ ] **TK2** `Game` Sundial: a crude measure of the time of day.
- [ ] **TK3** `Game` Clock: needs precision weights (lead, ML8) and brass gears (ML7).
- [ ] **TK4** `Game` Wristwatch: mastery of precision instruments. Gifting one to a miner for hire unlocks a bonus (PK16).

---

## Chapter 7 — Living world and economy *(design exploration)*

### 7A — NPCs
Crude models and simple behaviours are fine. The point is towns and trade feeling alive, not detailed characters.

- [ ] **NP1** `Game` Placeholder NPC model (capsule or low-poly) with simple animation.
- [ ] **NP2** `Core` Basic behaviours: idle, walk between points, work at a station, stand at a market stall.
- [ ] **NP3** `Core` NPC roles: merchant, smith, miner, villager.
- [ ] **NP4** `Game` Talk/interact prompt reusing the interaction system (I3).

### 7B — Settlement generation
Villages and towns are placed where the natural resources would support them.

- [ ] **ST1** `Core` Region scoring: sample the ore probability field (O1–O4) near the surface without generating voxels, plus terrain flatness and water access, to score each world region by resources and potential wealth.
- [ ] **ST2** `Core` Specialization from dominant resources, e.g. good coal + iron near the surface → small camp specializing in blacksmithing and weaponry.
- [ ] **ST3** `Core` Size from wealth: water + basic ores + gold pockets → larger town with more merchants.
- [ ] **ST4** `Core` Settlement tiers: camp → village → town, each with a set of buildings, NPC count, and merchant slots.
- [ ] **ST5** `Core` Deterministic from seed, using the same world-cell approach as ore centroids (O2), so settlements exist before their chunks are loaded.
- [ ] **ST6** `Core` Flatten/clear terrain under building footprints.
- [ ] **ST7** `Lab` Map overlay showing region scores, chosen settlement sites, and why each specialization was picked.
- [ ] **ST8** `Core` *(backlog)* Town name generator, seeded per settlement (ST5).
  - Pattern: `[Descriptor] [Geological feature] [Settlement type]`, e.g. *Wind River Village*, *Stone Mountain City*, *Gold Coast Outpost*.
  - Descriptors: colours, adjectives, and resource types (copper, silver, gold, steel, iron, bronze, brick, clay, cedar, maple, oak, …).
  - Settlement types: Camp, Outpost, Village, Town, City. The type tracks the ST4 tier, so a town can grow from *Iron Creek Camp* to *Iron Creek City*.
  - Suffixes where they fit, attached to a single word: -ville, -shire, -stead, -borough (e.g. *Copperville*). -stead for the smallest places, -borough for market towns, -shire for regions.
  - Names carry meaning: natural resources (copper, clay, cedar) hint at nearby deposits from ST1 scoring, while manufactured ones (steel, bronze, brick) mark what the town produces (ST2).
- Dependencies: coal and iron (5D X1) and water bodies (not yet in the plan — see open questions).

### 7C — Merchants and trade
- [ ] **TR1** `Core` Per-town supply and demand: towns sell what they specialize in cheaply and pay more for what they lack.
- [ ] **TR2** `Core` Prices respond to trade: selling a lot of one item in a town lowers its price there over time.
- [ ] **TR3** `Core` Currency, possibly silver and gold coin (ties to precious metal refining, PR5).
- [ ] **TR4** `Game` Buy/sell UI with merchant NPCs, showing the town's prices.
- [ ] **TR5** `Core` Item quality (CR3) affects sale price.
- [ ] **TR6** `Core` *(backlog)* Ore value from contained metal: value is the weight of the valuable element locked in the mineral (PR2 species), with the amount of ore measured as volume from marching cubes extraction (I5, V11).
- [ ] **TR7** `Core + Game` *(added 2026-10-12)* Player-owned shop: the player sets up a shop that sells to locals or travellers. What's on display adds an appearance bonus (fine linens and carpets, TX5; high-quality goods, CR3) that nudges the average selling price up. Part of EC2's venture.

### 7D — Economic goal
- [ ] **EC1** Design pass: what "taking control" of a town means (e.g. market share of key goods, owning its workshops, becoming its main supplier) and the win condition.
  - *(backlog)* Candidate end goals to test: make a city the largest economic power; reach a target amount of money; gather every type of ore, mineral and gem; unlock everything in the skill tree; advance to space asteroid mining (see the setting-fit open question).
- [ ] **EC2** `Core` Player venture: owning property or workshops, and possibly hiring NPC workers.
- [ ] **EC3** `Core` Per-town influence meter driven by the player's share of trade and production.
- [ ] **EC4** `Core` Rival businesses or town guilds that compete for the same markets.
- [ ] **EC5** `Game` Ledger UI: wealth over time, influence per town, best trade routes.
- [ ] **EC6** *(backlog)* Core loop design pass: collecting → crafting → selling. Explore what makes each leg enjoyable and how they hand off to each other; playtest before building more systems on top. *(2026-10-06: the first pass is Milestone 2, GL1–GL16, with copper as the test case.)*
- [ ] **EC7** `Core` *(backlog)* Mining rights: the player pays a fee to unlock mining in an area. Fees are set by the nearby city or lord and tied to the player's standing with them (EC3), feeding the town-dominance goal. Needs a region map (ST1). *(2026-10-12: first pass GL42, paying to unlock sections of chunks.)*
- [ ] **EC8** `Core + Game` *(added 2026-10-12)* Toll bridge: a bridge across hard terrain makes travel much easier, and charges a toll to cross. The player could build one as income, or pay others'.

### 7E — World events *(backlog)*
- [ ] **WE1** `Core` Meteor event: a rare meteor crash-lands on the terrain (crater via a large A7 edit, like PK11), leaving a good source of high-quality iron.
  - Note: meteoric iron is real — nickel-iron that was worked before smelting existed — so it fits the "native metals" start of the tech tree (T1) as an early, rare iron source.

---

## Chapter 8 — Tribes and progression scaling *(design exploration)*

Imported from an earlier design discussion. Decided ideas and open exploration are kept separate.

### 8A — Core concept (converged)
Each tribe's progression expresses its worldview through a different mathematical sequence, rather than everyone getting "+10 stack size." The tribes differ in **how** they solve the same problem, so the best tribe shouldn't be obvious from a stack-size graph.

| Tribe | Theme | Sequence | Philosophy |
|---|---|---|---|
| Standard | Physical, practical | 5, 10, 15, 20, 30, 40, 50, 75, 100 | **Add** more |
| Inventor / Mathematician | Geometry, engineering | 6, 12, 18, 24, 30, 48, 60, 72, 120, 180 | **Optimize** (factor-rich numbers) |
| Natural / Organic | Biological, adaptive | 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233 (Fibonacci, no repeated 1) | **Adapt** |
| Computational | Binary, abstract | 1, 2, 4, 8 … 1024 (S(n) = 2ⁿ, from n = 0) | **Encode** |
| Combined *(not a tribe)* | Technology passing through eras | 5, 10, 15, 20 → 24, 36, 48, 60, 96, 120 → 128, 256, 512, 1024 | Physical → mathematical → computational |

How each tribe approaches the same problems:

| Problem | Standard | Inventor | Natural | Computational |
|---|---|---|---|---|
| Carry more | Bigger physical stacks | Divisible stacks | Mixed-item flexible capacity | More address bits |
| Organize | Familiar slots | Mathematical partitioning | Bundling / adaptation | Addressable memory |
| Process ore | Conventional machinery | Ratio-perfect machinery | Sustainable machinery | Scalable automation |
| Handle loss | Conventional recovery | Optimize against loss | Nature may restore losses | Backup / redundancy? |

Key realization: Fibonacci (~1.618ⁿ) can never beat 2ⁿ. **Computational wins raw capacity; Natural changes what capacity means.** Natural should win through positive abilities, not by handicapping Computational.

### 8B — Tribe mechanics to prototype
- [ ] **TB1** `Core` Standard: in-world explanation of +5 steps (one hand = five digits = five units; another hand, grip, or carrying attachment adds 5). Reliable, easy to read, no extreme late scaling.
- [ ] **TB2** `Core` Inventor: a concrete benefit from divisibility, e.g. batches that divide evenly into recipe ratios or machine cycles get better throughput or avoid partial-cycle waste. Formula not decided.
- [ ] **TB3** `Core` Natural, mixed-item bundling: sacks/bundles with one shared capacity pool regardless of item type. The strongest counterweight to 2ⁿ. (Shares the composite-contents idea with mixed ore, PK1.)
- [ ] **TB4** `Core` Natural, connection to nature: a higher nature affinity raises the chance that items lost on death are returned (gifted back, regrown nearby, rare items recoverable). Probability curve not decided.
- [ ] **TB5** `Core` Natural, clean processing: lower nameplate throughput but less energy, maintenance, fuel, and pollution. Ties directly to ecological impact (PK3), which gives it a real systemic payoff.
- [ ] **TB6** `Core` Computational, bits: upgrades unlock another bit, each doubling capacity.
- [ ] **TB7** `Core` Computational, dimensional storage (rare paradigm unlock): S = N^d = 2^(bd), e.g. N = 16 → 16, 256, 4,096. Whether this is Computational-only, general late tech, or too strong is undecided.
- [ ] **TB8** `Core` Computational's natural costs for scale (energy, materials, infrastructure complexity, brittleness) rather than arbitrary efficiency penalties.
- [ ] **TB9** `Core` Machine identity for miner → crusher → smelter per tribe (reliable / ratio-rewarding / sustainable / scalable). Earlier example rates were illustrative only.

### 8C — Lab tools
- [ ] **TB10** `Lab` Progression plotter: all tribe sequences plus reference curves (1–2–5 "pseudo-log", n², n³, 2ⁿ) on linear and log axes, plotted against **cost per tier** once that exists.
- [ ] **TB11** `Lab` Inventory simulation: effective capacity for a realistic mixed haul (e.g. iron, berries, wood, tools, stone) per tribe, to test whether Natural bundling really balances 2ⁿ.
- [ ] **TB12** `Lab` Pipeline simulation: throughput vs total cost (fuel, pollution, maintenance) per tribe over time.

### 8D — Decisions needed
Most important first: **what does one progression step cost?** Comparing tier *n* across tribes only makes sense if tier 8 means roughly the same investment for everyone.

- [ ] **TD1** Cost per tier (research, time, resources, machine complexity).
- [ ] **TD2** Permanent tribe choice, or technology philosophies the player can mix?
- [ ] **TD3** Do stack limits apply per slot, per item type, or per container? (Critical for Natural bundling.)
- [ ] **TD4** How strong is Natural bundling — full fungibility of all inventory, or limited to specific containers?
- [ ] **TD5** Is Combined a fifth generalist tribe, a universal tech tree, or one civilization's history?
- [ ] **TD6** What systemic consequences separate dirty and clean industry (pollution, energy, finite fuel, ecosystem damage, maintenance)?

### Idea pool *(unassigned)*
- Efficiency curve E(x) = 100(1 − 1/x)%: fast early gains, diminishing toward 100%. Candidate for packing efficiency, ecological harmony, machine efficiency, resource recovery, or mastery — including skills (6A).
- Natural extras: self-rebalancing or living inventories, storage plants, graceful overflow, adaptive compression, biome bonuses.

### Explored and set aside
Primes, polygonal numbers, factorials, generic linear→exponential hybrids, 1–2–5 as the universal progression, Natural having secretly better growth, arbitrary Computational efficiency penalties, Natural passive item generation, and Computational overflow destroying items.

---

## Parked ideas

Not scheduled. Revisit once Chapters 3 and 5 are working.

- **PK1 Mixed ore.** Mined material becomes a mixture instead of one item, e.g. a bucket that is 80% dirt, 10% copper ore, 5% gold ore, 5% stone.
  - This falls out of existing work: a brush dig already covers many voxels, and the volume math (V11) can sum volume per material to give the composition.
  - Prospecting skill (PS3) decides which fractions the player can *see*; trace ores below the threshold are still there but show as the host material.
  - Open design points: are mixtures one inventory item with a composition, or do they auto-split? Does separation happen in processing (washing, sorting, panning in PR1)?
  - *(Added 2026-10-09)* First pass: GL32. One item per ore, its contents blended by volume across a stack; separation is PR7.

- **PK2 Multiplayer.** Big question mark; currently leaning **no** (not planning a full Steam release).
  - Don't design for it, but avoid closing the door cheaply: the core already keeps simulation separate from presentation (A1) and generation deterministic from seed (O4, ST5), which are the main things multiplayer would need.
  - Revisit only if the demo becomes a full game.

- **PK3 Ecological impact.** The more the player takes from the land, the sicker the world becomes, so the slow march of wealth turns into ecological decline.
  - A hidden ecology number, increased by voxels mined or altered (and possibly furnace and charcoal use), tracked in the background.
  - It lowers tree growth, fruit yield, and animal reproduction over time.
  - Needs systems not in the plan yet: vegetation, crops or fruit, and animals.
  - Open design points: global vs per-region, whether it can recover or be restored, whether the player is ever told, and whether it affects the economic goal (e.g. towns decline too).

- **PK4 Day/night cycle.** Sun and moon movement with lighting changes across a day.
  - Gives time a visible meaning, and pairs with PK6 (bells as the villages' clock), NPC schedules (NP2) and market hours (7C).
  - Open design points: day length in real minutes, whether night is dangerous or just darker, and whether work (mining, smelting) changes at night.

- **PK5 Climate-driven biomes.** Biomes chosen from parameters such as elevation, temperature and precipitation (and possibly others, e.g. distance to water), instead of being placed directly.
  - A more concrete version of P8. Each parameter is its own noise field from the world seed (P4), so biomes stay deterministic and independent of which chunks are loaded.
  - Biome would then drive surface material (M10), trees (PK7) and settlement scoring (ST1).

- **PK6 Bronze church bells.** Casting a large bronze bell is a late bronze-working achievement that unlocks timekeeping in villages.
  - Depends on bronze (copper + tin, HF4 and X1), large-scale casting, and a village to hang it in (7B).
  - Timekeeping could unlock scheduled markets, NPC work hours, or contracts with deadlines; PK4 gives the hours something to measure.

- **PK7 Trees and woodcutting.** Trees that can be chopped for wood, with several species, value tiers and growth mechanics, with a sense of progression similar to Old School RuneScape's woodcutting (common to rare woods, higher tiers needing better tools or skill).
  - Wood is already in the plan as fuel and charcoal (HF1); species could differ in burn temperature, building or tool use, and trade value (TR1).
  - Growth over time, saplings and replanting tie into PK3 (ecology) and PK5 (which species grow where).
  - Fits the skill question in 6A (woodcutting as a skill).
  - *(Added 2026-10-06)* Whether trees are built from marching voxels is tested in MX5; the axe, hatchet and saw are in the tool set (TL2).
  - *(Added 2026-10-08)* First pass done: GL30 grows trees in the density data (wood and leaves materials, GL31), on flat ground, from the seed. Species, felling, growth and tiers are still to come.

- **PK8 Water.** Oceans, lakes and rivers.
  - Static water bodies are needed anyway for settlement scoring (ST1) and water power (DV2); see Open questions.
  - **Stretch goal:** flowing water physics (filling dug holes, flooding mines, the "pumping water out of deep mines" problem in DV3).

- **PK9 Gravity voxels.** *(Promoted 2026-10-06 to SM1–SM2 in 5H: stability from support below, slumping to a threshold, poured material settling.)* Loose materials such as sand and gravel fall when nothing supports them, like Minecraft's gravity blocks.
  - A material property in the M9 registry (e.g. "loose"), checked after edits through the A7 path; falling voxels move density and material, then dirty the affected chunks (M5).
  - Raises a smooth-terrain question Minecraft doesn't have: what "unsupported" means for densities rather than whole blocks.

- **PK10 Detached terrain bodies (advanced gravity).** When digging fully separates a piece of terrain from the rest, it breaks off and falls and tumbles as its own physics body.
  - Detection: after an edit, check whether the solid voxels near the edit are still connected to the anchored world (flood fill or connected components on the voxel grid). If a region is cut off, extract it into its own mesh.
  - The detached piece becomes a rigidbody with a convex hull (or a set of convex pieces) as its collider, since Unity's MeshCollider must be convex on moving bodies.
  - Open design points: does it re-merge into the terrain when it lands, or stay a separate object; size limits; performance across chunk borders.

- **PK11 Explosives.** TNT/dynamite (or black powder, to fit the setting) for blasting rock.
  - An explosion is just a large, falloff-shaped call to the A7 edit path (like K16's smooth brush), so the core cost is small; the work is effects, damage and drops (I5).
  - Could feed PK10 (blasting chunks loose) and fits the alchemy line (DV4) as a crafted chemical.

- **PK12 Semi-automatic digging machines.** Machines that partly automate ore extraction, e.g. powered drills or dredges that dig while tended.
  - Extends the existing "automate what the player has mastered" principle (Chapter 6, DV2 water power).
  - **Guardrail: this is not a factory game.** Machines should help with the tedious part of a step the player already understands, need tending (fuel, repairs, supervision), and stay local. No sprawling conveyor networks or production lines that run the economy on their own.

- **PK13 Fauna and flora collection.** *(backlog)* A collection system for plants and animals.
  - Needs vegetation and animals, which aren't in the plan yet (same gap as PK3); pairs with PK7 trees.

- **PK14 Grandfather's cottage.** *(backlog)* An old cottage that, as part of the story line, turns out to be the player's grandfather's house. A mountain climber game demo is still on his computer, and the player must figure out how to turn it on or unlock it.

- **PK15 Evolving title music.** *(backlog)* Menu music gains instruments as the player unlocks things: leather making adds drums and percussion; brass or another metal adds stringed instruments.
  - *(Added 2026-10-12)* The ladder: it starts with birds and nature sounds; leather and wood crafting (drums) adds percussion; wood carving adds flutes; strings and better wood add the lute; brass adds horns; brass strings add guitar, violin and cello.

- **PK16 Timepiece Easter egg.** *(backlog)* A side quest to gift a timepiece to the local ore miner; afterwards he mines on beat with the title music (PK15). Timepieces tie into PK6 timekeeping. *(2026-10-12: the timepiece is TK4's wristwatch, gifted to a miner for hire.)*

- **PK17 Building system.** *(backlog)* Parked; it was in an earlier "Milestone 3 / Alpha" plan with undecided status. Would overlap with EC2 (owning property and workshops).
  - *(Added 2026-10-05)* First decision when it's picked up: building snapped to a grid, like Valheim, or placed freely, off-grid, like Rust (as a start). The build grid overlay (M23) can preview snapped placement, and M25 sets the grid's size against the marching voxels (see Open questions).
- **PK18 Seasons and calendar.** *(added 2026-10-03)* A calendar of days, months and years, with seasons that change the world over the year.
  - Builds on PK4 (day/night) for the passing of days; pairs with PK6 (bells keeping village time), market days and seasonal prices (7C), NPC schedules (NP2), and growth cycles for trees and plants (PK7, PK13).
  - Open design points: year length in real time, which systems seasons touch (snow on terrain, frozen water with PK8, crop and tree growth, travel, prices), and whether the calendar drives events (festivals, fairs, harvest; 7E world events).
- **PK19 Weather cycle.** *(added 2026-10-05)* Rain, snow, fog and wind changing over time.
  - Pairs with day/night (PK4) and seasons (PK18). Could affect travel, outdoor work, fire and fuel (6D), and water levels (PK8).

---

## Open questions

- ~~Target chunk size for Chapter 3+ (16³ vs 32³)? Chunk shape?~~ Settled 2026-10-08 (M17): stacked 16³ cubes.
- Which A6 variants does the game lock in (likely interpolated + smooth)?
- Density type for storage: float, half, or byte (affects K26 results and save size)? The backlog leans byte (K28).
- Save file format: binary (compact, fast) vs JSON (readable, easier to debug)?
- Ore spread: single σ (spherical nodes) or per-axis σ (flattened/elongated veins) from the start?
- Should the tech tree gate which ores the player can mine (by tool hardness) or which they can process?
- Skills vs tech tree: separate systems, or does skill level gate tech nodes?
- Mixed ore (PK1): single composite item or auto-split into separate items?
- Water bodies (rivers, lakes) are needed for settlement scoring (ST1) and water power (DV2) — which chapter adds them? (Parked as PK8, with flowing water as a stretch goal.)
- Settlement generation before or after the economy design is settled (EC1)?
- Setting fit: a Computational tribe and the Combined sequence's computational era clash with the medieval + Da Vinci setting. Reframe (e.g. clockwork, counting machines, runes/sigils as 'bits'), push to a later era, or relax the setting? The backlog adds two more cases: coffee (I7) only reached Europe in the 1600s (an exotic import, or a period stimulant instead?), and asteroid mining as an end goal (EC1) leaves the medieval world entirely. *(2026-10-12)* The materials ladder (6H) adds more: aluminium, titanium, tungsten, stainless steel, vulcanized rubber and a stock market (platinum, ML13) are 19th–20th century.
- *(Added 2026-10-12)* Gunpowder (ML14): just sulfur and coal, or the potassium nitrate (saltpetre) route, which is historically right and could be a more satisfying chain?
- *(Added 2026-10-12)* Sections unlocked by paying (GL42): how big is a section, how do locked ones look, and does the price rise with each one?
- Dropped material (MF3): clump back into terrain, or drop as items? Interacts with mixed ore (PK1) and gravity voxels (PK9).
- Do settlements (7B) belong to tribes, so a town's culture shapes what it makes and trades?
- *(Added 2026-10-05)* Infinite procedural world, or a bounded map with a limited set of biomes and so of tribes and races? Tested in P15.
- *(Added 2026-10-05)* Building on a grid (Valheim) or off-grid (Rust)? Part of PK17, sized by M25. (The grid cell is settled at 1 m, 2026-10-08.)
- *(Added 2026-10-06)* Where does Milestone 2's loop run: WorldLab with the player (the default, like Checkpoint 3.1), or should the lean `Game` scene (GW3) be pulled forward so the loop is played without lab tools at all?
- *(Added 2026-10-06)* Breaking stages (GL3, MF5): cracked and loose as extra materials (simple, no mesher changes, but triples the ore ids), or a separate per-sample damage value beside the material (needs storage and shader work)?
- *(Added 2026-10-06)* Tools act on corners (TL1), so their size depends on the voxel size: settle M25 before tuning tool shapes, or define shapes in metres and convert?
- *(Added 2026-10-06)* MX1 edge steps: snap vertex positions per material, or store densities in fewer bits per material?
