# clube — Backlog (2026-11-09, c)

Playtest notes given 2026-11-09, kept as they were given. They are organized in `Docs/clube-objectives.md` under "Playtest 2026-11-09"; the GL IDs below say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

Fixes:
- **GL512** Menu title screen background. The general concept and placement of things is good but use the actual world gen to make the world that's on display. Let's see the real trees, campfire, stump, grass/dirt/stone/ and most importantly the ORES!
- **GL513** In the tutorial when it says to walk up to the crew lead to talk, put a quest highlight arrow above their head and on the mini map
- **GL515** bug with the tutorial it automatically says that I stole something and it kicks me to my own lot. I spawn with leaves and a stick in my inventory and get ckicked out when talking to the crew lead.
- **GL516** Bug when mining for the mining corp, the land says I don't own it.
- **GL514** Change the tutorial so that it takes place on a different map specially made just for the tutorial, Make this new tutorial map be a larger company warehouse building which you spawn in the backside of it. You can only enter in the dorm hall room where you have an assigned bed to sleep in at night and which there is a room for the Crew Lead. The room with the crew lead has a wooden crate at the end of this bed with values in it. Also littered about outside there are crates with various items in them as well. When you first spawn you are talking to the crew lead who asks for your name which prompts you to type in your character name. He then directs you t othe working fields out back which face a large mountain on all 3 sides of the rear of the warehouse. You are given a company issued pickaxe and are free to begin mining into the side of this never ending mountain of stone.
- **GL481** bug: the crafting menu recipes are still out of order. Stone Hammer and Stone Furance (crafting Level 3) appear higher up in the list than Shovel, Maple Stick, Chest, Bucket, Torch with are level 2
- **GL482** bug: the left bracket in the quest items is still partially visually clipped.
- **GL483** Edit the text on the "...if you get stuck you can climb out of the hole" add to that to use SPACE as the key to climb
- **GL485** Bug when crushing ore the yellow text next to the cursor says the % of quality of the crushed ore even though the user isn't at the propsecting level to discover that yet
- **GL497** Bug, there's no trout in jumping in my game?
- **GL486** Bug, the fading orange text when refining an ore needs to fade faster, so that is appears to fade before the next text comes in when press and holding the actino to continuously refine a stack of material.
- **GL499** Bug, missing the early bird bonus for speed in the morning, where did that go?
- **GL498** bug, I don't see any improvements to the mining particle effects, no sparks and enlarged particles that spray.
- **GL487** bug, player cannot refine stone on a boulder. This requires avoiding the hand axe creation.
- **GL488** bug, missing way to craft firebrick, is this only done at the workbench? Add boards to stump recipe list
- **GL509** Change the thief spawing room under the village to be entrapped by terrain in all sides but with tunnels that lead out to the north, east, west, south faces of the parcel the town sits on. That way when the player buys land up to the edge of the town then digs down, they can discover the hidden tunnels to the thief.
- **GL511** bug, trading carts never appeared
- **GL510** bug, trading road route is nonsesical as it just ran right up a mountain face.
- **GL506** bug, the sand to grass transitions in the terrain are too jagged.

additional features:
- **GL503** carve out earth in riverways so that they are full of running water. The rivers are still patchy as you follow up them up into the mountains
- **GL504** Add rocks and downed tree trunks draped across rivers.
- **GL505** Add fallen trees to the forest (these don't have leaves but have a stump and falled logs on the ground, random cardinal directions)
- **GL491** Add button clicking noises for the menu buttons. Have any Back buttons make a slightly pictched down version of the other buttons.
- **GL484** Add Tip "Climb trees by holding SPACE to cut off smaller sections of limbs. As your Woodcutting level increases so does your tree cutting yield"
- **GL500** In the crafting menu GUI list the input item quantity as a fraction of current amount / recipe amount needed. (i.e. making campfires I have 91 wood and 51 stone so that should read 91/4 on the Wood count and 51/8 for the Stone count.) If your current item amoutn is less than the minimum needed for the recipe, show the fraction in RED text. So if I had no Stone it would say 0/8 and the text would be read as well as the stone icon being grayed out.
- **GL501** If you have a non stackable in the input slot of a crafting GUI, then left clicking a different (but same type) item from your inventory should replace the current one in the crafting GUI. (i.e. when maing a stone hatchet the hand axe that auto populated was a low durability rating, to swap it with a brand new one, the player shoud only have to left click a fresh one from thier inventory and it should swap out with the one in the crafting receipe slot)
- **GL495** Occasionally show shooting stars in the night sky
- **GL489** Remove the merchant table from your starting parcel. This buildable item will remain only in the debug menu in the cheats section
- **GL494** Make the campfire smoke larger and blacker
- **GL492** Make a licking/consuming noise when eating the ore
- **GL496** licking crushed ore gives more xp than licking raw ore
- **GL508** Make the trading post more of an organic shape. Don't generate things within a strict square outline. Start first by selecting a trading post location: no change to the requirements of proximity to ores and water. Next place shops close to their things of interest: Mining shop next to the open tin ore pit, lumber shop next to the trees, blacksmith shop next to river, merchant next to trading road, prospector is just wherever. Then draw the stone wall border around this arrangment. They should all roughly be near eachother, just not a perfect square.
- **GL493** Add SFX noise of picking over material as you refine crushed ore.
- **GL502** Update smelting yield to also be based on quality of fuel type too
- **GL507** make the world depth 5x as deep into the ground.
- **GL490** by default double the free camera movement and double shift multipler for debug navigation.
