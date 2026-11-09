# clube — Backlog

Ideas captured outside the code session, to be scoped and scheduled against the milestones in `docs/clube-objectives.md`. Checkboxes are for tracking; nothing here is committed to a milestone yet unless noted.

---

## Tech / Voxel Lab

- [ ] **Single-byte terrain data** — store terrain map (density) values as single bytes to keep data tight and simple.
  - Note: if density is signed around the iso-surface (negative = air, positive = solid), use `sbyte`, or `byte` with 128 as the zero point.
- [ ] **Height map export** — export the chunk map as a height map image file (bytes map directly to grayscale pixels).
- [ ] **Exe build** — test building the project as a standalone exe that runs the voxel lab.
- [ ] **Educational code snippets** — in the lab view, show code snippets alongside the marching cubes animation stepping.
- [ ] **Gravity multiplier** — debug adjustment for a gravity multiplier.

## Performance Tests

- [ ] **Burst jobs** — code test of Burst-compiled jobs.
- [ ] **GPU marching cubes** — run marching cubes on chunk creation as a compute shader to speed up world render time.

## Procedural Generation

- [ ] **Chunk shape comparison** — compare tall chunks (Minecraft-style) vs perfect cube chunks stacked vertically to fill elevation.
  - Note: affects the mountaineering skill; stacked cube chunks allow tall peaks without paying for empty sky everywhere.
- [ ] **Ore & gem rarity** — abundance should loosely follow real composition rates in Earth's crust.
  - Note: the real spread is enormous (iron ~5% vs gold at parts per billion); a log-scale compression keeps the real ordering while keeping rare ores findable.

## UI

- [ ] Mini map HUD element
- [ ] HUD info for ore gathered
- [ ] Inventory screen
- [ ] Skill tree screen
- [ ] Settings menu to adjust player controller key binds

## Mining Feel

- [ ] **Hardness & tool upgrades** — material hardness plus an extraction-tool upgrade path for increasing extraction efficiency.
- [ ] **Swing animations** — test different ore cracking / pickaxe swinging animations.
- [ ] **Dropped material behavior** — compare the gameplay feel of loose dirt/ore clumping back into the terrain mesh vs dropping as items you can pick back up.

## Core Loop & Systems

- [ ] **Main game loop** — collecting → crafting → selling. Explore options to make this more enjoyable.
- [ ] **Crafting system** — crafting steps require specific tools, and workbenches/equipment (e.g. anvil).
- [ ] **Item health (state of health)**
  - Tools, weapons and armor lose health with use; faster operations or harder usage degrade them more quickly.
  - Repairable at the cost of some time and base materials.
  - Slow accumulation of irrecoverable damage prevents repairing to 100% — modeled on battery capacity retention over cycle life.
  - Repair cost driven by a repair, blacksmith, or general crafting skill.
  - Implementation sketch: two values per item — current durability and max capacity (SOH). Repairs refill durability up to max; max only ever decreases.
- [ ] **Recycling** — break items/tools back down to base materials.
  - Yield determined by a recycling skill, or possibly a general crafting skill.
  - Yield also scales with the item's state of health (worn items return fewer materials).
- [ ] **Ore value** — value calculated from the weight of the valued element locked in the mineral; ore handled as volume from marching cubes extraction.

## Skills & Progression

- [ ] **Mountaineering skill** — higher skill lets you walk up steeper gradients and climb more mountains.
  - Note: compare surface normal angle against a max slope that rises with skill; can drive `CharacterController.slopeLimit` directly.
- [ ] **Skill books** — books hidden in merchant shops / blacksmith camps that increase a skill after reading.
- [ ] **Coffee** — consumable giving a daytime stimulant boost.

## World & Economy

- [ ] **Meteor event** — rare event where a meteor crash-lands on the terrain; good source of high-quality iron.
- [ ] **Gemstones** — added as a drop chance from certain ores.
- [ ] **Jewelry crafting** — craft jewelry from precious metals and gems, alongside blacksmithed weapons and armor to sell.
- [ ] **Mining rights** — land area unlock mechanic: player pays a fee to unlock mining rights to areas.
  - Idea: fees set by the nearby city/lord and tied to your standing with them, feeding the city-dominance goal.
- [ ] **Fauna & flora collection system**
- [ ] **Town name generator**
  - Pattern: `[Descriptor] [Geological feature] [Settlement type]` — e.g. *Wind River Village*, *Stone Mountain City*, *Gold Coast Outpost*.
  - Descriptors: colors, adjectives, and resource types — copper, silver, gold, steel, iron, bronze, brick, clay, cedar, maple, oak, etc.
  - Settlement types: Camp, Outpost, Village, Town, City.
  - Suffixes when appropriate: -ville, -shire, -stead, -borough (attach to a single word, e.g. *Copperville*).
  - Ideas: settlement type tracks town size/economic rank (Iron Creek Camp → Iron Creek City); natural resources (copper, clay, cedar) hint at nearby deposits while manufactured ones (steel, bronze, brick) mark what a town produces; -stead for the smallest places, -borough for market towns, -shire for regions.

---

## Parked Ideas

- **Grandfather's cottage** — an old cottage that, as part of the story line, turns out to be your grandfather's house. A mountain climber game demo is still on his computer; you must figure out how to turn it on/unlock it.
- **Evolving title music** — menu music gains instruments as you unlock things: leather making adds drums/percussion; brass or another metal adds stringed instruments.
- **Timepiece Easter egg** — side quest to gift a timepiece to the local ore miner; afterwards he mines on beat with the title-page music.
- **Building system** — parked (was in Milestone 3 / Alpha; status there undecided).
- **Possible end goals to test**
  1. Make a city into the largest economic power
  2. Advance to space asteroid mining
  3. Reach X amount of money
  4. Gather every type of ore / mineral / gem
  5. Unlock everything in the skill tree
