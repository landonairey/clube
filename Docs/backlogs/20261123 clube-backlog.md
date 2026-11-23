# Backlog 2026-11-23

Playtest notes, in the user's own words ("20261123 Playtest Notes Must Haves"):

1. In the tutorial make the out of bounds area be dictated by the wall around the settlement. If the player goes outside the wall then teleport back to the pit with a warning not to do that again. If the player goes outside again then have this be the 3rd way to kick them to their family's parcel.
2. bug, hematite texture is visible to the player before they have a high enough skill to detect it. It should enter your inventory with a question mark.
3. while you are at the tutorial, have the game already start buffering your family parcel in the background to cut down on the loading time when getting kicked out of the tutorial and going to your starting parcel.
4. bug, seed 819369, voxel xyz -300, 1245, -3920 there is a weird spikey line of stone that cuts across the starting parcel land. This continuesoutside of the parcel a bit and I also see soemthing similar as a ring aroun the nearby lake. Debug what's causing this in the terrain builder becasue it's not natural looking and needs to be fixed.
5. bug, two bridges cross eachother at seed 819369, voxel xyz -850, 1082, -4224 and this hitboxes made walking through impassable.
6. bug, seed 819369, voxel xyz -790, 1016, -4424 has a weird ramp in the footpath that juts upwards then suddenly stops.
7. fix the far view ocean water, it looks silly with the wavy checkerboard pattern. Add some waves that shimmer light and look decent. Use an approach that minimizes visibile repeating patterns.
8. Bug, The clams are just purple spheres in the water, add an actual low poly 3d model. Change the pearl inventory icon to be a 24-sided polygon shape with some hints of iridescence. Also change the interaction of the clams, press E to open, and pressing E while open with close it AND take whatever's inside if there is something inside. If you want to close it and keep a pearl in there then press the button that says Close. When taking a pearl the clam dissapears with some bubble particle effects. Play a bubble sfx noise.
9. Update the merchant to buy pearls, they are 40c and black pearls are 400c
10. bug: some signs that use a vertical metal fastening piece that ties two signs together on a post overlaps with the text on the post. Also the metal fasteners that attach the sign to the post partially obstruct the text on the sign
11. If there are too many clumped up dropped canvas bags that all have the same contents (for example a a pile of 107x bags with one hand axe each) then combine those sacks together. Anything over 10x of the same sack in one area just auto combine it. When you go to pick up the combined sack, have a UI box pop up to select splitting the stack of sacks. And give a warning if what you are picking up will go over your inventory size.
12. issue, I loaded into a parcel that only had pine trees which makes early crafting hard. Ensure that the starting parcel lot is next to an Outpost sized Settlement AND is in a biome that has Birch Trees, ensure that 2x Birch Trees are in the parcel
13. bug, have shovels be able to be made from pine wood as well as birch wood.
14. bug, I have floating leaves in my game, these pine trees leaves were left in the air after cutting down a tree.
15. bug, the 25% early bird bonus was not present in day 2. Have this present every morning of every day, even after using your bed or someone elses bed.
16. feature, add hitboxes to sign posts so you can jump on them. Also when you run into the swinging sign board you can knock it back. Also swinging a tool or punching it with your bare hands knocks the hanging board.
17. Change the settlement shops to use the actual workstations that the player can build. i.e. the campfire, furnance, anvil (you'll need to make a 3D model for the spinning wheel and at that to the crafting list for the workbench, it takes 10 boards of any wood, 5 copper ingots, 5 wool from any animal)
18. bug, when I saved and reloaded my game I came back to a pile of loose hand axes that used to be in canvas sacks. Something happened and there were no longer sacks on the ground but loose hand axes everywhere.
19. bug, have a campfire be made from anytype of wood, not just birch
20. bug, have the wooden crate be made from any wood not just birch wood
21. bug, I watched a white square approach my position on the map coming up a river but the trading boat was not visible.
22. Bug, the shop sings overhead their opening have the text clipped and cut off by the building itself.
23. have XP text float up near the cursor on every tool hit/ any time you get XP. Show the text that reads "<AMT> XP"
24. bug, have the lumber mill buy any type of hatchet
25. bug, when I left my parcel and went far away to a town and came back, I had floating tops of my previously cut down pine trees in my parcel. Is that becasue they are many chunks above me and it didn't get cleared out?
26. bug, I'm at the merchant and at disposition 37, it only takes 12.12c more to rais it another level, that scaling seems wrong to be able to reach 100c per level by disposition 50. I've found that the equation 5.619*exp(0.057575*current_disposition) gives the right amount of Coins needed to increase to the next disposition level that I'm looking for.
27. bug, flickering shadows or light rays in a mining hole I dug down voxel position -295, 1210, -3941 seed 819369 Day 4 time 15:30. I think it's due to the overcast clouds and the sun position. But I'm seeing lots of flickering light ont he walls of the dug out mining pit here.
28. bug, have the Skill Journal say that mining level 2 gives you ability to crush Ore by bashing it on a boulder. Right now at level 2 the Journal says nothing new is unlocked.
29. bug, when using a pick during mining ore and hard stone and other material I shoudl see sparks on occasion but I don't.
30. bug, the charcoal icon and 3d model looks like stone.
31. bug, have any type of wood be able to be used to make charcoal.
32. When buying and selling a the shop, make it more visible how many coins you have and how many coins the shop keeper has
33. bug, trees that are high up don't fall their tallest leaves when felling a tree. Also Some leaves that are outside the parcel don't fall either, have these fall.
34. bug, ore that shows up as a question mark in your inventory or stored in a crate inventory should update to the identified ore once you level up mining to identify it.
35. bug, on day 6 the 25% early bird bonus turned on at 2:30am, that's the wrong time.
36. Green specs on the Malachite inventory Icon are still too small to make out easily.
37. Add Coal + Sticks as another valid recipe for torch. The Sticks could be from any type of tree.
38. Cut the Lesson cost in half of what it currently is when buying XP level ups at shops.
39. bug, the time command in the cheat menu does not allow you to rewind to an earlier day. Attempting to to go to the night of the 5th day just added 5 days to my current game time.
40. bug, if you start crafting an item and only hit the Strike button once, then switch the recipe to a different item it should reset the crafting to that new item. However, it doesn't switch and continues crafting the original one. Switching recipes should also reset the strike counter.
41. bug, make all leaves the same inventory item. Wether they come from bushes, pine trees, maple tees, birch trees, or from fallen piles it should all be the same inventory item. No more distiguishing between them in the contents makeup of a stack.
42. When saving a game, have it be able to choose an existing save file to save over 
43. bug: when fallen trees spawn on land they look like wet noodles, draped on the landscape surface. Have these fallen trees look straight. One idea is to figure out the average slope for that area and shoot the trunk straight in that direction replacing any terrain it may run through. The fallen trees over rivers look nice and straight.
44. bug, when in your inventory if you left click a stack of pure items to move up into your bag, do NOT automatically combine it with a stack of lower quality. Pure Items should only combine with other Pure items in your bag automatically. Howeverm if you manually drag and drop a pure stack onto a stack of lower quality then it will combine.
45. bug, when making the stone hammer, it comes out a a fractional quality amount. Instead have the hammer come out with a % of quality rather than a fractional quantity of the hammer. Every crafting event makes one full hammer.
46. If you are in the Map and press E, have it close the map and switch view to your inventory.
47. in a a crafting inventory screen with your inventory and the UI of any crafting station, if you drag and drop a stack onto another stack of the same item type but different quality that's currently in the input item slot of that work station, then combine the stacks. 
48. bug, the anvil (like the hammer) has a fractional quantity. Have the crafting produce one full anvil and the quality depends on the input iron ingot quality. ALso a major bug with the anvil is that it doesn't leave my inventory after building it on the ground.
