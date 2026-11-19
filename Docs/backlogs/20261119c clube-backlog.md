# Backlog 2026-11-19c

Playtest notes, in the user's own words ("20261119 Playtest Notes"):

1. Don't start the game time clock until the scene is settled and you can play (tutorial and family parcel)
2. Add the spacebar to climb mechanic back to the tutorial to help get the player out of a hole they've dug
3. The yellow arrow that shows your family parcel (first quest) have it bounce up and down and give the parcel yellow hologram walls that extend up from the fence and fade out after 15m tall
4. Update animal pathing behavior so that they cannot scale super steep surfaces
5. Text still clipping in the menus (audio Menu back button from gameplay, back button in video and audio settings from main menu, Game demo button in new game menu, tutorial button in new game menu, random button in for seed in new game menu, widen the buttons for the save game names to be 2-3 times as long to read the full name)
6. Add seed number to the FPS dialog box for debug purposes
7. Update the first part of the build numbering system now that we have some demos, rihgt now it's just v0.1.0
8. When you've in the stump, crate, furnace or other inventory UI allow the player to be able to drag and drop items outside the UI which drops the items on the ground
9. Change the title screen from "clube" to "Fossa Terra"
10. bug, when loading a saved game, just clicking the save game triggers to load it. You need to select it and then only load once the user clicks the load button
11. bug, when loading from the saved game, the player's position is not preserved.
12. bug, bridges are not flush with paths
13. have merchants always sell stone and birch wood to get the player out of a bind on early materials if they get stuck
14. In the skill levels show the <current xp>/<xp needed for next level> numbers for visibility.
15. bug, have the crafted hand axe durability depend on the quality of the stone used
16. change the Ore discovery to trigger only when you first see an inventory entry of that ore type. So for cassiterite it should show up when you use a tool pointed at the ore and collect that ore stack to your inventory or when you are refining a stack of dirt for example and out pops cassiterite as a separate iventory item. It should NOT trigger the new ore discovery when you shovel a load of dirt that happens to grab some cassiterite in with it, because that's just a stack of lower quality dirt in your inventory
17. add ability to sleep in shop keeper's beds. Doing so wake them up and does a a pt reduction in their disposition towards you. (cap the disposition ranges from 0-100 if you haven't done so already so that it doesn't go negative and doesn't go above 100)
18. update the Tip text for campfire to say at the end "Then CRAFT a Campfire at the Stump, and press B to BUILD the Campfire on the ground". This should clarify the completion requirement for the quest item
19. Remove the % number on tool items in your inventory. This will be represented by the health bar on that item slot that we already have in place. Also we're going to change the naming on that for tools to refer to this trait as Durability instead of Quality. Therefore a newly crafted 100% Durability item will show up as just the item icon (no bar present becasue it's full) and as you use it, show the bar for it's current durability level. Newly crafted items at less than 100% durability will show up with a bar in your inventory as the view with no bar is reserved for only when the item is at 100% durability. Sticks move their quality descriptor to the Poor, Me, Good, High, Pure system
