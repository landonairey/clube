# clube — Backlog (2026-11-10)

Playtest notes given 2026-11-10, kept as they were given. They are organized in `Docs/clube-objectives.md` under "Playtest 2026-11-10"; the GL IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

Fixes

- **GL517** [M] Title screen still looks off: the terrain needs a more interesting shape, near the edge of a mountain, with a river cutting through. The trees are misshapen: use the actual fully generated trees. The campfire flames aren't the same as the real in-game ones, and it's missing smoke. Use the same terrain generation as the game for this mini world.
- **GL518** [M] Main menu buttons are missing the click sounds added before.
- **GL519** [T1] When typing your name for the crew lead, WASD still moves the player. Disable character control until the name is entered.
- **GL520** [T1] 3D grass and bushes spawn inside the mining corp building.
- **GL521** [T1] Crates around the tutorial say "take what's inside", which is misleading; they should say "Do not take". Crates can be opened, but it only counts as stealing once an item goes into your inventory. Fill the crates with various valuables. Change the crew lead's line from "And don't touch anything" to "And don't take anything that isn't yours".
- **GL522** [T2] You can wear out the company pick and never get a new one (solved by T2's tool shed).
- **GL523** [M] The copper pick's 3D model should have a copper head with green (patina) colour, to match its icon.
- **GL524** [M] Near a full stack of Stone, the next hit starts a new stack (1023.88 and 49), and the hit after that fills the first (1024 and 92.63). Fill the stack fully before starting a new one.
- **GL525** [T1] Handing materials in to the company also takes your mining pick. Take only materials, never tools.

New features

- **GL526** [T1] A sign on the back of the tutorial building listing the Mining Corp rules: "No Stealing", "Fill your quota".
- **GL527** [T1] Suppress skill levels in the tutorial: the player can't see them or earn xp there.
- **GL528** [T2] While mining, the crew lead says "Report back to me if you find anything interesting". After the player finds an ore, talking to the crew lead offers "discuss the <Ore Name>", and he always replies "We don't care about any of that, we only do Stone here. Now get back to work."
- **GL529** [T1] Disable climbing in the tutorial.
- **GL530** [T1] Put the player's name on their assigned bed.
- **GL531** [T2] Some other workers mining the mountain wall.
- **GL532** [T2] A tall fence along the sides of the building, so the player can't go around to the front.
- **GL533** [T2] A tool shed with a "Tool Shed" sign. After breaking a pickaxe, the player can get a new one there, but can only carry one at a time.
- **GL534** [T2] Quota: turn in 1024 Stone on day 1 before 17:00, or be kicked out. Each day adds 1024 more (2048 on day 2, and so on).
