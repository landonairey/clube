# clube — Backlog (2026-10-06)

Ideas captured on 2026-10-06, kept as they were noted. They are reviewed and organized in `Docs/clube-objectives.md` (the living tracker); the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

**Priority set with this batch:** the next action is the first pass of the gameplay loop (collect → refine → craft → sell). It became **Milestone 2** in the objectives doc, right after Checkpoint 3.2; everything else below is placed in its chapter and waits.

---

## Gameplay loop (priority)

- [ ] **First gameplay loop** — complete the first pass of the loop: collect resource → refine resource → craft resource → sell resource. → **Milestone 2** (GL1–GL16), which is the first pass of EC6
  - Needs: character controller (done, M6), pickaxe tool usage, block-breaking mesh updates, inventory management, furnace usage, anvil workplace usage, merchant table for selling, currency and economy. → **GL1–GL5** (tools, breaking), **GL6–GL7** (inventory), **GL8–GL11** (furnace, anvil), **GL12–GL14** (currency, merchant)
  - Test case: collect copper ore, smelt it in a furnace into a bun, strike the bun on the anvil into a refined ingot, sell the ingot on the market. → **Milestone 2** "Done when", **GL15–GL16**

## Tech / Voxel meshing

- [ ] **Edge positions by material softness** — softer soil like dirt uses interpolated edges for a smooth rolling surface; harder materials like rock keep midpoint edges for the voxels below. → **MX1**
  - 1 position (midpoint only) for stone: 1 bit, solid or none
  - 3 edge positions for clay: 2 bits
  - 7 edge positions for dirt: 3 bits
  - 15 edge positions for sand: 4 bits
- [ ] **Chisel skill** — shape marching voxels into a mesh with pure square edges. The blend of marching cubes and cubic voxel meshes must have no gaps. → **MX2** (mesh), **SK8** (skill), **TL2** (chisel tool)
- [ ] **Trees from marching voxels** — are trees made from marching voxels on the grid? Test creating them in the marching voxels, with more edge steps than the centre midpoint (maybe a full 8-bit set of steps). → **MX5**, note on **PK7**
- [ ] **Voronoi deep-earth voxels** — rare deep-earth voxels change from marching cubes to Voronoi cells within the build grid. Prototype a mesh transition helper from the marching cubes mesh to Voronoi: only slightly gradual, and no gaps in the seams. → **MX3**
- [ ] **Large vein/crystal voxels** — rare large vein or crystal formations where the material uses marching voxels 2× or 4× the normal size, with a mesh helper that removes the seams between the voxel sizes. → **MX4** (shares the seam problem with **P7**)

## Gameplay — materials

- [ ] **Stability of soft materials** — softer materials have a stability calculated from the voxels below them. Stone is highly stable; sand has low stability, and its edges fall until they reach a stability threshold. → **SM1** (promotes **PK9**)
- [ ] **Dropping material** — material dropped out of a bucket or shovel at a target surface falls with gravity until its voxels are stable. → **SM2**
- [ ] **Voxel breaking** — rock turns into cracked rock under a pickaxe, then into loose rock. Loose rock can be picked up with a shovel, a pickaxe or by hand. → **MF5**, first pass **GL3**, **GL5**

## Gameplay — tools

- [ ] **Tool list** — shovel, trowel, chisel, rake, pickaxe, mattock, axe, hatchet, saw (and the hand). → **TL2**; pickaxe and hand first, in **GL1–GL2**
- [ ] **Tool cursor** — a soft glow on the marching voxel corners within the tool's area of impact. A small trowel changes a single corner (shared by several voxels); a shovel at least 4 corners (maybe 12, in a solid plus shape); the chisel's cursor covers the whole marching voxel. → **TL3**, first pass **GL4**
- [ ] **Tool behaviours** — the rake covers a large area (a whole build-grid cell of corners), acts only on softer materials and smooths the mesh flat. The pickaxe covers about the shovel's area and works well on harder materials. The hand scoops single corners of soft material but is very inefficient at breaking hard material. → **TL2**, **TL4**
