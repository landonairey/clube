# Walking FPS report: batches X9 to X18 (2026-11-18 to 2026-11-19)

Goal: a smooth 60 FPS while walking. Measured in the IL2CPP release build, `clube.exe -walkbench` on MapGen (seed 1,
1080p, RTX 3080, i9-10900K), the same 1,127 m road route at 6 m/s. Figures are from each batch's own report; "1% low"
and "main thread" are given where the batch reported them. Different sessions re-measured their own baselines on the same
machine, so run-to-run noise of a few FPS (and a few more between sessions) applies.

## Start and end

| | FPS (average) | 1% low |
|---|---:|---:|
| Your playtest before this work | 5 to 8 | n/a |
| Walk benchmark baseline (before X9) | 14.0 | n/a |
| **After X18 (end)** | **152.3** | **43.3** |

Worst frame at the end: 64.3 ms. Main thread at X14: 12.3 ms (not re-measured after).

## Per batch

| Batch | PR | Change | Average FPS after | 1% low after | Other |
|---|---|---|---:|---:|---|
| X9 | #183 | first speed-ups | 31.6 | n/a | |
| X10 | #188 | frame breakdown, bushes, shared masks, `FrameBudget` | 37.5 | 10.4 | main thread 25.4 ms; settlement stall 708 to 107 ms |
| X11 | #189 | slicing big edits | 44.8 | 23.8 | |
| X12 | #190 | IL2CPP build | 55.3 | 27.0 | |
| X13 | #191 | sparse chunks | 68.5 | 29.0 | managed heap about 300 MB larger |
| X14 | #192 | chunk hand-off, ore, column stamps in jobs | 82.1 | 32.6 | main thread 12.3 ms; streaming 3.86 to 1.92 ms; frames over 16 ms from streaming 31 to 5 |
| X15 | #193 | grass, bushes, water in Burst jobs | 91.8 | n/a | each under 0.5 ms a frame |
| X16 | #194 | chunks drawn by 16 m region | 134.5 (its own baseline 93.4) | n/a | draw calls 5,566 to 1,117 |
| X17 | #195 | HUD off IMGUI (uGUI canvas plus one OnGUI host) | 127.9 vs 123.2 on main, same build | 38.9 vs 34.7 | worst frame 94.9 to 62.5 ms |
| X18 | #196 | allocation sweep | 152.3 vs 129.3 on X17 main | 43.3 vs 39.1 | GC collections over the walk 139 to 46 |

X16 and X17 disagree on the X16 end state (134.5 against 123 to 129 re-measured by X17): treat the 125 to 135 range as the
real figure after X16.

## What still stands between the game and a smooth 60

Average FPS is far past 60, but the 1% low is 43, so some frames still miss 16.7 ms:

1. **Garbage collection.** 46 collections in the 150 s walk against a target of 15 or fewer (about 3 per 15 s while
   walking). Half of them (23) happen in the load stretches, where the managed heap grows from 350 to 1,100 MB of chunk
   data. Next step is GL779 step 1: move chunk data out of the managed heap. It is a large change and not started.
2. **Villager hitches.** Over 16 ms in about 16 frames, worst 38 ms (X14 measurement).
3. **Settlement work-out and streaming frames.** Settlement workings (preparing digs and tree claims) and 5 streaming
   frames over 16 ms remain.
4. **Not hand-tested.** Digging after X13 and X14, ground collapse timing after X11, the labs after X16, the quest box in
   the early quests after X17. Benchmarks and tests passed (1141 EditMode tests), but a hand playtest of the new build is
   the real check on your 5 to 8 FPS.
