# clube — Backlog (2026-10-21, third batch)

Playtest notes from 2026-10-21, kept as they were given (spelling tidied). They are organized in `Docs/clube-objectives.md` as Milestone 2's nineteenth pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Items and inventory

- [ ] Bug: I crafted 97% quality sticks, then grabbed more wood and crafted 98% sticks, and the better sticks stacked on top of the worse ones. Don't allow stacking of mixed-quality items. → **GL131**
- [ ] Bug: the listed quality of a cassiterite stack was 41% in my playtest (57% dirt, 22% stone, 18% cassiterite, 3% grass). It should be 18%, because what we care about is the target ore itself. Make an ore's quality its share of the target ore. → **GL132**
- [ ] Don't auto-replace a broken tool with a new one; leave that to the player. → **GL138**

## Collecting

- [ ] Bug: digging stone with the hand still works. The hand must not affect hard materials (stone, ore). When digging dirt by hand, nearby stone or ore shouldn't be affected either. The only stone you can grab is the surface-level loose stone piles. → **GL133**

## Stations

- [ ] Change the UI in the campfire, furnace and other workstations to just say "Efficiency xx%" at the top, not quality. After "Efficiency xx%" put what it modifies: Heat/Yield for the campfire. → **GL135**
- [ ] Get rid of the amount of item carried in the campfire and furnaces. Just put a decimal on the amount of produced item: a tin bun can come out as a decimal. → **GL136**
- [ ] The furnace's recipe list should start with the lowest-level item first, which is the tin bun, then the copper ones. → **GL139**
- [ ] Remove the bronze ingot recipe from the furnace; that's made on the Flat Rock or the anvil. → **GL139**
- [ ] Clicking a recipe should grab a full stack of each needed input, not one set. Bug: clicking cassiterite → tin bun only grabbed 3 ore. → **GL140**
- [ ] Furnace bug: I need to look down near the ground to press E on the furnace; looking at its body should work. → **GL141**
- [ ] Furnace bug: I swapped chalcopyrite in for cassiterite and took out the tin bun. The furnace stopped smelting, when it should keep going, now making copper buns. → **GL142**

## Recipes and prices

- [ ] Add a shovel recipe to the worktop. → **GL134**
- [ ] Add a recipe that makes a bronze chisel from a bronze bun and a stick. This gives the unlock path to the Flat Rock crafting top next. → **GL143**
- [ ] One copper bun sells for 14c and one copper ingot for 10c: rebalance. → **GL144**

## Journal

- [ ] Add a second journal for crafting. Chapter one is Crafting, with each recipe's details, 3 recipes per page, with sketches like the ore journal. Chapter two is Mining and what ore vision unlocks at each level. Chapter three is Smelting: which recipes unlock and the different types of furnace. → **GL137**
