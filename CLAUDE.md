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

## Current focus: Chapter 0 — Minimal scene
Close out C0.1–C0.4: assembly definitions per A1, a `VoxelLab` scene shell
with a debug free-fly camera, README and `.gitattributes`, and migrating the
pre-plan single-voxel prototype (PR #1, `Assets/Scripts/Voxel/`) into the
new assembly layout. Rebuilding it as a 1×1×1 chunk (A8) is Chapter 1 work.

## Conventions
- Assemblies (A1): `Clube.Core`, `Clube.Debug`, `Clube.Game`. Debug and
  Game reference Core; **Core never references Debug or Game.**
- Namespaces match assembly names (`Clube.Core`, `Clube.Debug`, `Clube.Game`).
- Scenes (A9): `VoxelLab`, `ChunkLab`, `WorldLab`, `Game`. The `Game` scene
  contains no `Clube.Debug` components (A10).
- Docs go in `Docs/` (capital D, G5).
- Workflow (G2): one objective = one GitHub issue; one sub-section = one
  branch = one PR, listing the objective IDs it closes.
- Folder structure under `Assets/`: to be set in C0.2.
- Naming/formatting: follow the existing code (private fields camelCase,
  `[SerializeField] private`, XML doc comments on public types). Formalize
  later if needed.

## Environment
- Unity 6.3 LTS, URP, Windows Build Support (IL2CPP)
- C#, Visual Studio
- Unity MCP connected for Editor-level control (Inspector values, scene
  state, console errors) — not just file edits
