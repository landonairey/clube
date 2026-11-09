# clube — Backlog (2026-10-19, third batch)

Notes from the evening of 2026-10-19, kept as they were given (spelling tidied, grouped under headings). They are organized in `Docs/clube-objectives.md` as Milestone 2's twelfth pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Keys

- [ ] **F3** hides the FPS box. → **GL78**
- [ ] Mention **F2** (close the debug panel) on the keys cheat sheet. → **GL78**

## Mining

- [ ] **No floating slivers** — when you break a voxel, if it leaves any surrounding voxels with a very small portion of material that would float as a sliver in the air, remove that material too and add it to what you collected. → **GL79**

## Crafting

- [ ] **Click a recipe to load it** — clicking a recipe at the worktop pulls its ingredients into the input slots. Items can still be added by hand, and the output item still appears. → **GL80**
- [ ] **Bug: wrong default recipe** — with sticks and stones in the inputs, the default is the pointy stick rather than the stone pick. → **GL81**
- [ ] **Stone pick icon** — a stick handle with the stone head coming off one side only (the right), like a true ancient stone pick, not hanging over both sides. → **GL82**

## Building

- [ ] **Bug: campfire on a stump** — the player can place a campfire on top of a stump. → **GL83**
- [ ] **Steep slopes** — building on a steep slope shows the build grid running up and down the vertical wall. Draw the xz hologram floating level instead; on a steep surface colour the item red and don't allow building there. On flat ground the grid follows the terrain as before. → **GL84**

## Campfire

- [ ] **A campfire in the terrain** — instead of a 3D model, placing a campfire puts stone and wood into the terrain mesh, and that arrangement is special: the player can use it like the campfire. Mining its stones and wood breaks the voxels, and it stops being a campfire — just material in the terrain. No material is gained: a campfire mined away completely yields less than it cost to craft. Keep the warm red glow in the centre. → **GL85**
