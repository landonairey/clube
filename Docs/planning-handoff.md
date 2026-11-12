# Planning handoff — "Bounded world, models and animals"

For the planning chat. Read this file and nothing else to pick up the work; keep it current (it replaces the chat's memory). The plan itself is in `Docs/clube-objectives.md` under that section (GL589–GL636).

## How the planning chat works
- It only plans, dispatches and relays. It never reads code, diffs or the history doc, and never implements.
- One batch at a time (one checkout, one Editor). Dispatch = a fresh session (when the user is at the desktop and asks in the chat) or a background agent (from a scheduled, Remote Control or agent-triggered turn, where the app refuses sessions). The prompt is short: "Read `Docs/batch-brief.md`, then do batch X: GL IDs, cut `<new>` from `<chain tip>`, notes." Model: Fable for batches.
- On a batch's report: relay its 12 lines or fewer to the user with the PR link, update this file if the batch did not, dispatch the next. If a batch stops on a usage limit, resume the same agent or session with a message after the reset; do not start a new one.
- New feedback from the user: a new dated backlog file with their words, GL entries with line links, committed to `main` (use a temporary `git worktree` of `origin/main` while a batch holds the checkout), then slot it into the queue below.
- Start a fresh planning chat when this one passes about 100k tokens: this file is the handoff.

## State (updated 2026-11-12)
- **Chain tip:** `gl627/ore-looks-skills` (batch E is on `gl608/character-models`, cut from it).
- **Open PRs, to merge in order:** #132 A map · #133 B settlements and roads plan · #134 C MapGen scene and M map · #135 K iron, coal, steel · #136 sea LOD · #137 D animals · #138 L ore looks, 10x XP, skill cheats. Each PR shows the earlier ones until they merge. The user merges; the planning chat does not.
- **Running:** batch E (GL608, character models), a background agent of the previous planning chat; it reports there.

## Queue, in order
1. **M — map screen and new-game preview** (GL635, GL636), branch `gl635/map-polish`. Grows `WorldMapScreen` and `MapPicture` (`Assets/Scripts/Game/Map/`, `Core/Generation/Map/`); the new-game menu preview must be fast for a seed change (a coarse height sample, not the full water and survey build, which takes 6–8 s).
2. **H — settlements and roads in the world** (GL613, GL614, GL620, GL631–GL634). Large: split in two if needed. H1 roads: smoothing, slope rules from the player's walkable limit, a Dead Grass material and item appended at the end of `MaterialRegistry`, dirt and cobble surfaces, signposts, bridges. H2 settlements on their parcels growing `TownPlan` and `TownBuilder`, shops per `EconomySpecialty`. Roads today are 40 m polylines in `WorldPlan`.
3. **F — tools and dropped items** (GL609–GL611).
4. **G — inventory icons** (GL612), after F (icons come from the new tool models).
5. **I — NPC work** (GL615, GL616), uses E's figure system and D's animals.
6. **J — thieves and boats** (GL617, GL618); the *Edmund Fitzgerald* already has its route in `WorldPlan`.

## Open with the user
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
