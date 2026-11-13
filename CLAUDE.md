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

## Current state
Milestone 1 (Chapters 0–3) is built; work is Milestone 2's gameplay passes (GL items),
tracked in the objectives doc. **Now: "Bounded world, models and animals" (GL589–GL636), batches
A–M, one branch and PR per batch, each branch cut from the one before; the world is a
bounded 5 km map (P15 decided), scene `MapGen`.** A planning chat picks the work up from
[Docs/planning-handoff.md](Docs/planning-handoff.md) alone; a batch session starts from
[Docs/batch-brief.md](Docs/batch-brief.md).

What's built, pass by pass, with class names: [Docs/history.md](Docs/history.md). Don't
read it whole — grep it for a class name or ID (`GL344`, `TreeFeller`) to find how and
where something was built. After a pass, add one short paragraph at its end (not here).

Map of the game (details in the history):
- Scenes: `MainMenu` → `Tutorial` (the opening tutorial's own map, when chosen) → WorldLab (fixed 300 m area, the game the demo exe plays) and
  `ProceduralWorldLab` (streamed); `VoxelLab`, `ChunkLab` are the single-voxel and chunk labs.
- Core layers (A14): data → algorithms (`Generation`, `Meshing`, `Editing`, `Queries`,
  `Volume`, `Saving`) → `Streaming` → `Rendering`. `WorldView` is a façade over `World`
  (data), `ChunkPipeline` (Burst jobs), `WorldStreamer`, `ChunkRendererPool`.
- Terrain data per sample: density (byte), material id (`VoxelMaterial` assets via
  `MaterialRegistry`), water (sixteenths), carve flags. Edits go through `World`
  (`SetDensity`, `ApplyBrush`, `SetMaterial`, `SetWater`) so border copies agree.
- Game (`Clube.Game`): player (`Game/Player`), tools (`PlayerToolUser`, `ToolStrike`),
  inventory (`PlayerInventory`, `InventoryGridGui`, `InventoryDrag`), stations
  (`CraftingStation`, `StationObject`, `StationPanel`, `TerrainStructure`), economy
  (`Wallet`, `PriceList`, `MerchantPanel`, `ITrader`), skills (`SkillSet`, `PlayerSkills`),
  quests (`QuestLog`, `PlayerQuests`, `QuestHud`), journals (`FieldLogScreen`), saving
  (`Core/Saving`, `GameSessionHost`, `SaveSlots`; small new state implements `ISavedValues`
  instead of raising the save version), town (`Game/Town`, `TownPlan`).
- Demo exe: *Clube → Build → Game demo* (`DemoBuild`) → `Builds/Demo/`.

Settled (2026-10-08, GW1): chunks are 16 x 16 x 16 and build cells 1 m (both final);
voxels are 0.25 m, 4 per cell, pending playtests (M25). The labs keep these adjustable for
testing; the gameplay demo scenes use the fixed values. With 4 m chunks, P7 (LOD) and P16
(render distance in metres) are what let the world be seen far.

## Backlog → objectives traceability
- Every playtest note or piece of feedback that leads to a new objective goes into a dated
  backlog file, `Docs/backlogs/<yyyymmdd> clube-backlog.md`, in the user's own words (a later
  add-on on the same day goes in a new suffixed file, `20261111b`, `c`...). Never edit an
  existing backlog file.
- Every new GL entry names its backlog file with a link to the line, e.g.
  `*(added 2026-11-11, from [20261111b clube-backlog.md](backlogs/20261111b%20clube-backlog.md#L7))*`,
  and keeps the user's concrete details (numbers, lists, names), not a one-line summary.

## Conventions
- Assemblies (A1): `Clube.Core`, `Clube.Debug`, `Clube.Game`. Debug and
  Game reference Core; **Core never references Debug or Game.** Debug also
  references Game (lab tools tune the player, M16); Game never references Debug.
- Namespaces match assembly names (`Clube.Core`, `Clube.Debug`, `Clube.Game`).
- Scenes (A9): `MainMenu` (the exe's first scene), `Tutorial` (GL514), `VoxelLab`, `ChunkLab`, `WorldLab` (fixed area), `ProceduralWorldLab` (streamed), `MapGen` (the bounded 5 km map, GL598), `Game`. The `Game` scene
  contains no `Clube.Debug` components (A10).
- Docs go in `Docs/` (capital D, G5).
- Workflow (G2): one objective = one GitHub issue; one sub-section = one
  branch = one PR, listing the objective IDs it closes.
- Folder structure: code under `Assets/Scripts/{Core,Debug,Game}/`, one
  asmdef per folder, grouped by feature inside (e.g. `Core/Meshing/`).
  Core's folders are layers (A14, `Docs/architecture.md`): data (`Voxels`,
  `World`, `Materials`, `Config`, `Items`, `Crafting`, `Economy`) → algorithms (`Generation`,
  `Meshing`, `Editing`, `Queries`, `Volume`, `Saving`) → `Streaming` → `Rendering`
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

- Testing gotcha: pumping the World object's `LateUpdate` by hand, for long or from high up,
  ran the GPU out of memory and crashed the Editor: instanced draws free their GPU buffers
  only when a real frame ends (draw once per `Time.frameCount`). Step real frames instead.
- Testing gotcha: `SendMessage("Update")` also runs disabled components (a disabled
  `PlayerController` then resets the head's pitch); step real frames to check look angles.
- Jobs refuse unassigned native arrays even when unread: pass empty arrays (e.g.
  `WaterMap.None`), never `default`.
- Any shader drawn in the world needs a DepthNormals pass: with SSAO the depth texture is
  built from them (GL267).
- Timing: the far terrain, coarse terrain, distant tree and water views have
  `LastUpdateMilliseconds`; `WorldView.Stats.Phases` splits streaming. Real frames in Play
  mode: hook `EditorApplication.update` and read `ProfilerRecorder`s (Internal: `PlayerLoop`,
  `Update.ScriptRunBehaviourUpdate`, `PreLateUpdate.ScriptRunBehaviourLateUpdate`), logging
  to the Editor log. Built game: `clube.exe -townbench -screen-width 2560 -screen-height 1440`
  writes `townbench.txt` (the exe segfaults on quit, after writing it).
- MCP gotcha: a batch-mode Unity (the test run, in any clone, any session's) registers its own bridge, and the MCP tools
  then talk to it instead of the Editor, whose bridge file disappears. Finish Play-mode work before starting the suite,
  and check `ManageEditor GetProjectRoot` before an Editor command while a batch run may be going.
- Roads (GL613, GL631): a road class is a `RoadClassDefinition` row; `WorldRoads.Finish` must follow `WorldPlanner.Build`
  wherever a plan is drawn or built, or its roads are still 40 m cell lines without signposts.
- Token budget: read files in parts (grep first), check compiles through `Editor.log`, take
  screenshots only for visual items, run the batch-mode test suite once per batch.

## Environment
- Unity 6.3 LTS, URP, Windows Build Support (IL2CPP)
- C#, Visual Studio
- Unity MCP connected for Editor-level control (Inspector values, scene
  state, console errors) — not just file edits
