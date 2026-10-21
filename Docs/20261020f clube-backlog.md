# clube — Backlog (2026-10-20, sixth batch)

Notes from 2026-10-20, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md` under Milestone 2's fifteenth pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Mixing feels bad

- [ ] The mixed volume material feels like bad gameplay when you mix wood, leaves, grass, dirt and stone. These things wouldn't normally be mixed if you were digging in person. It's confusing to be digging around collecting resources and then realize you can't craft anything because the concentrations are so low for a given resource, and then you can't feasibly separate them. → suggestions given in the session (tool-selective collecting, separate bins per kind, take the material under the dot first, a quality penalty with waste instead of a minimum, hand sorting / sieving / washing); the hand and tools below come first.

## The furnace

- [ ] The furnace looks bad: change it to a more tapered, short-chimney shape that looks like the primitive historical first furnaces. → **GL98** (changed)

## Collecting by hand and with tools

- [ ] Change the collection method for the hand to only modify (pick up) the voxel the player is looking at. → **GL106**
- [ ] Later, the tools: a larger area of impact than the hand, with a high voxel-corner impact number for the material types they're effective for. E.g. you can shovel the dirt in the ground, which might change the corner values of dirt by 1 but those of stone by only 0.2, within the tool's area of impact. → **GL107**
