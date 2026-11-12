# clube — Backlog (2026-11-11c)

The user's notes of 2026-11-11 ("20261111 Notes": map gen overhaul, 3D model overhaul, animals and AI), kept as they were given. Organized in `Docs/clube-objectives.md` under "Bounded world, models and animals"; the list at the end says where each part went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

Read these three sections for implementation, also implement any sections in the main clube-objectives doc that are required to support these below items. You must develop these features that connects in with the existing game framework and infrastructure that we've developed so far. When we create this 5km x 5km map, LOD render distance must be good enough to see across the whole thing in good weather from a tall mountain with acceptable fps. Develop modules in a way that adding new item types, skill types, settlement types, etc is easy to do so. During the fable 5.2 promo period the plan is to use the remaining balance this week to run this and then use 50% of next week's usage on Monday in a second shot. Scope the work and execute in a token efficient way. 

Section 1. Map Gen Overhaul
Infinite, procedural generation is lazy. It leads to a sprawling, sparsely populated, vast void of repeatitiveness that's not carefully curated and is not interesting to explore it. There's also something to be said about giving bounds to a player, giving constraints to a player and having them optimize within the constraints that provides more value to what's currently there. Versus having something that's not valuable due to a sheer vastness. In an infinite, procedural generated world your impact in that world has to be equally large, thus base building turns into a show of who can make the largest thing in order to feel like it's significant. Versus a world that has a fixed outline means that a reasonably achievable finite player impact has a meaningful impact.

Onto the map gen, so target a roughly five by five kilometer map size. The borders will have some sort of boundary that can be arbitrarily chosen at startup. The world generation, the simplest case would be an island just surrounded by an endless ocean. So the edges of the map are dictated by when the land mass rolls off to an ocean biome, and that ocean will just continue on forever if the player starts to swim there. The other edge types could be an endless sprawling desert. It could also be a cliff that drops off into a void of nothingness. Or it could be a mountain ridge too tall to actually climb.

Based on resources in the map there are different types of settlements. Every settlement (apart from a camp) needs to be connected by some sort of land-based path. And that's a footpath, which is a small path to walk or go by horse, or by dirt cart path which is wider and flatter, or a paved cart path that is more prepared path meant for 2 way traffic carts. Carts are both horse drawn and hand pulled.

Settlement type - icon, minimun connection, parcel size, NPC population size
Camp - tent, no paths, <1, 1
Outpost - house, walking, ~1, 2-4
Village - houses, dirt cart, ~4, 4-8
Town - townhall, dirt cart, ~9, 8-16
City - tower, paved cart, ~16, 16-24
Capital - castle, paved cart + ocean, >16, 24-32

There's also a special type of camp, which is a thieves camp. And they are Pirates that hijack cart paths, trading paths, they also steal from towns, and they steal from the player. They can also be water-based pirates that go out and intercept sea vessels and/or river boat vessels for their goods.

Quantity in world
12x camps
4x theif camps
12x outposts
10x villages
8x towns
4x cities
1x capitals

Economy specialty: settlements spawn based on local resources to them and generate shops that the player can access that trade sub categories of goods based on their local economy. let's have a look at them. Every below shop specialty needs to occure at least once in the world. This influences settlement spawning logic.

Base Supply Chain - sub category
Lumber - building supplies (wood, boards, tools like hatchets), carpentry (wooden crates, boards, hand carved flute, goods not yet defined), tool componentry (sticks, chisel, axes, shovel)
Ore - Mining (buys/sells Raw Ores and pickaxes), Crushing (buys Raw/crushed Ores, sells crushed ores of varying quality), Smelting (buys raw/crushed ores and furnaces, sells buns/ingots/furances of varying quality), Blacksmith buys ingots, anvils and sells crafted goods, anvils (sub sub categories of Armory, Weaponsmith, Toolsmith), Goldsmith gets into jewelry making with gold, silver, other precious metals yet defined and with gems and pearls not yet added to the game, Jeweler buys sells gems and pearls of varying cut and quality.
Aggregate - Building supplies (dirt, stone, crushed stone, limestone), masonry (buys/sells cobble stone, various types of brick), stone quary, limestone quary, sand pit, clay pit
Textile - fabrics (cloth, string, twine, silk), clothing, tannery (hides, tanned hides), leathery (leather worked goods) - Look at "6I — Textiles"
(exception is a Merchant which buys everything (they sell a random variety of things, skewed towards what the local economy of the town is NOT selling)

The NPC population in the settlements are workers and shop owners. They cycle duties throughout the hours in the week to walk out to chop down trees and run a shop buying/selling wood if they are at a lumber mill. Mining Outpost have NPCs that dig open surface pits and tunnels to extract ore then buy/sell those goods. Blacksmiths run furnaces in their shop, textile owners have sheep paddocks that they collect wool from to spin into yarn and tanneries hunt deer/ bison/ kill cows for hides. These are all visual, physical actions that NPCs do in the world and in many cases leave traces of the terrain modified from the work they do.

When we get to iron ore, name something Edmund Fitzgerald.

Recap:
Make a new Scene in Unity called MapGen. Start with a 5 km by 5 km map outline, generate interesting terrain landscape based on a seed, esnure there is varied terrain and biomes. Make sure at least one side of the map touches an ocean. It could be up to all 4 sides of the map, touch, an ocean, or just the one. Make sure to include the boundary types that we discussed above. Next generate ore resource nodes in the ground, tree spawns on the surface. From that map data, spawn the various settlements based on their proximity to resources. Make sure to hit the required quantity of these settlements described above. All economy specialties for what a shop is buying/selling as a sub caterogry needs to be present in the map at least once, there can be multiple instances i.e. 2x lumber mills that sell raw wood and boards. Next go through the map and connect all settlements with a network of roads, whether that be footpaths, cart paths, paved cart paths and water based transit. Smaller boats can use rivers and larger vessels use the sea. Once the various settlements have been determined based on their their local resource proximity, they get chosen what their economy specialty would be. It's all based on the supply chain of what's nearby. So a trading post that's nearby lumber turns into a lumber mill, and they have workers that go and chop down trees and process those into various lumber based goods. The capital is the specialty ones. There's only one capital in the entire world. It has to be on an ocean's edge, and it must have connection within its area to every resource type and every economy type there. It also needs to be connected well with paved cart paths.

Section 2. 3D Model Overhaul
Update all the player and NPC models to have an improved 3D low poly models. The goal is not to have blocky minecraft characters but instead have more fitting low poly figures that fit the look of the world better. Think closer to Totally Accurate Battle Simulator and the game Polylithic is much closer to the aspiration, this is also a good reference. https://sketchfab.com/3d-models/low-polygon-characters-pack-4187adfe52824bd5a88ffb356e0b40ef

Update the 3D models of the tools that you hold in your hand to look more stylized for the game (knots and grain in wood, cracks/notches in pickaxes, slight shine in the nicer metal tools like the water shimmer effect.

Update the 3D models of tools so that when you drop them ont he ground they drop the 3D model that has physics and can roll on the ground. Inspiration is Skyrim for item dropping behavior. Dropping material like stone and ore stil piles up on the ground in the terrain mesh. Dropping large buildable equipment like the campfire or furnace drops as a brown canvas bag similar to the mechanic in Rust. Also very small items like a pearl or gem drops in a canvas bag so the player can actually pick it up.

Freshen up the look of items in inventory slot images as well to match the 3D models. Stone should look like the low poly loose stones you see on the terrain surface, ores should follow this shape just with the different mesh material. Actually just follow this for the other materials, leaves, wood, sand, caly, etc. We want to see a small low poly chunk of the stuff as the ivnentory icon.

Section 3. Animals and AI
Add Rabbit, goat, sheep, yak, and alpaca to feed the textile supply chains. Add Cows, Deer, Elk, Bison to feed the tannery and leather supply chains. All of the animals need to have low poly 3D models, have spawning logic for appropriate biomes, and basic behaviors for daytime/ nightime routines. The player kills them by left clicking a tool on them and can press E on the carcass to open their inventory and collect drops from them. 

Add-on, same conversation:
Also pressing M pulls up the world map

---

Where it went:
- Section 1, the bounded map, edges, terrain, biomes, resources → GL589–GL592 (batch A)
- Settlement types, quantities, specialties, placement, roads, names, Edmund Fitzgerald → GL593–GL597 (batch B)
- The MapGen scene, the world map (M), seeing across the map, walking it → GL598–GL601 (batch C)
- Section 3, animals → GL602–GL607 (batch D)
- Section 2, character models → GL608 (batch E); tools and drops → GL609–GL611 (batch F); inventory icons → GL612 (batch G)
- Settlements built in the world, shops, NPC work cycles, carts, thieves, boats → GL613–GL618 (batches H–J, second shot)
