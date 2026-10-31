# clube — Backlog (2026-10-29)

A playtest report and requests from 2026-10-29, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] If the player is more than one parcel grid away from an owned parcel, simply say in text "You don't own this land". → **GL179**
- [ ] The stone furnace and clay furnace don't have a sketch outline in the journal. → **GL180**
- [ ] At the beginning of the game everything has a sketch outline entry in the tool journal, even at level 0. Change this so things that are unlocked have full data and picture in the entry, things one level away from being unlocked have the sketch outline with only the item name and the unlock level, and things more than one level away have just a big question mark with no other data. → **GL181**
- [ ] The J journal has a Mining chapter. It isn't needed, because there is already the mining journal on Q. → **GL182**
- [ ] We get to nearly Mining level 3 by simply picking up loose rocks on the surface. Adjust the balance so it takes roughly 10x the work for the same level. → **GL183**
- [ ] Change the xp needed per level: right now it's the same amount per level; have it go up by 50% each level compared to the last. → **GL184**
- [ ] Change the build highlight outline of the stone and clay furnace to outline just the final mesh edges; right now it looks like it highlights every voxel and the shape is too busy. → **GL185**
- [ ] Bug: refining crushed cassiterite at the stone boulder went to 0% quality after the first refinement, and later refinements lowered the stack's volume. Maybe it was moving the cassiterite back to the uncrushed ore stack? That's wrong. → **GL186**
- [ ] Bug: sometimes on opening the ore journal a discovered ore's description is blank; closing and reopening fixes it. → **GL187**
- [ ] Bug: stone stacks to 1024, but a mixed stack shows slightly more, 1024.7. → **GL188**
- [ ] Bug: in a tree trunk, leaves seem to replace wood on the way up. Make sure wood replaces any leaves material ids when a tree generates. → **GL189**
- [ ] The progress bar for refining a large stack (400 clay) on a boulder is far too slow. Make the progress bar repeat faster, with small updates, like taking out 1 volume of impurities every 1 second. → **GL190**
- [ ] Add a way to split a stack in the inventory, and a way to split a stack going into a crafting station as an ingredient (like wood to fuel a furnace). → **GL191**
- [ ] A higher quality clay furnace with crushed cassiterite gives 118% yield in smelting. Rebalance: material can't come from nothing. → **GL192**
- [ ] Make the hatchet less good at collecting leaves, so you don't accidentally get a lot of leaves with your wood. → **GL193**
- [ ] Bug: when a stack of raw ore gets below 1 volume, pressing on a boulder changes from crushing the ore to refining it. → **GL194**
- [ ] In a crafting screen you should still be able to drag and swap items in your inventory while it's open. → **GL195**
- [ ] Make the bronze tools perform better than the stone ones. → **GL196**
- [ ] If both furnace input slots are loaded with two stacks of ore, make sure it eventually pulls from both. → **GL197**
- [ ] Have the merchants buy sticks. → **GL198**
- [ ] It's awkward selling things and being left with a fraction of a stack; just use decimal coins. → **GL199**
