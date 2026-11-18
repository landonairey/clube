20261118 Notes (answers to the X2, X7 and X8 follow-ups, PRs #173, #178 and #179)

Keep the map active for the tutorial for now

Add the tutorial map support, sea hole fix and load screen freeze fix in a short next batch

Tutorial map, the open question from X2 (PR #173): the `Tutorial` scene has no map, so M does nothing there; the user wants the map to work in the tutorial.

Sea hole, from X7 (PR #178): at world position (2490, 60, 2550) a large area of open sea has no water over its sand floor; `main`'s old shader shows the same hole; the water clipping is the suspect, not diagnosed.

Load screen freeze, from X8 (PR #179): the map survey (`ResourceSurvey`) blocks the load screen for up to 30 s at a time while the game loads, so the progress text stops updating; X8 left it out of PR #179; usual remedy would be spreading it over frames or a thread, not investigated.
