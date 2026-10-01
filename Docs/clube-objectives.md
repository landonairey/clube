# clube — Destructible Terrain Demo: Scoped Objectives

Project: `E:\Repos\Unity3D\clube` (fresh restart; earlier terrain-demo repo is reference only, not ported)
Target: Milestone 1 — Destructible Terrain Demo = **Chapters 0–3** (ends at Checkpoint 3.2)

Each objective has an ID so it maps 1:1 to a GitHub issue; PRs group objectives per sub-section (see G2). Items marked *(added)* are suggestions beyond the original list — keep, cut, or defer as you like.

**Plan maturity:** Chapters 0–3 are scoped and schedulable. Chapters 4–5 are drafts. Chapters 6–8 are design exploration — context for decisions, not work to schedule.

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

- [ ] **A1** Assemblies: `Clube.Core` (data, meshing, generation, chunk management), `Clube.Debug` (lab components, gizmos, readouts), `Clube.Game` (player, UI, settings). `Clube.Debug` and `Clube.Game` reference `Clube.Core`; **Core never references Debug or Game.**
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
  - `WorldLab` — chunk manager + chunk focus mode, stats
  - `Game` — chunk manager, player, settings menu, **no debug**
- [ ] **A10** Release build check: `Game` scene has no `Clube.Debug` components, and the debug assembly is excluded from release builds.
- [ ] **A11** Step recording: the mesher accepts an optional recorder that logs each algorithm step as data. Compiled out of release builds (A5) and zero cost when no recorder is passed. Playback lives in `Clube.Debug` and never re-runs the algorithm itself.
- [ ] **A12** Voxel storage behind an interface (`IVoxelStorage`: get/set density and material, iterate, serialize). Meshing, editing and saving talk only to the interface, so flat array, RLE and octree implementations can be swapped and compared.

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

Note: V1, V2 and V6 now run on the 1×1×1 chunk through the A7 edit path (the pre-plan `SingleVoxel` component is gone). A2 and A12 are started but not complete: `WorldConfig` has no generator or seed yet (K9, K10), and `IVoxelStorage` has density only (materials M10, iteration and serialization K26).

### 1A — Core controls
- [x] **V1** `Config` Iso value slider.
- [x] **V2** `Core + Lab` Individual sliders for the 8 corner values (via the A7 edit path).
- [x] **V3** `Core + Lab` Interpolated edge vertices vs edge midpoints (A6 variant).
- [x] **V4** `Core + Lab` Smooth vs flat shading (A6 variant).
- [x] **V5** `Lab` Toggle triangle winding / draw direction (`FlipFaces` component).

### 1B — Visual debugging
- [x] **V6** `Lab` Corner gizmo spheres, grayscale by corner value.
- [x] **V7** `Lab` *(added)* Show current case index (0–255) and which of the 15 base configurations it maps to.
- [x] **V8** `Lab` *(added)* Normal gizmos per vertex/face (verifies V4 and V5).
- [x] **V9** `Lab` *(added)* Highlight active edges from the edge table.
- [x] **V10** `Lab` *(added)* Preset buttons for known cases, including ambiguous ones.
- [x] **V20** `Lab` *(added)* Type a case index (0–255) to jump straight to it: corners whose bit is set become solid, the rest empty. Step one case at a time with ◀ ▶ in the Inspector or the left/right arrow keys in Play mode.
- [x] **V21** `Lab` *(added)* Teaching labels on the cube corners: each corner's index and the bit it sets in the case index (e.g. "c3 → bit 3 = 8"), marked solid or empty, alongside the resulting case index in binary and decimal. Shows how corners map to the case index and from there to the triangles.

### 1C — Volume inspection
- [x] **V11** `Lab` Approximate fill: weighted-percentage volume from corner values. *(Already in Core as `VoxelVolume`, alongside V12 and the V14 reference, since mining (I5) will need it.)*
- [x] **V12** `Lab` Exact fill via tetrahedralisation of the enclosed solid.
  - Note: the MC surface alone is open; the enclosed solid is the mesh **plus** the cube faces clipped to the inside region. Close it first, then fan into tetrahedra from a reference point and sum signed volumes.
- [x] **V13** `Lab` Exploded view of the tetrahedra (explode-distance slider, per-tet colouring).
- [x] **V14** `Lab` *(added)* Readout comparing V11 vs V12 (absolute and % error); optional Monte Carlo sample count as a third reference.

### 1D — Step-through animation
Record the real algorithm once (A11), then replay the log. Stepping back just rebuilds state up to an earlier point in the log.

- [ ] **V15** `Core` Step recorder logging a struct per step: step type, voxel coordinate, corner values, case index, edge mask, vertex positions, triangle.
- [ ] **V16** `Lab` Playback of the per-voxel steps:
  1. Read corners — highlight cube and its 8 corner values
  2. Classify — colour corners inside/outside against the iso value
  3. Case index — build the 8-bit number corner by corner, shown in binary and decimal
  4. Edge table — highlight crossed edges
  5. Interpolate — slide each vertex along its edge (or snap to midpoint per V3)
  6. Triangle table — add triangles one at a time, showing winding order
- [ ] **V17** `Lab` Controls: play/pause, step forward/back (e.g. right/left arrow keys, new Input System), restart, speed slider, per-step-type durations. Note: the arrow keys currently step case indices in VoxelLab (V20, `CaseStepInput`); give V17 a mode switch or different keys.
- [ ] **V18** `Lab` Info line describing the current step, e.g. "Voxel (3,1,2): corners 0, 3, 5 inside → case 41 → edges 0, 3, 8."
- [ ] **V19** `Lab` Visuals that work in the game window, not Gizmos: pooled spheres for corners, `LineRenderer` for edges, separate mesh for the partial surface, on-screen UI for the info line.

**Done when:** every toggle updates the mesh live, V12 returns 0 for an all-outside cube and 1 (× unit volume) for all-inside, and the V16 sequence can be played through or stepped both ways for any preset case (V10).

---

## Chapter 2 — Single chunk (`ChunkLab`)

Goal: scale to an X×Y×Z grid while keeping all Chapter 1 controls reachable.

### 2A — Structure
- [ ] **K1** `Config` Chunk size (X, Y, Z integers).
- [ ] **K2** `Config` Voxel unit size.
- [ ] **K3** `Lab` Chapter 1 global toggles (iso, interpolation, shading, winding, gizmos) apply chunk-wide.
- [ ] **K4** `Lab` *(added)* Stats readout: vertex count, triangle count, last mesh build time (ms).

### 2B — Voxel selection
- [ ] **K5** `Lab` Click to select a voxel; highlight it.
- [ ] **K6** `Lab` Focus camera on selected voxel.
- [ ] **K7** `Lab` Selected voxel exposes the Chapter 1 per-voxel controls. Editing a corner goes through A7 and updates neighbouring voxels that share it.

### 2C — Terrain surface generation
- [ ] **K8** `Config` Base surface level + amplitude parameters.
- [ ] **K9** `Core` Generators, selected via `Config`:
  - Flat
  - Sine wave
  - 2D Perlin
  - 3D Perlin
  - 2D Perlin with octaves (frequency, lacunarity, persistence)
  - Spline-based (height remapping curve)
  - Heightmap import (PNG/RAW)
- [ ] **K10** `Config` *(added)* Seed parameter for all noise generators.

### 2D — Performance
- [ ] **K11** `Lab` Benchmark: `List<T>` vs preallocated arrays for mesh building.
  - Method: Stopwatch over N runs after warmup; record ms and GC allocations (Profiler / `GC.GetAllocatedBytesForCurrentThread`); test at 3+ chunk sizes.
  - Output: results table in the repo (`Docs/benchmarks.md`). Winner becomes the `Core` implementation.
- [ ] **K12** `Lab` *(added, optional)* Third variant: `NativeArray` + Jobs/Burst — sets up Chapter 4 threading.

### 2E — Terrain editing
- [ ] **K13** `Core + Lab` Click to add terrain (A7 path).
- [ ] **K14** `Core + Lab` Click to remove terrain (A7 path).
- [ ] **K15** `Core + Lab` Brush radius control.
- [ ] **K16** `Core + Lab` *(added)* Brush strength / falloff (hard vs smooth sphere).

### 2F — Step-through animation at chunk scale
Extends Chapter 1 playback (V15–V19) so the mesh can be watched growing voxel by voxel.

- [ ] **K17** `Lab` Opening step: show the sampled density field as a grid of grayscale points before any voxel is processed.
- [ ] **K18** `Lab` Closing step: calculate normals and show the finished shaded mesh.
- [ ] **K19** `Lab` Granularity setting: step by sub-step, whole voxel, or whole Y layer.
- [ ] **K20** `Lab` Skip-empty-voxels toggle (cases 0 and 255 produce no triangles and dominate terrain chunks).
- [ ] **K21** `Lab` Jump to voxel, tied to voxel selection (K5).
- [ ] **K22** `Lab` Optional camera follow on the current voxel.

### 2G — Voxel data storage exploration
Compare storage schemes on a single chunk, behind the A12 interface. Measured again at scale in Chapter 3 (M12).

- [x] **K23** `Core` Flat array baseline (current storage).
- [ ] **K24** `Core` Run-length encoding: runs along one axis; test at least two run orders (e.g. X-first vs Y-first) since terrain is mostly vertical layers.
- [ ] **K25** `Core` Sparse octree: uniform regions collapse into single nodes; configurable max depth.
- [ ] **K26** `Lab` Benchmark each scheme for memory size, read speed during meshing, write speed during brush edits, and serialized size.
  - Note: smooth marching cubes needs varied densities near the surface, so compression mostly comes from solid and air regions. Also test with quantized densities (e.g. byte instead of float) to see how that changes the results.
  - Output: results added to `Docs/benchmarks.md`.
- [ ] **K27** `Lab` *(added)* Visualize the storage: RLE runs as coloured bars, octree nodes as wireframe boxes at each depth.

**Done when:** a 32³ chunk can be generated with any K9 generator, edited in play mode, and benchmark results are committed; a 4×4×4 chunk can be played through at voxel granularity in about a minute.

---

## Chapter 3 — Collection of chunks (`WorldLab` → `Game`)

This is where the shift happens: lab controls stay in `WorldLab`, and `Game` gets its first playable form.

### 3A — Chunk management
- [ ] **M1** `Core` *(added — prerequisite)* Chunk coordinate system and chunk manager (world pos ↔ chunk ↔ voxel).
- [ ] **M2** `Core` *(added — prerequisite)* Seamless borders: chunks share edge density samples so no cracks/gaps.
- [ ] **M3** `Game` Render distance setting controls how many chunks are loaded (player setting per A3, not a config value).
- [ ] **M4** `Lab` Focus mode: select and highlight an individual chunk; show its stats.
- [ ] **M5** `Core` *(added)* Terrain edits that cross chunk borders update all affected chunks.
- [ ] **M12** `Lab` Storage at scale: repeat the K26 benchmark across many loaded chunks (total memory, load/unload time) and choose the `Core` storage for the game. Record the decision in `Docs/benchmarks.md`.

### 3B — Player
- [ ] **M6** `Game` Basic character: walk, jump, collide with terrain (MeshCollider regenerated on edit).
- [ ] **M7** `Lab` *(added)* Toggle between player camera and debug free-fly camera.
- [ ] **M8** `Game` *(added)* In-game dig/place controls calling the A7 edit path; brush size as a player setting.

**Checkpoint 3.1 — playable terrain (after 3A + 3B):** in the `Game` scene, the player can walk across a multi-chunk area and dig through a chunk border without seams, using a single placeholder material. No debug components present (A10).

### 3C — Materials
- [ ] **M9** `Core` Material registry: one definition per material (id, category aggregate/ore, texture, colour, hardness, drop item). Starts with aggregates **grass, dirt, stone** and ores **gold, silver, copper**; built so more of each can be added later without code changes, and so a material can later carry hidden mineral species (PR2).
- [ ] **M10** `Core` Per-voxel material ID stored alongside density in voxel storage (A12). Aggregates assigned by depth below the surface: grass on top, a dirt band, stone below.
- [ ] **M11** `Core` Terrain shader (URP — Shader Graph or hand-written URP HLSL) using a texture array indexed by material ID, so one material handles all six types.
- [ ] **M13** `Core` Blending where materials meet: marching cubes vertices sit on edges between voxels, so pass per-vertex material weights (e.g. vertex colours or UV channels) and blend in the shader instead of hard seams.
- [ ] **M14** `Core` *(added)* Triplanar mapping so steep faces don't stretch.
- [ ] **M15** `Core + Lab` Toggle between material display modes in `WorldLab` and `ChunkLab`, as an A6 mesh variant chosen once per chunk build:
  - **Hard seams:** each triangle takes one material (e.g. the dominant material among its voxel's corners), with vertices split along material borders so there's no bleeding.
  - **Blended:** per-vertex material weights blended in the shader (M13).
  - *(added)* **Debug colours:** flat colour per material ID, no textures, to check material assignment and ore placement at a glance.
  - Readout of vertex count per mode, since hard seams duplicate vertices along borders.

### 3D — Ore generation
Ore nodes are generated as a procedural centroid with a 3D Gaussian falloff. Each voxel near a node rolls against the falloff probability to decide whether ore replaces its aggregate material.

- [ ] **O1** `Core` Ore definition per ore type (added to the M9 registry): nodes per region, depth range, peak probability at the centroid, spread (σ, optionally separate σx, σy, σz for flattened or elongated veins), host materials it may replace (e.g. stone only).
- [ ] **O2** `Core` Centroid placement: divide the world into fixed-size cells, and derive each cell's node centroids from the world seed and cell coordinate. Deterministic, and independent of which chunks are loaded.
- [ ] **O3** `Core` Cross-chunk nodes: when generating a chunk, check centroids in neighbouring cells within ~3σ so nodes near borders spill correctly into adjacent chunks.
- [ ] **O4** `Core` Replacement roll: probability = peak × exp(−d² / 2σ²), compared against a hash of the voxel's world position and seed (not `Random`), so the same world always regenerates identically.
- [ ] **O5** `Core` Overlap rule when two ore nodes reach the same voxel (e.g. highest probability wins, or fixed ore priority).
- [ ] **O6** `Core` Ore items: one item definition per ore and aggregate type (id, name, icon), linked from the material registry. Chapter 5 inventory and mining build on these.
- [ ] **O7** `Lab` Ore visualization: centroid markers, translucent spheres at 1σ/2σ/3σ, and an X-ray mode that hides aggregates to show ore voxels only.
- [ ] **O8** `Lab` Live ore parameter tuning in `WorldLab` with regenerate, plus a readout of ore voxel count per type per chunk.
- [ ] **O9** `Core` Unit tests: same seed gives same centroids and ore voxels; probability falls off correctly with distance; border chunks agree.

**Checkpoint 3.2 — materials and ore (after 3C + 3D), completes Milestone 1:** the Checkpoint 3.1 scene now shows all six materials, with ore nodes spanning chunk borders consistently. Still no debug components (A10).

---

## Chapter 4 — Procedurally generated chunks (`Game`) *(draft — to be expanded)*

- [ ] **P1** `Core` Chunks stream in/out around the player (load/unload radius from render distance).
- [ ] **P2** `Core` Chunk object pooling.
- [ ] **P3** `Core` Background mesh generation (Jobs/Burst or threads); no frame hitches on load.
- [ ] **P4** `Core` Deterministic world from seed.
- [ ] **P5** `Core` Caves via 3D noise.
- [ ] **P6** `Core` Persist player edits for unloaded chunks.
- [ ] **P7** `Core` Level of detail for distant chunks.
- [ ] **P8** `Core` Biomes / region-based generator blending.
- [ ] **P9** `Core` Ore generation (O1–O5) works with streaming: unloading and reloading a chunk gives identical ore placement.
- [ ] **P10** `Game` *(added)* Settings menu: render distance, sensitivity, graphics options.

---

## Chapter 5 — Game demo features (`Game`) *(draft — to be expanded)*

Features that make the procedural world feel like a game.

### 5A — Items and player interaction
- [ ] **I1** `Core` Extend the ore and aggregate item definitions from O6 into a general item system as ScriptableObjects (add max stack size, world model, non-material items).
- [ ] **I2** `Game` Items exist in the world as pickups; player picks up by walking over them or pressing an interact key.
- [ ] **I3** `Game` Interaction system: raycast from camera, highlight what's under the crosshair, prompt shown on screen ("Press E to pick up").
- [ ] **I4** `Game` Drop items from inventory back into the world.
- [ ] **I5** `Game` *(added)* Mining produces items: removed terrain adds material to the inventory based on material type and volume removed (reuses V11 volume math — move it to `Core` at this point).
- [ ] **I6** `Game` *(added)* Placing terrain uses up material from the inventory.

### 5B — Inventory management
- [ ] **N1** `Core` Inventory data model separate from UI: slots, stacking, add/remove/move/split, capacity limits. Keep capacity rules pluggable so tribe progressions and mixed-item bundles (Chapter 8) can slot in later.
- [ ] **N2** `Game` Inventory UI: grid of slots, drag and drop, stack counts.
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
- [ ] **X1** `Core` Additional ore types (e.g. tin, iron, lead, coal) — new entries in the M9 registry, likely driven by what the tech tree needs.
- [ ] **X2** `Core` Alternative aggregates (e.g. sand, gravel, clay, granite, limestone), with rules for where each appears.

---

## Chapter 6 — Progression, processing, and crafting *(design exploration)*

**Setting:** stay in a medieval age, with a few forward-looking, Da Vinci-like inventions: glass and crude optics, water and steam mechanical power, and basic chemistry framed as **alchemy**.

**Design principle:** inspired by real metallurgy but not tedious. Each processing step should be a meaningful choice that changes yield or quality, not busywork. Steps the player has mastered can later be automated (e.g. water power).

### 6A — Skill progression feel test
- [ ] **SK1** `Game` Prototype a minimal skill system (XP from an activity, levels, one visible effect per level) and playtest whether skill progression feels right for the demo, for later gameplay, or not at all.
- [ ] **SK2** `Lab` Toggle skills on/off and override skill levels, so the same world can be compared at novice vs expert.
- [ ] **SK3** Decision record in `Docs/` on whether skills stay, and in which chapter they land.

### 6B — Prospecting skill
Prospecting reads the same probability field that ore generation uses (O1–O4). Skill controls how clearly the player sees it.

- [ ] **PS1** `Core` Ore node hints: low skill gives vague direction or a wide area; high skill narrows it to an approximate centroid and depth.
- [ ] **PS2** `Core` Soil/rock sampling: reports the composition of a sampled spot, with more accurate percentages at higher skill.
- [ ] **PS3** `Core` Detection threshold: trace amounts (e.g. a few % gold) only show above a skill level or with better tools.
- [ ] **PS4** `Game` Prospecting tools that scale with tech: bare hands and eyes → pan and sieve → magnifying lens (ties to 6E optics).
- [ ] **PS5** `Lab` Side-by-side view of the true ore field vs what the player's skill reveals.

### 6C — Ore processing
Replace the usual "1 ore in, 1 bar out" with a short chain loosely based on the real thing.

- [ ] **PR1** Research pass: pick 3–5 steps worth making gameplay from crushing → washing/sorting → roasting → smelting (with fuel and flux) → refining.
- [ ] **PR2** `Core` Mineral identity: each ore material has a true mineral species (e.g. copper ore as **malachite** or **chalcopyrite**). Before the alchemy unlock the player just sees "copper ore"; after it, the species is revealed. The true identity is always stored; only the display depends on knowledge. (Requires M9 registry to support sub-species.)
- [ ] **PR3** `Core` Species-specific routes: e.g. malachite smelts fairly directly with charcoal, while chalcopyrite needs roasting first to drive off sulphur. Knowing the species lets the player pick the better route and get higher yield.
- [ ] **PR4** `Core` Quality and yield as outputs of process choices (temperature, fuel, flux, number of refining passes), feeding into crafted item quality.
- [ ] **PR5** Precious metal refining: panning for gold, and cupellation to separate silver from lead ores (needs lead from 5D).

### 6D — Heat and fuel progression
Furnace temperature is the key that unlocks metals.

- [ ] **HF1** `Core` Fuel types with burn temperatures: wood → charcoal (charcoal kiln / clamp) → later coal or coke.
- [ ] **HF2** `Core` Furnace tiers: open fire / pit → clay-built furnace → fired brick furnace (clay → bricks via a kiln).
- [ ] **HF3** `Core` Airflow: hand bellows → water-powered bellows (6E) for higher temperatures.
- [ ] **HF4** `Core` Temperature gates: copper and bronze first; iron via a bloomery (makes a spongy bloom that must be hammered, not poured); steel later via carburizing iron.
- [ ] **HF5** `Lab` Readout of furnace temperature against each metal's threshold for tuning.

### 6E — Da Vinci-like inventions *(select few)*
- [ ] **DV1** Glass: sand + heat → glass → crude lenses. Lenses improve prospecting (PS4) and alchemy analysis.
- [ ] **DV2** Water power: water wheel driving bellows, trip hammers, and stamp mills (automates crushing from PR1).
- [ ] **DV3** Steam power: a late, limited invention, e.g. pumping water out of deep mines.
- [ ] **DV4** Alchemy workbench: the unlock that reveals mineral species (PR2) and enables chemical refining steps.

### 6F — Crafting
- [ ] **CR1** `Core` Recipe data model: inputs, outputs, required station, required tech or skill.
- [ ] **CR2** `Core` Medieval item set: tools (pick, shovel, hammer), weapons (sword, axe, spear), armour (mail, plate pieces).
- [ ] **CR3** `Core` Item quality inherited from metal quality (PR4); better metals and processes give better tools, which can mine harder materials (M9 hardness).
- [ ] **CR4** `Game` Crafting stations: forge, anvil, kiln, alchemy bench.

### 6G — Tech tree
- [ ] **T1** Research pass: map the historical order from surface gathering and native metals → copper smelting → bronze (needs tin) → iron bloomery → steel, alongside clay/brick, charcoal, glass, water power, and alchemy. Choose which steps become nodes.
- [ ] **T2** `Core` Tech tree data model: nodes, prerequisites, unlock costs, what each unlocks (items, recipes, stations, ore identification).
- [ ] **T3** `Game` Tech tree UI.
- [ ] **T4** `Core` Tech and skill progress included in the save file (extends S3).

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
- Dependencies: coal and iron (5D X1) and water bodies (not yet in the plan — see open questions).

### 7C — Merchants and trade
- [ ] **TR1** `Core` Per-town supply and demand: towns sell what they specialize in cheaply and pay more for what they lack.
- [ ] **TR2** `Core` Prices respond to trade: selling a lot of one item in a town lowers its price there over time.
- [ ] **TR3** `Core` Currency, possibly silver and gold coin (ties to precious metal refining, PR5).
- [ ] **TR4** `Game` Buy/sell UI with merchant NPCs, showing the town's prices.
- [ ] **TR5** `Core` Item quality (CR3) affects sale price.

### 7D — Economic goal
- [ ] **EC1** Design pass: what "taking control" of a town means (e.g. market share of key goods, owning its workshops, becoming its main supplier) and the win condition.
- [ ] **EC2** `Core` Player venture: owning property or workshops, and possibly hiring NPC workers.
- [ ] **EC3** `Core` Per-town influence meter driven by the player's share of trade and production.
- [ ] **EC4** `Core` Rival businesses or town guilds that compete for the same markets.
- [ ] **EC5** `Game` Ledger UI: wealth over time, influence per town, best trade routes.

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

- **PK8 Water.** Oceans, lakes and rivers.
  - Static water bodies are needed anyway for settlement scoring (ST1) and water power (DV2); see Open questions.
  - **Stretch goal:** flowing water physics (filling dug holes, flooding mines, the "pumping water out of deep mines" problem in DV3).

- **PK9 Gravity voxels.** Loose materials such as sand and gravel fall when nothing supports them, like Minecraft's gravity blocks.
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

---

## Open questions

- Target chunk size for Chapter 3+ (16³ vs 32³)?
- Which A6 variants does the game lock in (likely interpolated + smooth)?
- Density type for storage: float, half, or byte (affects K26 results and save size)?
- Save file format: binary (compact, fast) vs JSON (readable, easier to debug)?
- Ore spread: single σ (spherical nodes) or per-axis σ (flattened/elongated veins) from the start?
- Should the tech tree gate which ores the player can mine (by tool hardness) or which they can process?
- Skills vs tech tree: separate systems, or does skill level gate tech nodes?
- Mixed ore (PK1): single composite item or auto-split into separate items?
- Water bodies (rivers, lakes) are needed for settlement scoring (ST1) and water power (DV2) — which chapter adds them? (Parked as PK8, with flowing water as a stretch goal.)
- Settlement generation before or after the economy design is settled (EC1)?
- Setting fit: a Computational tribe and the Combined sequence's computational era clash with the medieval + Da Vinci setting. Reframe (e.g. clockwork, counting machines, runes/sigils as 'bits'), push to a later era, or relax the setting?
- Do settlements (7B) belong to tribes, so a town's culture shapes what it makes and trades?
