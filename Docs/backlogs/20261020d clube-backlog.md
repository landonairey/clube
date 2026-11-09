# clube — Backlog (2026-10-20, fourth batch)

Notes from 2026-10-20, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md` as Milestone 2's fourteenth pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Collecting mechanic testing

Visual feedback, cursor types, highlights. → **GL95**

- [ ] Small dot cursor only
- [ ] Small dot cursor and black voxel outline
- [ ] What we have today, highlighting faces
- [ ] Soft glow cube corners
- [ ] Translucent area-of-impact sphere

Collection. → **GL96**

- [ ] What we have today: binary voxel grab and multiple hits
- [ ] Area-of-impact sphere + tool-based impact amount (the hand could move the corner values by 0.2, but a tool could move the whole value of 1). Interpolated edges show gradual terrain impact.
- [ ] Area-of-impact sphere + binary grab (hands a smaller sphere, tools a bigger sphere; every corner in the sphere changes from high to low)
