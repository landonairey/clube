# clube — Backlog (2026-11-10c)

Playtest notes given 2026-11-10 (third round), kept as they were given. They are organized in `Docs/clube-objectives.md` under "Playtest 2026-11-10c"; the GL IDs there say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

20261110c Playtest Notes:

Fixes
-Disable the P shortcut in the tutorial from launching into debug mode
-Change the workers in the tuturial to sleep in their beds inside and not just on the grass outside
-Change the workers in the tuturial to have the same mining sounds/ particle effects/ tool area effects on the stone as you the player does. Also make them actually walk up to spots to mine and walk over to the next spot to mine instead of just teleporting. This needs to feel more real.
-Change the crates in the tutorial to the actual wooden crates in the game with an inventory to them
-bug, the crew lead did not have a discussion topic button to bring up discussing ore. Have the option to discuss ore or hand in material or exit the dialog when pressing E on the crew lead. (after discussing ore for the first time just grey out the button and not be able to click it again).
-If you play the tutorial "by the rules" it can take a while. Ramp up the quota by simply doubling the starting value for suspequent days. 1024, 2048, 4096, 8196, etc). Along with this have a strict 9am to 5pm policy (player cannot dig with their hands or a tool outside of these times).
-Bug, by about day 4 or 5 the workers in the tutorial did not advance digging the wall position at all.
-Bug, the grass edge lip coming up the start of the mountain wall was removed but when mining the stone there I get green particle effects and get grass collecting in my ivnentory. Fix the material in the terrain here to actually be stone, not just look like it.
-Int he crafting menus when you show input item quantity as a fraction over the recipe quantity, show that in the bottom of the item icon slot. So for example making Birch Wood into sticks I have 90.74 Birch Wood, below the birch item picture in the item slot write out the text "90.74/1"
-Bug, river trout are still not existent and aren't jumping, aren't making any noise. I can't find them.
-FPS tanks to 20-30fps after roaming around a bit in the world. Even setting render distance down to 8 chunks resulted in only 35fps at a simple 16:9 resolution. Find out the bottle necks here to fix.
-Change the 3D render of the shovel to be a spade to match the icon image. Right now the flat shovel type does nopt match the kind of work we are using it for!
-Bug when refining on a rock, the text stil says what % quality value the refined material is currently at even if you haven't unlocked that visibility with prospecting yet.
-When building a wooden crate it says the crate high HIGH grade with some % leaves, % grass (leftover from the grade of wood used. Shed that information when crafting a crate, it's doesn't make sense to say that the crate is made of 0.1% leaves.
-Maple Wood quality should folow the same behavior that birch does, use the POOR, to PURE mechanics.
-bug, on day 2 I didn't see the early bird bonus anymore. Make sure this happens every day


Features
-Change the attire on the crew lead to lookmore wealthy and have the workers in the tutorial have tattered clothes
-In the tutorial when you steal an item or fail the quota, have the player freeze and the spawn the crew lead next to them, rotate the player's camera towards them as they tell you you're getting kicked out.
-Add a new tip after gathering wood to increase crafting skill to make the campfire by crafting sticks at a Stump or Hand Axes on a boulder
-Add a new tip after building a Campfire to increase crafting skill to unlock making tools
-When a road crosses a river build a bridge using two small cooble stone footings, maple boards across, and the wooden fences as side rails that we see marking parcels. Give the fences a hitbox this time though.
-Crushed stone should show a POOR, MEd, GOOD, HIGH, PURE rating becasue it is a material that can be mixed. Add a texture to this that resembles crushed stone and make this a material that you can drop on the ground. It piles up and falls similar to Sand.
-When prospecting level 1 text comes up, say you can now see the second item in the material mix
-Add a Skill notebook tab to the journals. Each Skill has it's own chapter with details about what you've unlocked at each level so far for the skills. There is a preview of what is unlocked at each of the net skill levels that is next up to unlock but nothing is discussed beyond that.
-Change the descriptors to items that are in a material mix. Change non-primary material items to use descriptors of Dominant for 50% or more, Abundant for 25-49%, Moderate for 10-24%, Minor for 1-9%, Trace for <1%. They don't need any text coloring. ex. I have 188 HIGH Birch Wood with the Birch Wood Component being written as HIGH GRADE, that makes sene becasue that is the material of interest. But the other component POOR GRASS and POOR LEAVES do not make sense in that context. 
-Tweak stump crafting behavior to clear last recipies when you approach it again. When you approach it to open it there should be no recipe currently selected.