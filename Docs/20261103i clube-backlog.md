# clube — Backlog (2026-11-03, ninth)

Requests from 2026-11-03, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Bug: after filling a bucket and doing several small pours, the water eventually gets stuck on the hill next to the lake. After water stops falling, every later pour stops flowing properly. Is a queue of water processing getting hung up by the lake? → **GL333**
- [ ] Bug: clay dropped under water shows the drying steam. Suppress it, and don't let clay dry while it's wet. → **GL334**
- [ ] Bug: stream generation has large gaps (seen at -178, 294, -292). → **GL336**
- [ ] One bucket scoop takes 31 water, which seems a lot. Make it behave like the shovel: with the same area of impact, scoop up all the water, reducing those voxels' water to zero on every scoop. → **GL335**
- [ ] Bug: ponds and lakes act like infinite water sources; scooping again and again, even a small pond always fills back in. → **GL333**
- [ ] Water still behaves a bit gummy, clumping on the way down a hill: a full bucket dump reaches the bottom only as separate clumps. How can we improve this within playability? Should we implement Minecraft-like source blocks and flowing blocks? → **GL337**
- [ ] Question: with lakes and ponds finite, won't rivers eventually drain them? → **GL333** (answered: rivers hold their level both ways; a lake drops at most to its outflow river's level)
- [ ] Idea: the terrain carries a moisture field, a reservoir in every voxel that rain fills. Valleys collect runoff into channels that draw on the soil's moisture like a water table, feeding the rivers instead of a fixed source, and they run dry if it doesn't rain again. Would that work? → **WT1** (new)
