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
It defines the architecture rules (A1–A12), ground rules (G1–G5), and every
objective by ID. Reference objective IDs in branches, commits, and PRs.
If this file and the objectives doc disagree, the objectives doc wins —
fix this file.

Milestone 1 = Chapters 0–3. Chapters 4–5 are drafts; Chapters 6–8 are
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
PNG" button write to `Assets/Heightmaps/`.
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
decision). Render distance is a player setting: `PlayerSettings` in
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
**Next:** 2D's K12 (Jobs/Burst), then 2G, then M12, per the build order.

## Conventions
- Assemblies (A1): `Clube.Core`, `Clube.Debug`, `Clube.Game`. Debug and
  Game reference Core; **Core never references Debug or Game.**
- Namespaces match assembly names (`Clube.Core`, `Clube.Debug`, `Clube.Game`).
- Scenes (A9): `VoxelLab`, `ChunkLab`, `WorldLab`, `Game`. The `Game` scene
  contains no `Clube.Debug` components (A10).
- Docs go in `Docs/` (capital D, G5).
- Workflow (G2): one objective = one GitHub issue; one sub-section = one
  branch = one PR, listing the objective IDs it closes.
- Folder structure: code under `Assets/Scripts/{Core,Debug,Game}/`, one
  asmdef per folder, grouped by feature inside (e.g. `Core/Meshing/`).
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
- Naming/formatting: follow the existing code (private fields camelCase,
  `[SerializeField] private`, XML doc comments on public types). Formalize
  later if needed.

## Environment
- Unity 6.3 LTS, URP, Windows Build Support (IL2CPP)
- C#, Visual Studio
- Unity MCP connected for Editor-level control (Inspector values, scene
  state, console errors) — not just file edits
