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

## Current focus: small scene first
Before any terrain/voxel work: get a minimal, working Unity scene up —
basic scene setup, a camera, confirm the project builds and runs cleanly.
Keep this stage genuinely small. Do not pull in Marching Cubes, chunking,
or terrain generation yet; that comes after the base scene is solid.

## Longer-term direction (for context, not active work)
- Marching Cubes terrain generation with destruction/formation
- Navigable world
- Eventually: mining mechanics, inventory/crafting, RPG progression,
  multiplayer — roughly in that order. Do not build ahead into these until
  asked; flag if a change starts pulling in that direction.

## Conventions
<!-- Fill in as decisions get made -->
- Scene organization: (TBD)
- Naming/formatting conventions: (TBD)
- Folder structure under Assets/: (TBD)

## Environment
- Unity 6.3 LTS, Windows Build Support (IL2CPP)
- C#, Visual Studio
- Unity MCP connected for Editor-level control (Inspector values, scene
  state, console errors) — not just file edits
