# clube — Backlog (2026-10-19, second batch)

Notes from later on 2026-10-19, kept as they were given (spelling tidied, grouped under headings). They are organized in `Docs/clube-objectives.md` as Milestone 2's eleventh pass; the IDs below say where each one went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Looks

- [ ] **Terrain tiling** — break up the repeating tiling material patterns on the terrain mesh. → **GL65**
- [ ] **Dawn sky** — add a skybox with the sun around dawn. → **GL66** (first look at PK4)
- [ ] **Grass** — add some 3D low-poly grass on top of the grass terrain surface. → **GL67**

## Crafting and items

- [ ] **Instant pickup** — auto-pulling non-stackable items from crafting into the inventory works, but do it faster: have it hit the UI window for only a frame, then move to the inventory. → **GL68** (refines GL59)
- [ ] **Malachite icon** — the malachite item icon should look greenish; right now it looks identical to chalcopyrite. → **GL69**
- [ ] **Ore readout** — the ore item description should put its components on separate lines: name and amount, volume and amount, then one line per % of each component. → **GL70**

## Inventory

- [ ] **Click to move** — in the inventory, clicking an item moves it to the first open hotbar slot; clicking an item on the hotbar moves it to the first open slot of the collapsible inventory. → **GL71** (first pass of N2)
- [ ] **Drag and drop** — press and hold to drag an item to a specific slot. If an item is already there, swap places with it; the same item stacks up to a full stack and leaves the remainder in the slot it came from. → **GL71**

## Mining

- [ ] **Silver and gold hidden** — silver and gold ore also stay locked as an unknown "?" ore behind higher mining levels. → **GL72** (refines GL54)
- [ ] **Mining journal** — Q opens a field journal on graph paper, 3 ores to a page. Entries are blank until the ore is discovered; then the entry fills in with a hand sketch of the ore (black ink outlines, the ore's own colour for its speckles), then a short description: the depth it was found at, its grade, hardness, luster, colour, feel, and crafting uses (once those recipes have been discovered). → **GL73**

## Questions

- [ ] Why does the Ores dropdown in the control panel take a while to open? → answered in the eleventh pass notes (GL74)
