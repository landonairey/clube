# clube — First gameplay loop playtest (2026-10-07)

Notes from the first full run of Milestone 2's loop (GL16): **mine copper → smelt a bun → hammer an ingot → sell it**. Played in WorldLab at 4 voxels per metre, driving the game through its own code paths from the Editor. Real keyboard and mouse input can't reach an unfocused Editor, so the timings below are game time and swing counts, not hand feel. A hand playtest of the controls is still owed (see the end).

![The loop site: furnace, anvil and merchant stall around the dig site](images/20261007%20loop%20site.jpg)

## Setup (GL15)

- **Site:** found by scanning the seeded world's copper nodes for the shallowest one under level ground near the origin. Node at (-15.6, 14.2, 4.0): 4.7 m below a flat valley floor (surface 18.9 m, under 0.2 m of rise across each station's footprint), with a second node beside it at 7.6 m. The world generated 1,424 copper samples there, the shallowest 3.7 m down.
- **Layout:** spawn 4 m west of the dig site, facing it. Furnace (-23.1, 6.7) and anvil (-22.5, -1.8) behind the spawn, merchant stall (-14.2, 7.7) beside the dig. All face the spawn and stand 5–7 m from it.
- **Starting:** WorldLab's fly camera now starts over the spawn; press **P** to drop the player there with the pickaxe in slot 1.
- The site depends on the world's seed and terrain settings. Changing those moves the copper, so rerun the scan if they change.

## The run

| Step | What happened | Cost |
|---|---|---|
| Arrive | Landed 4 m from the dig site, stations 5–7 m away | — |
| Dig to copper | Straight down through grass and dirt, then stone; once at the node, 6 swings gave 14 copper ore and 18 stone | about a dozen productive swings (~5 s at 2.5 hits/s), plus time lost stuck (below) |
| Get out | Dropped dirt at my feet (G) 4 times, 16 at a time: lifted 4.8 m back to the surface | 64 dirt |
| Smelt | 8 ore → 1 copper bun | 6.0 s |
| Hammer | 1 bun → 1 copper ingot | 6 strikes |
| Sell | Ingot for 30, 6 spare ore for 6 | **36 coins** |

Refining pays: the same 8 ore are worth 8 coins raw, 12 as a bun and 30 as an ingot.

## What to change next pass

1. **You get stuck in your own shaft.** The pickaxe's 3×3×3 reach cuts a shaft about 1 m wide; the player's capsule is 0.78 m. The dig box centres on the voxel the ray hits, not on the player, so successive boxes leave lips. Marching cubes bevels them at about 45°, under the controller's 50° slope limit, so the capsule stands on them. I hung 2.5 m above the shaft floor, out of the tool's 4 m reach, with every swing hitting nothing. A thinner capsule (0.28 m) didn't free it. Options:
   - centre the box under the player when they aim steeply down;
   - lower the slope limit so bevels don't count as ground;
   - snap down-digs to the player's column.
2. **No way out but pillaring.** Dropping dirt at your feet works and is fun, but it refills the shaft. Consider digging stairs, a ladder item, or a little more jump height. Any of these also helps with item 1.
3. **Reach versus holes.** Aiming ahead into a hole goes out of the 4 m reach within a few layers, so you have to stand in the hole, which leads back to item 1.
4. **The pickaxe excavates rather than picks.** A swing clears a 0.75 m cube of grass or dirt, which makes soft ground trivial; stone takes 4 swings per cube. Per-material tool efficiency (TL4: pickaxe good on rock, poor on soil) or a shovel for soft ground would give tool choice a point.
5. **Too many items per swing.** One item per removed corner means a 3×3×3 swing can yield up to 64. Eight ore for a bun is quick, and a small shaft produced 212 dirt. Count drops by volume removed (I5) or raise the ore per bun, and rethink whether aggregates should fill the inventory at all.
6. **Finding copper is luck.** It's 3.7 m or more below the surface with no sign of it from above. An outcrop near spawn, or the first step of prospecting (PS1 hints), would make the first loop discoverable rather than placed.
7. **Fine as they are:** the station panels, the furnace working while the panel is closed, the price ladder, and the pile-and-lift drop.

## Still to check by hand

These need real input, so they're for the next session in the Editor:
- swing feel and the cross cursor while digging;
- E on each station and the merchant, and the Strike button;
- G tap versus hold;
- jump momentum while running;
- the hotbar keys, wheel and Q, and Tab for the inventory screen.
