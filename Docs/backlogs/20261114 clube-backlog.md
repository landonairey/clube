20261114 Playtest Notes

Overall load time still take a long time when dropping into the game. When the player spawned, I watched in real time as roads and rivers marched through overriding the terrain surface and then settlements took shape. Analyze ways to speed this up. 

Remove the giant flagpole in the settlements. This doesn't make sense and carts get stuck on its hitbox. Also remove the settlement name text from the player's HUD. It doesn't need to be here. The names are on singposts and on the map.

Add a signboard outside a settlement with the name of that settlement

The large areas of dirt fields don't make sense. It should be grass in most climates, sand in the desert climates. The 3D grass height can vary on top of the green grass. 

Never saw a sheep in my playtest, what are their spawn requirements?

The roads within a settlement have too many right angles. Make the paths curve between houses more organically. 

Add settlement structures and roads to the LOD system so you can see them from far away. Roads should be easier because they are painted on the surface. For settlement structures consider making stand-in 3D renders of these with reduced polygon count that are not part of the terrain voxels. Varied LOD versions of these stand-in models could be made to represents houses from afar and then when the player gets close, have it swap with the real material. Think about the right choice to inplement here.

House roofs are steps with square voxels but we're using marching cubes! Make the roofs slant but keep the insides looked squared like the chisel was used on it. Try out varying the roof pitch to make house look less uniform.

Revamp the structures to vary buildings in the different settlements. The Capital needs to be more grand including a Castle (hence the icon) and walls around the capital. Cities have at least one main stone guard tower (hence the icon). Think having 1,2,3 story buildings, houses, mills, tavern, stable, blacksmith workshop, wood workshop, smelting areas, the whole lot of typical medieval dwellings. Inspo: https://dk.pinterest.com/pin/113645590585125977/
Change the icons on the Map to instead of being colored shapes to being a dark wood circle with thin silver steel outline. In the center are the icons for each settlement type drawn in silver steel color.
Make the Player be able to ride on carts and boats if they jump on them.
Change the trees to be up to 4x as tall, also 4x the amount of wood yield cap in your starting woodcutting level when you chop down a tree. And Increase tree density. So the world fills in a bit more.
Add a Pine evergreen tree type to the world that tends to be in colder/mountain climates. Quality sits between Birch and Maple.

Dig out rivers deeper and wider so that thr boats can navigate them without clipping into the shorelines. It also prevents patchy spots in the rivers.

Add shader for deep ocean water waves. Add dither to where the playable area blends into the map board for the transition to deep ocean. Looking down from the mountain to the ocean you see the ocean floor and then it abruptly ends at the map edge. Change this floor depth so its deeper, water look more dark in the deeper sections, and blend the playable area to the out of bounds area.

When NPC lumberjacks fell trees have them effectively have the highest wood cutting skill level. So that when they fell a tree, the whole tree ends up on the ground. And there's no yield loss. Then, after they fell the tree, they continue to chop up the laid down wood to extract it from the ground as well.

NPC workers only spawn when you get within render distance to the settlements. We need to have all settlements on the map actively gathering resources even when the settlement is not loaded. My recommendation is to come up with a rate of resource extraction and keep trwck of that over time. Then when you need to load a settlement, apply the neccesary terrain/tree deformations that equate to the reaources aimulated to have been extracted so far. Think about if this is the right approach and propose a solution. Think of Age Of Empires where all civilizations are gathering wood, stone, gold, livestock at the same time around the whole map.

Improve the pathfinding for NPCs as the workers get stuck on buildings and eachother for walking to their resource area to collect resources from

Add slight visual air haze that makes things look a more washed out the further they are. I.e when sranding on top of the highest peak the local mountain is in normal view colora, but the next peak is slightyl muted in color, and the distant land down below is even more so. Make this natural looking, not distracting. It should add to the sense of scale of distance to things and impressiveness of landscape.

Add taller bushes, and make groups of bushes. Change 3d grass to be denser (figure out a way to do so that's not as computationally intensive) and make taller grass.

Add Reeds that grow around the shores and partially in the water of lakes and rivers. They dont grow in saltwater. Vary density and location around these waterways like trees. Reeds are collectible and can be crafted into fibers and then twine (see the objectives backlog)

bug: the pickaxe items on NPCs appear 180degs rotated and glitched into tyheir bodies. The handle starts at their hand and then the head of the axe is in their shoulder.

bug: NPCs group up with eachother and clip into eachother.

Add a take all button to wooden crates.

Update trade routes to go off the map bounds to simulate trading with a larger world. This applies to ocean vessels and land based carts. As they pass outside the buildable area they can no longer be interacted with. And as these pass beyond the map boarder, they fade away.

bug: missing item pricing when hovering over items in inventory

bug: as the player walks/runs towards the water, the near water doesn't load as fast as the player is walking. So the far LOD water begins to receed backwards and the near water isn't rendering in quick enough and then a ring forms with a gap in the water. Fix this by optimizing near water render time and also only remove the far water LOD rendering when the near water is ready to take it's place. That way the only visual bug will be the pop from far to near but at least there won't be a gap.

bug: in the capital, there was lots of rivers runnign over roadways. Make sure bridges are installed at these junctions.

Improve the trading vehicle destinations. Carts shoudl pull up to the various shops that match the goods they are towing into town and offloading them. Give them an unloading animation when they are parked outside the shop which empties the crates on board. Then give them a loading animation which adds some new items to the crates in the cart and then they get back on and get going. For ocean vessels, create a set of marina docks that match the size of town. Larger capital? - make a more grand set of docks with paved connected to the downtown (docks need to be as close to town as the buildings in town). Smaller village or outpost only need a small foot dock connected to the couple buildings. Sailors dock up to the platforms and have an unloading animation, then loading animation, then off they go. 

remove the dark nubs on the stone hand axe, both the 3D model and the slot icon.

bug: the workers at the anvil are just parked outside the blacksmith hammering air. When you update the blacksmith workshop building design, add a courtyard with anvils and tool racks that these workers can be stationed at.
bug: similar as above, the textile workers at the fabric building are just squatting outside spinning a loom in the air. add a fabric workhop area with canvas tarps overhead the various fabric workstations. This means you need to make a low poly spinning wheel.
bug: workers outside merchant is stuck on building and doesn't have a broom in hand that they are using to sweep the air with. fix this.

Add light sources to settlements, smaller ones have a couple permanent torches lit. medium sizes ones have a mix of torches and large bonefires the size of 2x2 building squares. Largers ones have oil burning laterns.

tweak the tree felling mechanic so that when limbs rotate and fall downward they fall outwards 1-2 voxels away from the edge of the stump width. This will avoid wood piling up in the center on top of eachother in a tall tower as the player niddles off multiple smaller limb sections.

when you refine any material, dirt, clay, etc have it add propsecting Xp

dissallow auto stacking material into a Pure item stack unless the user manaully drags and drop lower grade material on top of a pure material stack.

change the chalcopyrite and malachite icons to not have the cracked texture on them. It should look like the other ores with the colors on the rock but they don't need cracks. The crushed version is just a collection of smaller pieces - also no cracks.

add 3D models of all the material items so that they appear in the hand of the player when equiped int he hotbar. 

bug: licking an ore seems to be removed from the game. Add that back in (licking - consumes and adds prospecting Xp). You can lick raw ore and crushed ore.

Change the basic Shovel item to have a wooden head as the recipe uses all wood and no stone

bug: missing ability to walk up to material and prospect it to get a little bit of prospecting Xp.

update tutorial to take place at one of the settlements in the map. This settlement sepcializes in Stone production and that's all it does. Build a stone wall around the settlement which alos enlcoses the stone quarry pit that the player will use to dig in the tutorial segment. This means that when you get dumped off on your family's parcel of land to begin the game, you cango back and visit the actual stone quarry. Their disposition with you will be zero however.

Udpate stealing mechanics, when you take from a cart or boat, the shops that the cart or boat are going to will reduce their disposition with you.

bug: when I have two partial stacks of the same item type in my inventory, one int he hotbar and one in the backpack inventory, when I click on the stack in the backpack it auto jups to the hotbar and combines with the first stack already present there. However, it doesn't fill the first stack completely. It leaves some decimal amount less than 1 away from the full quantity and the rest is in the second stack. I have to then manually drag and drop the stacks together to get one to be a true full stack.

bug: when yo uahve the wooden crate inventory open, you should be able to drag and drop an item from your inventory to a specific slot in the crate inventory.

number of strikes required to make a bun into an ingot should reduce as your blacksmithing skill increases.

have the furnaces be able to take either buns or ingots for their recipe (i.e, copper bun and tin ingot makes bronze bun.). The output in the furnace make the lesser of the two input types. (i.e. copper bun and tin ingot make bronze bun but a copper ingot and tin ingot make bronze ingot). Coal has no ingot form so coal and iron ingot makes steel ingot.

flip the steel recipe around, 4 coal and 1 iron makes 1 steel. I know it's technically wrong but it makes for better gameplay with material values.

make higher value items add more Xp. Crafting a steel ingot should give more Xp than a bronze ingot.

too much clicking on the flat rock. maybe it's time to do workbench upgrades with tools like adding a hammer to the station for reduced click count.

Bug: missing thin white outline on boulder, stump, all interactables. What happened to this?

console is full of logs saying "Boats: water route XX is not open water end to end: its lane stops short." What are these?

Feature: add horses to the game and horse drawn carts can occupy the larger paved trade routes. There is an NPC with a Horse tied up to a post next to the Town Signs outside the Capital and each City offering a fast travel option between them for 250c.

bug: floating cut section of a Birch Tree located at 4907, 283, 2525. Find out what happened here, maybe something over wrote the tree generation.

bug: I was walking with lumberjack NPCs kinda far from town and they all of a sudden dissapeared.

Improve the lansdcape generator to include feature generation of cliffs and canyons. Cliffs can form of varying run lengths and drop offs. Even in flatter grasslands the terrain surface can swell up to a cliff edge which dorps down to the continuation of rolling grasslands. The Cliff faces are mostly stone. Introduce Sandstone and Granite as new materials to the game, same stack size and gravity mechanics as Stone. Cliffs can run the edges of lakes and Oceans too. Cliffs on the Ocean edge can be incredibly tall and striking looking. Canyons make use of layered Limestone, Sandstone, Granite, and Stone to make visually interesting formations. They are in drier biomes and are carved by river paths.

bug, when typing in the cheat text entry, diable hotkeys and game controls. i.e. when typing make sure the WASD letters don't move the character and that M doesn't open the map for example.

where is the merteorite event mechanic? Read the docs and add this if not done so already and add a trigger in the debug menu to call in a meteor targeting to land where the player is positioned.

Add pearls and black pearl items to the game. Spawning - they spawn in lake beds and ocean beds in clams. Clams are about the size of a voxel and you press E to open one up to see the inventory of a clam there's a 25% chance of there being a pearl. Once you close the inventory (whether there is a pearl or not) the clam dissapears. More will have a chance to spawn later. Black Pearls can spawn in a chance of 1 in every 100 when a pearl is present in a Clam.

bug: Smelter shop should buy buns and ingots

bug: when you buy a torch it should not be the Lit Torch variant.

add a rule for no NPCs to mine/cut wood on your parcel and the surrounding 8 parcels to your starter one. This will ensure that you have the neccessary resources to progress properly especially with finding trees.

add ability to pick back up a placed torch

when you craft a hand axe and your inventory becomes full, display a text note on the screen that your inventory is full and drop the newly craft hand axe on the ground automatically. Apply this rule to anything that you craft and automatically attempt to pull that new item to your inventory. Same thing applies to when you're at the tree stump and when you leave you auto pull the input items back into your inventory. If your inveotry is full then display that text and drop the items on the ground.

bug when leaves disspeared they took with them the working furnace. It must have altered the voxels ont he furnace as they no longer became interactable. Also when a furnace breaks like this, spawn drop sacks of the items inside that pop out on the ground to you can recover the inventory contents that were present in a furnce before it got destroyed.

bug, when you press and hold G to drop items into a sack stack on the ground have it also take fractional amounts



Notes from Playtest Guide/ Batch PRs
Batch A #132
Add Limestone material to the game. Stack size same as Stone, gravity stability same as Stone.

Batch B #133

Batch C #134
Bug: there is a multisecond delay on pulling up the Map the first time you press M. Load this automatically in the background to be ready for when the player presses M to avoid the delay.

Skip having to show bridges on the Map, that's not needed.

batch K #135
Change Anvil crafting requirement to 64x Iron Ingots so you can get there with 2x full stacks of 32 Iron Ingots. Then change the Iron Ingot stack size back to 32 to be the same as the others.

Increase the Anvil price to 1000c


#136

#137
Bug, I cannot kill any animals. With hand nor with hatchet. The "You don't own this land" message is showing as I approach the animal so maybe that's getting in the way. update: I managed to swing at an animal when they were on my parcel and I did in fact kill it. So the fix here is to be able to kill animals outside of your parcel.

Add a HP hit sounds when killing an animal. Also add a dying groan noise when the animal dies at an appropriate pitch to the size of the animal.

Change animal behaviors so you can crouch walk up to them without them running away from you. As soon as you strike them or stand up then they can run away.

Hanve animals spawn in multiple groups also have different types of animals spawn in the same biomes, so update the biome mapping for each animal to have them be assigned to multiple biomes that will overlap with other animals, this will result in multiple animals types spawning in the same biome for the player to explore.

#138
Change the Magnetite to have black square crystals in the material, not Blue Colors. Separate the look of this from Cassiterite by the shape and size of the crystals.

Have the Goldsmith sell precious metals and have the leathery sell leather items.

bug: town road pave over rivers

bug: no shops were selling lessons that I could find.

#139

#140
Bug, the player model is no longer swinging a pickaxe in the diorama, ahve it resume this activity in the main menu

In the New Game Menu add a toggle between Demo or Debug. In Demo there is only one Game Mode called Game Demo and it has the contour map preview visible. In the Debug mode there are the same existing four: Voxel, Chunk, World, Infinite (changed from Procedural). Update these to actually connect to launch the four different Lab scenes.

#141
Where apropriate, bridges can be used to span distances across ravines, canyons, etc 

Update all the fonts on signposts, main menu, save/load menu, settings menu, new game menu, player HUD, MAP to use the Font Uncial Antiqua. Follow a similar wood color and text color as described before with the Map icons: dark wood boards with silver steel / eggshell font color.

Improve the look of the signposts by having nail heads shown joining the sign to the post. Add some variation to these where some use metal brackets to hold multiple boards together, some are just one sign nailed to the post. Add signs at the edge of a settlement with the name of that settlement. For these ones make a new 3D model style of a larger  board panel that is suspended from chains from the post that the settlement name is written in larger font size. Vary these between a board that hangs centered on the post vs a board that hangs off from a longer arm off the side of a post. For these ones on chains make the board say in the wind and have the chains move with the connection to the board.

Batch N #142
Parcels can be bought up to the edge of playable area (measured from center point of parcel). Parcels can not be prchased in the zone between playable to outer map boundary (again measured by position of parcel center).

Ocean floor needs to be deeper, and water less see through. I dont want to see the sandy/dirt bottom of the ocean floor when looking out at the ocean.

#143

#144
Steeper paths that go up mountains and all paths in general need a slight rule tweak. The Width of the path should be flat. As is right now, the walking paths are just painted on the faces of steep mountain slopes. That is not natural looking, instead have these paths cut into the mountain slope on one side of the width and on the other side, slightly build up the terrain to meet the path. The walking direction of the path and go up and down but the side to side width of any path should be flat.

Another visual improvement for mountain paths: for walking paths just use the existing Stone material for the path and throughout the path add "loose" stone bumps along the way. For cart paths that hit Stone mountain surfaces, use a mix of crushed stone and stone material for the path. Paved cart paths remain unchanged, still using the loose "cobble" stone material.

bug: Larkthorpe Outpost doesn't cleanly connect the walkable shop paths to the main paved road. There is a dsconnect there.

bug, in seed 2 then switchback paved pave North of the Capital that leads to Tintin Town has large step changes in sections that wouldn't realistically make this path navigable. Optimize the spline routing for paths.

visual bug, the tunnel design works but replace the entrance supports with wood planks and also periodically add wood supports within the length of the tunnel.

With the Island that has Foxcroft Town on it, this looks like two bridged could be built on the West side connecting Wrenham Village to Dunridge and another on the north side of the island connecting Elderstead Outpost to Oakridge Camp. Come up with logic, realistic bridge designs, that can choose to spawn a bridge to connect an island to mainland if there is a large enough settlement there and if the bridge span is small enough to do. For reference, the west side bridge looks to be too long of a distance to span, but the north side has a couple short sections that look appropriate. Mind the sea vessel sizees and their rotues though, you'll need to build the bridge up high enough to allow them to fit underneath.

Batch F #145
When you throw a lit torch on the ground, keep it lit.

Ingots are big enough that they don't need to drop in a bag

Bug, if you drop an item on the ground then cover it with terrain (or have sand cave in and cover it, then the item drops below the map. Change this so that the physics are frozen if an item is partially inside terrain material. It frees once you dig it out.

Batch G #146
Crate 3D models for all of these items to show them in the player's hand when they are selected on the hotbar: ores, ingots, buns, bricks, hides, wool, boards, sticks, etc + anything else I missed. Simply put, all items need to have a model to show in the player's hand.

Batch I #147
don't spawn the player at the capital, spawn them outside an Outpost that has a Merchant shop. If none have a Merchant then add one.

Batch J #148

#149
