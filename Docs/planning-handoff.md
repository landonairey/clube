# Planning handoff — "Bounded world, models and animals"

For the planning chat. Read this file and nothing else to pick up the work; keep it current (it replaces the chat's memory). The plan itself is in `Docs/clube-objectives.md` under that section (GL589–GL636).

## How the planning chat works
- It only plans, dispatches and relays. It never reads code, diffs or the history doc, and never implements.
- One batch at a time (one checkout, one Editor). Dispatch = a fresh session (when the user is at the desktop and asks in the chat) or a background agent (from a scheduled, Remote Control or agent-triggered turn, where the app refuses sessions). The prompt is short: "Read `Docs/batch-brief.md`, then do batch X: GL IDs, cut `<new>` from `<chain tip>`, notes." Model: Fable for batches.
- On a batch's report: relay its 12 lines or fewer to the user with the PR link, update this file if the batch did not, dispatch the next. If a batch stops on a usage limit, resume the same agent or session with a message after the reset; do not start a new one.
- New feedback from the user: a new dated backlog file with their words, GL entries with line links, committed to `main` (use a temporary `git worktree` of `origin/main` while a batch holds the checkout), then slot it into the queue below.
- Start a fresh planning chat when this one passes about 100k tokens: this file is the handoff.

## State (updated 2026-11-12)
- **Chain tip:** `gl613/roads` (the checkout is on it, clean).
- **Open PRs, to merge in order:** #132 A map · #133 B settlements and roads plan · #134 C MapGen scene and M map · #135 K iron, coal, steel · #136 sea LOD · #137 D animals · #138 L ore looks, 10x XP, skill cheats · #139 E character models · #140 M map legend and new-game preview · #141 H1 roads in the world. Each PR shows the earlier ones until they merge. The user merges; the planning chat does not.
- **Running:** nothing. Next to dispatch: batch H2 (settlements on their parcels, shops; GL613's settlement half and GL614).
- **Done:** H1 — roads in the world (GL620, GL631–GL634 and the roads half of GL613), PR #141. Roads are refined on the real ground after the plan (`WorldRoads.Finish`), written into the terrain round the viewer (`MapRoads`, `RoadWriter`), with bridges and signposts; Dead Grass is material 34. Leftovers: roads exist in the ground only within 150 m of the viewer, so the far terrain shows none; a dirt cart path does not show on a biome whose ground is dirt; writing a paved road costs about 4 ms a metre (one piece a frame while walking it) and the first pieces of a session hitch (about 0.1 s); the bounded tree cut and the junction fix were not seen in Play mode; a tree on a road is cut within 4 m, so a far branch can be left hanging; switchbacks were only seen in a test (seed 1's roads never pass 13% slope); saving and loading the written pieces was not tried in a real save; settlements are still marker posts, so roads end at a post.
- **Done earlier:** M — map screen and new-game preview (GL635, GL636), PR #140. Leftovers: the preview shows the bounded map while Start still loads WorldLab; typing a seed and Random were not tried by hand; names overlap where places are close at zoom 1; `BoundedMapTests.Survey_CoversTheMap_AndIsTheSameEveryTime` is flaky (two survey builds differ in the last float digit about one run in three).
- **Batch E's leftovers:** the player's shadow body and new first-person arm, the tutorial crew, the adventurer, the carter and the menu miner were never seen on screen (only the WorldLab shopkeepers were); no figure LOD or instancing yet for a 24–32 NPC city (11 renderers and 822–1048 triangles per figure: matters for batches H and I); the shadow body does not mirror tool swings; a recompile during Play mode once wiped the rigs' look (now serializable, not re-tested). The shared figure system is `FigureLook`/`FigureLooks`/`FigureMeshes`/`FigurePoses` (Core, `Meshing/Models/Figures/`) and `FigureRig` (Game); a new NPC look is a `FigurePreset` row.

## Queue, in order
1. **H2 — settlements in the world** (GL613's settlement half, GL614): settlements on their parcels growing `TownPlan` and `TownBuilder`, shops per `EconomySpecialty`. Roads are built (H1): a settlement's roads end at its centre (`PlannedSettlement.Position`), a signpost stands where each leaves it (`WorldPlan.Signposts`, `AtSettlement`), and `MapRoads` writes the road through the site, so H2 decides how a town's own ground meets it.
2. **F — tools and dropped items** (GL609–GL611).
3. **G — inventory icons** (GL612), after F (icons come from the new tool models).
4. **I — NPC work** (GL615, GL616), uses E's figure system and D's animals.
5. **J — thieves and boats** (GL617, GL618); the *Edmund Fitzgerald* already has its route in `WorldPlan`.

## Open with the user
- New-game preview: the sea has a faint wash and a blue shore line, beyond "contour lines and no other detail": keep?
- Anvil: sells for 60 coins but costs 100 iron ingots at 48 each; one must change.
- Magnetite was made dark steel-blue, not black: keep?
- Blocky dark patches on the sea after the sea LOD fix: not compared with a before picture.
- Readings made without the user: coal is also furnace fuel; steel = 4 iron buns + 1 coal; iron and steel tools mirror the bronze set; the *Edmund Fitzgerald* is the iron ore vessel.

## Known leftovers (not scheduled)
- GL600: the built-game frame time from the peak at 2560 x 1440 was never measured (Editor: 8.4 ms at 2272 x 1278). Needs a `-townbench`-style flag for MapGen.
- Rivers are still one quad per water cell (about 300,000 triangles); possible pinholes where the outer sea meets the sheets.
- The void is ground 600 m down; the ridge and deep sea floor lie outside the voxel world; the far terrain is not coloured by biome; the player can start on a treetop; start-up 10–12 s.
- Animals: no sounds, the player bumps their colliders, six species never seen spawned; real mouse and E input never exercised on animals, the M screen or the cheats.
- Limestone has no material (the limestone quarry is placed by the every-specialty pass).
