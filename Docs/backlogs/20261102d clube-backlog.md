# clube — Backlog (2026-11-02, night)

Requests from the night of 2026-11-02, after playing build 550, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Bug: water patches in the bottom of the lake bed. → **GL318**
- [ ] In the debug inspector I didn't mean a different gizmo to view water: the water voxels should show up in the same mesh grid as any other material in the chunk. → **GL318**
- [ ] Add a shader on the edges of rivers and lakes against the banks to simulate some foam and wave edges. → **GL318**
- [ ] Water visual bug in the flat lakes: the white triangles flicker too much and look glitchy. Turn the flicker down and tie it into the shader that makes the rolling 3D surface. → **GL318**
- [ ] Visual bug: I dug a channel and the water flowed down the mountain but left a patchy water run. Improve how water runs in channels. → **GL318**
- [ ] Still some issues with how water spawns: a chunk of water appears to grow out of the ground with vertical rectangular edges, which isn't normal. → **GL318**
- [ ] I dug next to a water body near the top of the mountain peak next to spawn and the water never flowed into the hole I dug. → **GL318**
- [ ] Add a wooden bucket tool that scoops up water from the rivers and lakes: it fills with a volume equal to the water it removes from the voxel data. Costs 50 wood to craft, sells for 10c, holds 100 volume. Left click scoops water, a single right click pours 5 volume, holding right click keeps pouring until the bucket is empty. → **GL319**
