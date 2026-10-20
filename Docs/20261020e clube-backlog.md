# clube — Backlog (2026-10-20, fifth batch)

Notes from 2026-10-20, kept as they were given (spelling tidied, grouped under headings). They are organized in `Docs/clube-objectives.md` as Milestone 2's fifteenth pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Collecting test, decided

- [ ] For now go with the dot for the visual and the gradual sphere for collection. Keep the others wired in the back in case we switch back later. → **GL95, GL96** (defaults)

## Furnaces from the ground

- [ ] Just like we made the campfire into a marching voxel mesh, do the same for the furnace. The first furnace you can make is with stone. → **GL98**
- [ ] Update the collected stones' texture to look like loose cobbled stones. This will serve as the furnace texture and when you drop stones on the ground. → **GL97**
- [ ] After that, the next furnace you can make is with clay, with better (hotter) stats than stone. → **GL98**
- [ ] Make that furnace as tall as the original 3D design one. → **GL98**

## Volume material management test

Inventory management. → **GL99**

- [ ] A single tool equipment slot and 4 material slots
- [ ] Volume-based slots and item slots (start with one volume slot that mixes all mined voxels together)

Volume mixing ramifications to work out:

- [ ] How does crafting work: a quality multiplier based on the material mix. E.g. a clay furnace from 100% clay gives the pure bonus, but one made with 76% clay, 15% dirt, 9% stone yields 76% effectiveness. → **GL100**
- [ ] How does dropping material work: a weighted random chance, based on the mix, of which item type is dropped next back into the terrain voxel grid. → **GL101**
- [ ] Potential frustration with the collection mechanic: the mesh material blends between materials and it's unclear which one you're getting. Test with a shader that draws abrupt transitions from the material of one voxel to the material of another. → **GL102**
- [ ] Still potential frustration after that: clicking on one voxel impacts the corners of the neighbouring voxels, which changes their volume, and you end up collecting volume from neighbouring voxels. Solve by making the material id stored in shared corners? Not sure that would work cleanly. → **GL103**
- [ ] Smelting low-grade ore: when you collect ore it might be 90% host stone and 10% target ore. That's fine; let it process at a yield debuff. When you have stone in your volume slot and collect cassiterite ore that is 90% stone and 10% cassiterite, it combines, and now you have a mix of over 90% stone and under 10% cassiterite. But when you drop this on the ground you don't drop pure cassiterite ore: it keeps the original 90/10 composition. → **GL104**
- [ ] Separation techniques will be unlocked later; we will use a basic method of crushing ore with rocks to crudely separate ore from host material. → **GL105** (later)
