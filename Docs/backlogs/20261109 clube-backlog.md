# clube — Backlog (2026-11-09)

Notes written 2026-11-05 (`20261105 Notes.txt`), kept as they were given (spelling tidied), in the order of the week's batches (A–I). They are organized in `Docs/clube-objectives.md` under "Week of 2026-11-09"; the IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

## Done before this snapshot

- [x] Slightly increase the font for all in the HUD, it reeks of AI generated information. Double the size of each inventory slot on the screen for all inventories, and increase the RPG level bars' sizes. → **GL427**

## A — Bugs and quick tweaks

- [ ] Have the game start at 7am when you start a new world. → **GL428**
- [ ] In the pause menu (Esc) display the game version number below the popup box. → **GL429**
- [ ] An unlit campfire does not bounce the player back. → **GL430**
- [ ] Bug: 99% quality cassiterite ore produced a 100% tin bun. → **GL431**
- [ ] Bug: a leaf item at 0.14 amount burns forever in a furnace. → **GL432**
- [ ] Bug: when you Ctrl-click to highlight all item types to sell, highlight all items of the same type regardless of the quality. → **GL433**
- [ ] Let the shopkeepers allow Ctrl to highlight all in a group sell. → **GL434**
- [ ] Bug: white text hard to read on the sand item slot picture. → **GL435**
- [ ] Bug: the white text next to the cursor is hard to read when it overlays the white-blue buildable hologram. → **GL436**
- [ ] Bug: the active quest dialog box in the top left HUD should stay open when you interact with inventories of objects or open the quest log. Don't hide this. → **GL437**
- [ ] Bug: text clipping in the journals now with the larger font. → **GL438**
- [ ] Remove the nondescript "Pickaxe" item from the game. We only want the Copper or Bronze one right now. → **GL439**
- [ ] When you make crushed ore it's still mixed with other stuff, sometimes other ore. If you refine 50% crushed chalcopyrite to 100% and by doing so separate out residual silver ore, add that silver ore to your inventory as crushed silver ore, not raw silver ore, because it's already gone through the crushing process. → **GL440**
- [ ] When refining at the boulder, have the orange text pop each time a cycle completes and begin to fade, then pop again at the next cycle, so the player sees it refreshing even when the text says the same thing. → **GL441**
- [ ] In the FPS debug box there is a total brush volume change metric. Change it to say "Volume" and show the player's total lifetime volume of materials collected and placed in the marching voxel system. → **GL442**
- [ ] Bug: have the clay furnace also display the output yield like the stone furnace. Make sure the brick and firebrick furnaces do the same. → **GL443**
- [ ] Bug: crafting one item left it in the output slot; clicking a different recipe swapped in the right inputs but failed to craft because the output slot was occupied. When switching crafting/smelting recipes, auto pull the output items to your inventory. → **GL444**
- [ ] Bug: head glitch in the caves often gives the player X-ray vision through the soil. Prevent this with the camera behaviour. → **GL445**

## D — Onboarding

- [ ] First-timer playthrough reports it's confusing to know that the stump, boulder and campfire are crafting stations. Fix 1, visuals: a sketch icon of the boulder next to the first quest (shape a stone into a hand axe), a tree next to the gather wood quest, the stump next to the build quest for the first campfire. → **GL446**
- [ ] Fix 2, tips in a separate box below the active quest dialog: → **GL447**
  - Pick up a stone: "Loose stones on the surface can be collected by hand, hard rock stone needs a tool to gather"
  - Craft hand axe: "With Stone equipped in the hotbar, right click on a boulder to shape it into a Hand Axe"
  - Gather wood: "Use the Hand Axe to cut down a Birch tree, Maple trees require a better tool"
  - Build campfire: "Increase your Crafting skill by making more hand axes or crafting items on the Stump, then use the Stump to build a Campfire"
  - Gather first ore: "Tin ore appears at the surface, use a tool to gather it. Shovels work best to clear softer dirt and grass while the Stone Pick works well on hard ore"
  - Smelt into bun: "Use the Campfire with a fuel source to smelt the Tin ore"
  - Copper and tin into bronze: "Copper ore is buried below the surface. Use tools to collect it. If you get stuck you can climb out of a hole"
  - Chisel flat rock: "Use the Chisel to create a 3x3 working area on a solid Stone surface"

## B — Crafting and stations

- [ ] Improve the crafting GUI for the stump, campfire, flat rock and furnaces: recipes in a list on the left; on the right, enlarged input slots, recipe details and output slots, a dedicated half for the active recipe. → **GL448**
- [ ] Same stations: if a selected recipe is missing ingredients, show a small red X where their count would be in the input slot, and a message underneath saying you need X more of <item>. → **GL449**
- [ ] In the stump UI make locked recipes more obvious with a lock symbol to their left. In every station's list only show what the player can craft now and what's locked behind their next skill level; hide higher levels to cut clutter. → **GL450**
- [ ] Missing a place to craft copper tools. Add them to the stump. → **GL451**
- [ ] Upgrade from the stump: a workbench, made from 100 wood boards at the stump (the stump turns 1 wood into 1 wood board). It's a buildable made of wood board material, a rectangular worktable block on the ground, made from real voxels like the furnace, and can be destroyed by accident. → **GL452**
- [ ] Add a firebrick recipe to the stump made with clay and sand. → **GL453**
- [ ] Have the flat rock also crush and refine, at 2x the batch size of the boulder. → **GL454**
- [ ] Add a Goldsmithing skill that will be used to craft jewellery. → **GL455**
- [ ] Coffee, a non-stackable consumable bought from the trading post merchant for 50c. Selected in the hotbar, right click to drink: 25% quicker tool cooldown for 3 hours. More coffee doesn't stack the effect but resets the 3 hours. → **GL456**

## C — Prospecting and hit effects

- [ ] Show mixed item materials by Prospecting level. At first only the main material shows (for ores, list the ore even when the host rock is the larger share), and exact % are replaced by coloured grades: white "PURE" 100%, gold "High Grade" 90-99%, green "Good" 75-89%, yellow "Medium" 50-74%, red "Poor" under 50%. Each level reveals one more component (others are a question mark); exact % at Prospecting 10. → **GL457**
- [ ] With bare hands (no item selected) right click a material in the ground to prospect it for a little xp; it tells you what your Prospecting level allows. An ore you haven't learned stays a question mark but gives more xp. No xp for the same voxel twice. → **GL458**
- [ ] A rock crushing sound when crushing ore, an animation of your hand smashing the ore at the boulder, particles of crushed fragments flying out, and now and then a few sparks. → **GL459**
- [ ] When mining, debris sometimes lands in a huddled group. Never do this: always splash outward from the mined area, with sparks now and then. → **GL460**

## I — UI and settings

- [ ] A minimap in the top left; move the FPS debug box below it. → **GL461**
- [ ] Graphics menu presets — 16:9: 1080p, 1440p, 2160p; 21:9: 2560x1080, 3440x1440, 5120x2160; 32:9: 3840x1080, 5120x1440, 7680x2160. Resizing the window changes the resolution. A Window / Full screen toggle; in Window mode, bordered or borderless. → **GL462**
- [ ] Change the bright yellow parcel boundary fence to a natural maple wood fence at the same height above the terrain as now. → **GL463**

## H — Sky and nature

- [ ] Bright for more hours than dark: like a solstice, 16 hours of light, 8 of dark. → **GL464**
- [ ] Fireflies at dusk. → **GL465**
- [ ] River trout that occasionally jump and splash: more in the morning, less in the afternoon, not at all at night. → **GL466**

## G — Trees and climbing

- [ ] Climb steep surfaces (trees, a mining hole): press and hold Space against the wall for a slow ascent at 25% walk speed. → **GL467**
- [ ] Climb trees to remove upper limbs first, with a tree cutting overhaul: lower Woodcutting levels cap the wood volume a felled tree yields, so beginners climb and cut smaller branches one at a time. Level 0 caps at 10 volume, each level +10% compounding. → **GL468**
- [ ] When a tree is felled, leaves kick up with the dust cloud and shrink quickly until they disappear. Make the dust cloud bigger, to hide the change from the 3D leaf balls hitting the ground to the flattened voxel tree. → **GL469**

## E — Trading post

- [ ] A wooden sign outside the town center, which is called a trading post from now on, named with the naming generator from the objectives list. → **GL470**
- [ ] A disposition with each shopkeeper, 0-100, starting at 10. Every 100c of trade moves it up 1. At 100 prices are fair; each point below is a point of markup on what they sell and a point off what they pay. → **GL471**
- [ ] The town's anvil and furnace become usable once the blacksmith's disposition reaches 25. → **GL472**
- [ ] With 10k coins the trading post merchant offers to sell you the trading post and its parcel for 10k: you can dig there, and it gives 500c a day in sale profits. → **GL473**
- [ ] The tin mine becomes an open pit, not a staircase (the node is at the surface): clear some grass at the node and the miner digs it out over time. → **GL474**
- [ ] A secret cave under the town with a few wooden crates, one torch and a new character, the thief. Hide some valuables from the shops in the crates. → **GL475**

## F — Travelers

- [ ] After the 5th night a lone adventurer appears on the peak of the mountain nearest the starting parcel, with a campfire visible at night. They buy and sell everything, with a 10k coin budget. → **GL476**
- [ ] A travelling trade cart every 2-3 days between the trading post and a more distant town (at least 10 parcels away), on a roughed-in cart path. Goods in crates on the cart can be looted. → **GL477**
