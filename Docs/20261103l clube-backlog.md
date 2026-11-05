# clube — Backlog (2026-11-03, twelfth)

Requests from 2026-11-03, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Bug: the merchant table floats up in the air after switching between terrain generator types. → **GL341**
- [ ] Draw the 3D grass out as far as the surface rocks are drawn in the terrain. → **GL340**
- [ ] Improve the very large boulders' shape. Make them 2 to 5 times as big, and vary the shape: sometimes flat faces, and features with a smaller base on the ground extending out to a larger waist higher off the surface. They should look less like half spheres placed on the terrain. → **GL342**
- [ ] Switch the default terrain generator to ErodedLandscape. Playing it now sits around 30-40 fps, which is not very playable. → **GL343**
- [ ] Tree chopping physics: chop at a limb and everything above it (the limb, its connected branches and leaves) falls. When chopping starts there, build a 3D poly model matching the marching-voxel shape behind the scenes; once the limb is cut through, swap the voxel mesh for that model and play a fall: it drops and rotates flat, falling to the right of the player's view direction, snapped to the nearest cardinal direction to keep to the grid. Once most of it has crossed the terrain surface, show a brief large plume of dust and swap back to voxels: all the affected voxels, turned 90° about the cut point, dropped straight down onto the terrain surface, wood first and leaves on top, in a flattened pile. The falling limb passes through any tree wood or leaves on its way down (it may be cut from the top of a tree, or next to another tree). → **GL344**
- [ ] Revamp the colour palettes in the game to keep things uniform and groovy. This one looks cool and would need greys and browns added to fill out the material types: https://coolors.co/palette/f94144-f3722c-f8961e-f9844a-f9c74f-90be6d-43aa8b-4d908e-577590-277da1 → **AR1**
