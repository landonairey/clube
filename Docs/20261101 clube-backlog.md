# clube — Backlog (2026-11-01)

Requests from 2026-11-01, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] The "+1 Stone" text that pops up next to the cursor doesn't match the amount actually added to the stack in the inventory. Make the text match what's actually added to the stack. → **GL279**
- [ ] Make the pick digging on ore and stone sound more crunchy, like you are breaking rock. → **GL280**
- [ ] Bug with picking up bushes: sometimes the cursor is on the bush but it says "You don't own this land". → **GL281**
- [ ] Change the chest to look like a wooden crate instead. → **GL282**
- [ ] Have the clouds vary more in size, and layer some wispy clouds in with the fluffy ones. Different heights of clouds too. Think cumulonimbus against cirrus and stratus. → **GL283**
- [ ] The moon looks too jagged: draw the outline with a polygon, and draw the phase's inner curve with a low-count polygon too. → **GL285**
- [ ] Add charcoal to the game, made by cooking wood or sticks as an input (not fuel) in the furnace or campfire. → **GL291**
- [ ] Add a torch, crafted from 4 charcoal and 1 stick. It can be placed on the ground or held in the right hand. It lasts 20 minutes from when it's equipped in the hand or placed, until it's extinguished and disappears. Unlit torches can be sold to the merchant; lit ones sell for their price scaled by their remaining health. → **GL287**
- [ ] Add copper tools (copper coloured, with some green edges): a copper pick and hatchet. Balance the line-up of tool usefulness and effectiveness so stone and wood sit below copper, and bronze above. → **GL286**
- [ ] Basic wood and stone tools have durability that breaks the tool at the end of its life. Copper has this too, but can be repaired with more copper ingots. Over time a grey degradation builds up which limits how far a repair can bring it back. Bronze tools the same, but their permanent degradation is slower than copper's. → **GL286**
- [ ] Slow down the xp progression of levels. Stone is worth only 0.1 per volume at the merchant; clay 5c per volume; wood also 5c per volume. → **GL289**
- [ ] When chopping wood with a hatchet, the volume gathered in the inventory shoots up. Is this the volume of voxel mesh gathered, or is a multiplier adding more than what's gathered? What's gathered should match the voxel mesh volume changed in the world, by the tetrahedra volume math. → **GL288**
- [ ] Bug: clouds look too bright at night and lose their polygon line detail. → **GL284**
- [ ] Add a sleeping roll you can buy from the merchant (1000c). It lets you sleep to any hour. Like the bed in Morrowind, once the roll is placed on the ground, clicking it pops up a dial to pick how many hours to rest, showing when you'll wake. The popup has a button to cancel and one to rest. While resting the screen blurs, the popup stays and counts the hours, 1 s per hour, and the sun, clouds and weather move on. When the time is up the popup goes and the screen clears, and play resumes as normal. → **GL290**
