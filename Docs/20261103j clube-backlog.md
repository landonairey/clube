# clube — Backlog (2026-11-03, tenth)

Requests from 2026-11-03, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Reference: runevision's "Fast & Gorgeous Erosion Filter Explained" (https://www.youtube.com/watch?v=r4V21_uUK8Y, blog post https://blog.runevision.com/2026/03/fast-and-gorgeous-erosion-filter.html). It makes terrain features without needing to generate a whole world. Rip up the landscape generation, or make a new generator type, and try it, tied into stream creation. → **P12a**
- [ ] In the new terrain, use the branching valleys between the mountain peaks as the beginnings of rivers. The rivers flow down to nearby lakes and ponds and merge with other rivers. → **P12b**
- [ ] Keep the same water flow behaviour we have now, make all water finite, and feed it from the moisture level in the chunk. There's no rain yet, so keep each chunk's moisture always full and the rivers never deplete it. → **WT2** (first pass of WT1)
- [ ] Make a grayscale height map of the terrain: does it look natural, interesting, and scaled right for good gameplay? → answered in **P12a** (the smooth massif base, strength 0.15)
