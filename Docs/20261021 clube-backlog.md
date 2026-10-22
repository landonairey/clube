# clube — Backlog (2026-10-21)

Notes from 2026-10-21, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md` as Milestone 2's seventeenth pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Collecting

- Picking loose surface stones, whole, by hand is good.
- [ ] Chipping away at a rock face with your hand feels off. Change back to: hands cannot collect hard material. Tools remain more effective for the materials they're designed for (axe for plants, shovel for soil, pick for rock). → **GL119**

## Sorted stacks instead of bins

- [ ] New plan: remove the volume mixing buckets. Instead, auto-sort items into your inventory based on the target you're looking at. Material still mixes within that group. For example: the player looks at grass and digs it, and the grass enters an inventory slot as grass and keeps stacking there. The player clicks on grass and gathers a little dirt from a neighbouring cell: that grass stack is now a grass and dirt mix. The player continues, but now looks at a dirt voxel: the dirt being collected piles up in a slot for dirt, and more dirt keeps stacking there. This keeps the fun of mixing materials and being conscious of purity for crafting, with the quality of life of items stacking in separate, sorted slots based on the material the player looks at. Implement this to playtest. → **GL120**
- [ ] Also change the inventory back to the previous full set of slots (hotbar plus backpack), no separate tool slot: anything can move to or be placed in any slot. → **GL125**

## Misc issues

- [ ] Dropping material on the ground looks broken: for hard materials like stone it starts filling adjacent square piles before finishing the first pile. → **GL121**
- [ ] The pointy stick and sticks are listed with a volume unit. That doesn't make sense: there's no volume for tools. → **GL122**
- [ ] The grass layer is too thick: change it to one voxel thick. Make grass a bio fuel item to put in a campfire or furnace, burning about like leaves. → **GL123**
- [ ] Grass mixed with a little dirt burns worse. → **GL124**
