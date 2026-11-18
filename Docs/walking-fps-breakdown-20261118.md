# Walking frame rate: where the time goes (2026-11-18, GL770, batch X10)

Measured in the release build of the game (1080p, RTX 3080, i9-10900K, seed 1, render distance 64), walking 1127 m of road at
6 m/s with `clube.exe -walkbench`. Averages are over walking stretches 2 to 10, weighted by frames; the first stretch is still
the load settling.

## Result of this batch

| | Before (main, `130a037`) | After (X10) |
|---|---|---|
| FPS, whole walk | 32.9 | **37.5** |
| FPS, slowest 15 s after the first stretch | 26.5 | 31.9 |
| 1% low | 9.1 | 10.4 |
| Main thread, average frame | 28.9 ms | 25.4 ms |
| Longest settlement frame | 708 ms | 107 ms |
| Longest frame of all | 949 ms | 884 ms |
| Ground shown within 48 m, from the second stretch on | 100% | 100% |

**60 FPS is not reached.** The main thread has to lose another 8.7 ms on average, and the hitches below have to go.

## One frame, by system (after X10)

Main thread 25.4 ms. The GPU (7.2 ms) and render thread (3.4 ms) are not what limits the frame.

| Where | ms a frame | Worst frame (ms) | What it is |
|---|---:|---:|---|
| **Streaming** (`WorldView.Update`) | **6.58** | 56 | start generating 1.99, start meshing 1.94, add chunks 1.22, area 0.64, colliders 0.57, apply meshes 0.12, poll 0.10 |
| Roads being written | 2.51 | 868 | whole trees cut voxel by voxel inside one road piece |
| Grass | 2.32 | 20 | scan and grow 1.32, rebuild batches 0.61, draw and lawn the rest |
| Water | 1.99 | 15 | chunk meshes 1.15, flow 0.80 |
| Settlement workings | 1.54 | 94 | preparing digs and tree claims |
| Bushes | 1.12 | 13 | was 2.42 |
| Distant trees | 0.94 | 9 | was 1.57 |
| Chunk drawing (`WorldView.LateUpdate`) | 0.90 | 15 | |
| Carts | 0.77 | 12 | |
| Settlements being written | 0.65 | 107 | was 708 at worst |
| Falling ground | 0.42 | 156 | |
| Animals, birds, clouds, boats, coarse and far terrain, distant buildings, people | 0.88 | 81 | villagers are the 81 ms |
| Other scripts | about 0.4 | | |
| **Frame rendering on the main thread** (cameras, culling, IMGUI) | **3.14** | | `PostLateUpdate.FinishFrameRendering` |
| Physics | 0.35 | | |
| Everything else in the player loop | about 0.9 | | |

Also measured:

- **Worker threads:** the game keeps 3.1 of the machine's 20 logical cores busy: main thread 1.0, render thread and job workers 2.1.
- **Garbage collection:** 57 collections in 135 s, about one every 2.4 s, up to 11.6 ms each.
- **Drawing:** about 4,900 draw calls, 250 set-pass calls and 5.2 million triangles a frame, with 6,500-10,000 chunk renderers.
- **Backlog:** about 1.1 million chunks are still waiting to load at any moment of the walk, so streaming never finishes.

Not separated in the release build: the IMGUI share of the 3.14 ms. The Editor shows about 1.8 ms of `GUI.Repaint`.

## Fixed in this batch

1. **The settlement stall.** Every parcel a settlement locked rebuilt the player's whole fence (`LandParcels.Lock` raised `Changed`).
2. **Bushes.** Each meshed chunk asked for a look over some 30,000 cells, nearly every frame. Chunks now wait in a nearest-first list, and bushes draw from one uploaded buffer per mesh.
3. **Far-view coverage masks.** Four views each refilled the same 17,000-texel mask; they share one, and a refill visits only fully meshed columns.
4. **`FrameBudget`.** The backlog systems share 12.5 ms of script time a frame instead of each spending its own budget every frame.
5. **Measuring.** `FrameCost`, `PlayerLoopTimes` and `ProcessLoad` give this table from the release build, with no development build.

## What to do better, ranked

Gains are estimates from the table above and have not been measured. They overlap, so they don't add up.

| # | Finding | How well-running games do it | Expected gain | Effort |
|---|---|---|---|---|
| 1 | **The game ships on Mono.** About 21 ms of the frame is scripts, running on the slower of Unity's two C# back ends | Ship IL2CPP: C# compiled to native code | 4-8 ms | Small, once the C++ tools are installed |
| 2 | **The whole 3D volume is loaded.** 12,900 columns x 104 layers, though nearly every chunk is all air or all rock, so the backlog never empties and every system works flat out | Sparse worlds: a chunk the height map proves uniform is never generated, stored or meshed until something edits it | 3-6 ms, and fewer hitches | Medium to large |
| 3 | **Chunk hand-off runs on the main thread** while 17 cores sit idle: snapshots, copies and job set-up cost 5 ms | The main thread only schedules; data lives in native arrays and jobs do the copying | 3-5 ms | Large |
| 4 | **Big edits happen inside one frame**: tree cuts (868 ms), falling ground (156 ms), settlement preparation (94 ms), villagers (81 ms), boats (70 ms). These set the 1% low of 10 FPS | Long work is sliced or runs on a job, and its result is applied in small pieces | 1% low from 10 to 25+ FPS | Medium |
| 5 | **Flora and water build on the main thread** (grass 1.9, water 1.15, bushes about 0.5) | Scatter and mesh in Burst jobs | 2-3 ms | Medium |
| 6 | **One GameObject per chunk**: 4,900 draw calls | Merged regions or `BatchRendererGroup`, GPU culling and instancing | 1-2 ms, plus render-thread room | Large |
| 7 | **37 `OnGUI` methods** in the game's code, on a GUI system that lays out every frame | A retained UI that redraws on change | Up to 1-2 ms | Medium |
| 8 | **A collection every 2.4 s**, with a 500-900 MB managed heap | No per-frame allocations; chunk data outside the managed heap | 0.5-1 ms, and fewer 10 ms spikes | Medium |

## Proposed follow-up batches

These need GL numbers from a planning chat. Each should be measured with `-walkbench` before and after.

1. **Ship on IL2CPP.** Install Visual Studio's "Desktop development with C++" workload, switch the Windows player to IL2CPP, add `link.xml` for anything stripped, and play the demo from start to finish. Done when the build runs the whole walk and FPS before and after are reported.
2. **Sparse chunks.** Skip generating, storing and meshing chunks the height map proves all air or all solid, and load them on demand when something edits or digs into them. Done when the waiting chunk count falls by at least 80% and digging behaves as before.
3. **Slice big edits.** Spread `GroundLeveling.FloodClear`, `ClearSevered`, falling ground, `SettlementWorks.PrepareSteps` and villager start-up over frames. Done when no system takes over 16 ms in a frame on the walk.
4. **Chunk hand-off in jobs.** Move `AddGenerated`, the mesh snapshots and generation set-up into Burst jobs on native chunk data. Done when streaming is under 2 ms a frame.
5. **Flora and water in jobs.** Burst jobs for `GrassScatter.Scatter`, `BushScatter.Scatter` and `WaterView.BuildChunk`. Done when each is under 0.5 ms a frame while walking.
6. **Chunk rendering.** Draw chunks through `BatchRendererGroup` or merged regions. Done when draw calls fall below 1,500 with the same picture.
7. **HUD off IMGUI.** Start by switching off `OnGUI` on screens that are closed, then move the always-on HUD to a retained UI.
8. **Allocation sweep.** Find per-frame allocations with the Profiler in the Editor. Done when the walk has fewer than one collection every 10 s.

Suggested order: 1, 3, 2, then the rest. Batch 1 is the cheapest large gain, and batch 3 is what players will feel most.
