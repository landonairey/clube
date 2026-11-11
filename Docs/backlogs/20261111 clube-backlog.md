# clube — Backlog (2026-11-11)

Playtest notes given 2026-11-11 (tutorial playtest), kept as they were given. They are organized in `Docs/clube-objectives.md` under "Playtest 2026-11-11"; the GL IDs there say where each went. Like the earlier backlogs, this file is a snapshot: change the objectives doc, not this.

---

20261111 Playtest Notes (tutorial):

Fixes
- Crew Lead dialogue offers "Discuss ore" before the player has mined any ore. Only offer it once the player has mined (or holds) ore. It stays greyed out after first use.
- The tutorial starts at hour 9 so the player can dig straight away. The workers start the day in their beds, waking and walking to work as their normal schedule says. They must not spawn at the dig.
- Walking some distance from the dig site, grass appears again up the tutorial mountain side (grass/dirt bands on the far mountain wall). The earlier fix was a band-aid: find the root cause. The mountain side's material should be stone in the terrain's 3D data at its source, with no grass or dirt at any distance or after walking away and back.
- Sleeping in the tutorial gave a "quest done: sleep through the night" notification. Disable ALL quests while in the tutorial at the core: no quest progress, completion, notifications or HUD from the quest system until the tutorial hands over.
- In the tutorial, the rightmost miner got stuck in the mountain wall. Find why and fix it so no worker can end up inside terrain.
- In the tutorial, the player can craft a stone hand axe, which only fills inventory space because it can't be sold. Disable that recipe (and any other stone-tool hand recipe that makes unsellable items) while in the tutorial, at the source of the tutorial's recipe list rather than by hiding a button. Restore it after the tutorial hands over.
- Workers float in the air when the player digs under their feet. They should walk on the terrain surface (a nav mesh built from the terrain chunk colliders, updated locally, preferred; otherwise ground them every frame by a downward cast so they drop when the ground is dug away). Say which was done and why. Combine with the stuck-in-wall fix if it is the same movement code.
