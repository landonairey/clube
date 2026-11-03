# clube — Backlog (2026-11-02, evening)

Requests from the evening of 2026-11-02, after playing build 548, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Some issues with the water surface extending out of the ground surface. → **GL315**
- [ ] The pointy cone for a fire is trash: use a group of multiple low-poly shapes of mixed white, yellow, orange and red to mimic the dancing flames of a torch, while staying on brand with the visuals of the game. → **GL314**
- [ ] Bug: the sand visuals look like wood. → **GL317**
- [ ] Bug: when I dug through land to have water spill out, the water spilled out of the freshly dug channel correctly but continued to spread out at the same height, extending from the top of a mountain. The behaviour should be more like Minecraft, where water falls down voxel drops. Ideally water meets up with other water eventually as it takes the gradient descent of slopes. → **GL315**
- [ ] Related: fix up the look of water as well. It really should start with being made as a material in the marching voxel terrain; it should not look like a bunch of floating 2D sheets. When I click on a chunk in the debug view, it also needs to highlight which voxels are the water ones. (Later on we will use a bucket to collect water, which in turn pulls volume of water from the marching voxel data.) → **GL315**, bucket in **GL311**
- [ ] Water visuals: the smaller triangles look good, but update the shimmering triangles to actually be the smaller ones in the mesh surface. → **GL315**
- [ ] Adjust the water colour to be more desaturated to fit the look of the trees and terrain, and add a bit more transparency so it doesn't look like a hypnotic blue disco. Slow down the flickering shader, and have the slope affect the speed, so steeper runs are faster and gradual slopes slower. → **GL315**
- [ ] Add the swimming player motion, and tint the screen to look underwater when swimming. → **GL316**
