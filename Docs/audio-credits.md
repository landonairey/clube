# Audio credits

Every sound in `Assets/Audio/` and where it came from. Add a row for each new source; CC-BY
sounds need their author named here (and in the game's credits).

All current sounds are by **Kenney Vleugels ([kenney.nl](https://kenney.nl))**, licensed
**CC0 1.0** ([creativecommons.org/publicdomain/zero/1.0](http://creativecommons.org/publicdomain/zero/1.0/)):
free for commercial use, no credit required. Downloaded 2026-10-28.

| Folder | Used for | Pack | Files |
|---|---|---|---|
| `Footsteps/Stone` | stone steps | [Impact Sounds](https://kenney.nl/assets/impact-sounds) | `footstep_concrete_000-004` |
| `Footsteps/Grass` | grass and dirt steps | Impact Sounds | `footstep_grass_000-004` |
| `Footsteps/Clay` | clay steps; any hit on dirt, grass, clay | Impact Sounds | `footstep_snow_000-004` |
| `Tools/Hand` | hand | Impact Sounds | `impactSoft_medium_000-004` |
| `Tools/HandAxe` | hand axe | Impact Sounds | `impactWood_light_000-004` |
| `Tools/StonePick` | stone pick | Impact Sounds | `impactGeneric_light_000-004` |
| `Tools/StoneAxe` | stone hatchet | [RPG Audio](https://kenney.nl/assets/rpg-audio), Impact Sounds | `chop`, `impactWood_medium_000-004` |
| `Tools/Other` | other tools | Impact Sounds | `impactPlate_light_000-004` |
| `Tools/Chisel` | chisels | Impact Sounds | `impactMetal_light_000-004` |
| `Tools/Leaves` | any hit on leaves | RPG Audio | `cloth2`, `cloth4`, `handleSmallLeather` |
| `Actions/Craft` | Crafting recipes (worktops) | RPG Audio | `knifeSlice`, `knifeSlice2` |
| `Actions/Smelt` | Smelting recipes (buns) | Impact Sounds | `impactTin_medium_000-004` |
| `Actions/Smith` | Blacksmithing recipes (anvil, flat rock) | Impact Sounds | `impactMetal_heavy_000-004` |
| `Actions/Sell` | selling | RPG Audio | `handleCoins`, `handleCoins2` |
| `Actions/Place` | placing | Impact Sounds | `impactWood_heavy_000-004` |
| `Actions/Parcel` | buying land | [Interface Sounds](https://kenney.nl/assets/interface-sounds), RPG Audio | `confirmation_002`, `handleCoins2` (played together) |

## Meteor (GL870)

| Path | Used for | Source | Licence |
|---|---|---|---|
| `Resources/Meteor/Meteor flyby.wav` | a meteor coming in | **placeholder**, synthesised (2026-11-25). Wanted: [meteor flyby](https://pixabay.com/sound-effects/film-special-effects-meteor-flyby-14766/) by freesound_community | Pixabay Content License |
| `Resources/Meteor/Meteor impact.wav` | its impact | **placeholder**, synthesised (2026-11-25). Wanted: the middle of [Meteor Impact Sound Effect](https://pixabay.com/sound-effects/film-special-effects-meteor-impact-sound-effect-sound-effects-224185/) by Alice_soundz (set the window with `impactStart` / `impactLength` on `Resources/Meteor/MeteorSounds.asset`) | Pixabay Content License |

Pixabay Content License: free to use, also commercially, no credit required (credited here anyway). The automatic download was blocked, so the placeholders stand until the clips are dropped in: download each, put it in `Assets/Resources/Meteor/` and assign it on `MeteorSounds.asset` (or replace the placeholder file of the same name).

Wanted, not in these packs: fire or bellows for smelting, a grittier rock-pick strike, dirt digging.

## Fonts

| Font | Used for | Licence |
|---|---|---|
| [Uncial Antiqua](https://fonts.google.com/specimen/Uncial+Antiqua) by Brian J. Bonislawsky (Astigmatic) | every screen, HUD, signboard and the map (`GameFont`, GL732) | SIL Open Font License 1.1; text in `Assets/Resources/Fonts/UncialAntiqua-OFL.txt` |
