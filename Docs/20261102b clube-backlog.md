# clube — Backlog (2026-11-02, afternoon)

Requests from the afternoon of 2026-11-02, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Add sand as a new element. Sand acts like a soft material, but it also falls with gravity to lower ground. → **GL306**, **GL308**
- [ ] Dirt also falls with gravity. → **GL308**
- [ ] Add rivers and streams that generate through the valleys between mountains and flow downward towards a local minimum, where a body of water forms: a pond or a lake, a steady, non-moving body of water. → **GL307**
- [ ] Sand spawns at the bottoms and shorelines of these ponds and lakes. Clay deposits spawn more often alongside riverbanks, and down into ponds and the floors of lakes. → **GL307**
- [ ] To keep the low-poly feel, the water is triangle meshes. Rivers show their motion downhill, against the gradient of the land, by triangles shimmering in turn between blue, light blue and sometimes white, in the direction of the flow. Ponds and lakes shimmer at random: not a direction of flow, just so it reads as a water surface. → **GL309**
- [ ] Bug: at the merchant, the yellow highlight of the Ctrl group selection doesn't always update correctly when adding to or taking from the group. → **GL310**
- [ ] Add a shader to the tops of the rivers, lakes and ponds to show some 3D movement on the surface: it doesn't change the water's volume, only how the surface looks, with ripples and flowing for some 3D visual texture. For more triangles, cut every triangle into 4 smaller ones (in the shader, for the display only), keeping the speckled pattern that shows the direction of flow, with a little glimmer on the larger bodies of water. → **GL312**
- [ ] Digging into the terrain around the water lets the water flow into the new opening. Placing material on top of water destroys that water at the position and replaces it with the material dropped. → **GL313**
