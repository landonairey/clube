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

## Current focus: Chapter 2 — Single chunk (`ChunkLab`)
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
2B in progress: `VoxelSelector` picks voxels by click (K5) and F frames the
selection (K6); next is K7, the per-voxel corner controls.

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
- MCP gotcha: GameObject instance IDs change on every domain reload
  (entering Play mode, recompiling). Look objects up again with `find`
  before passing an ID to tools like `Camera_Capture`.
- Naming/formatting: follow the existing code (private fields camelCase,
  `[SerializeField] private`, XML doc comments on public types). Formalize
  later if needed.

## Environment
- Unity 6.3 LTS, URP, Windows Build Support (IL2CPP)
- C#, Visual Studio
- Unity MCP connected for Editor-level control (Inspector values, scene
  state, console errors) — not just file edits
