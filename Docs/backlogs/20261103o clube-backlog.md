# clube — Backlog (2026-11-03, fifteenth)

Requests from 2026-11-03, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md`; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

- [ ] Bug: wood that falls on the ground takes more swings to chop through than it did upright in the generated tree (maybe only with the hand axe; test it). And fallen wood doesn't need the falling animation a second time when you chop it on the ground. → **GL349**
- [ ] Leaves that fell from cutting down a tree slowly decay (disappear), at random: a 10% chance to decay every in-game hour. Fallen leaves no longer use the shader that makes them blow in the wind, and no longer make the leaves-rustling noise. → **GL350**
- [ ] Remove the Pickaxe from the merchant and replace it with a Bronze Pickaxe at a 2x markup on whatever we can sell a Bronze Pickaxe for. → **GL351**
- [ ] When you leave a workplace, automatically pull the crafted item as well as the input items. → **GL352**
- [ ] Bug with the torch: it let me build it outside the walls of a tunnel I dug (picture 1, not received). Update the build mechanics so it can be placed on the centre of any single voxel; the player must be able to place it inside mining tunnels. It wasn't letting me build underground (probably looking for a terrain surface to snap to). → **GL353**
- [ ] Make the green specks in malachite's item picture a little larger, so it's easier to tell from stone. → **GL354**
- [ ] Have the merchant buy furnaces as well. → **GL355**
- [ ] When the player touches the campfire, bounce them back the way they came with a short sizzle sound. → **GL356**
- [ ] Add the auto sort to wooden crate inventories too. → **GL357**
- [ ] Bug with grouped selling prices: I highlighted 6 full stacks of 1024 stone (100%) and the total was only for a single stack. The sell total needs to be for the whole highlighted group. → **GL358**
- [ ] Bug with the flat rock: I hammered 6 bronze buns into ingots; after the first 2 I had 2 ingots, but every later bun went nowhere and I still only had 2 ingots at the end. → **GL359**
- [ ] Add tool repair to the flat rock: put the tool and its matching ingot in the input slots (e.g. bronze pick and bronze ingot) and it uses the ingot to fully repair the tool. Hovering over a tool reads out its remaining health and total permanent damage. → **GL360**
- [ ] Edit the player's hitbox so they can pass, running and jumping, between two furnaces built next to each other on the grid. → **GL361**
- [ ] Refining material on a rock adds a very small amount of Blacksmithing xp. → **GL362**
- [ ] Bug: I have a stone item in my inventory that is 0 volume; it shouldn't exist. If it's just a very small amount, round down below the fraction that's visibly shown (0.1) and delete the item. → **GL363**
- [ ] Remove Tab from opening the inventory; E does that now. → **GL364**
- [ ] Add an earlier side quest to reach 1k coins, and move the 10k coins one further down the list. Also add one called Landlord: own 10 parcels of land. → **GL365**
- [ ] When hovering over items, show their value as well. → **GL366**
- [ ] Be able to drag and drop items around in the crate. → **GL367**
- [ ] Show a thin white highlight outline on any equipment, workstation or thing the player can interact with when the cursor is on it within range: the tree stump, a stone boulder, the merchant, the flat rock, the crate, furnaces, the campfire, and so on. → **GL368**
- [ ] Have the sleeping roll match the average slope of the terrain it sits on, so it looks like it's on the ground. → **GL369**
- [ ] Have the sleep fast-forward run twice as fast. → **GL370**
- [ ] Have the fractional part of a stack join the rest of the input at a crafting station: with 6.07 gold buttons, crafting ingots moves 6 to the input slot and leaves 0.07 in my inventory; move the whole 6.07. → **GL371**
