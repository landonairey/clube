# Benchmarks

Results of the performance experiments in `clube-objectives.md`. Each section
records how it was measured, the numbers, and the decision taken, so a
later run can be compared against it.

## K11 — Mesh building: `List<T>` vs preallocated arrays

**Question.** Does the mesher get faster if it writes vertices and indices
into preallocated arrays instead of `List<T>`?

**Method.**
- `MeshStorageBenchmark` (`Clube.Debug`), run from
  *Clube → Benchmarks → Mesh storage: List vs array (K11)*.
- Every variant runs the same flat-shaded Marching Cubes loop
  (`StorageMesher`), generic over the storage type. Each storage is a struct,
  so its calls are direct and storage is the only thing that differs.
- The variants:
  - **List, new each build:** fresh lists every build, as naive code would do.
  - **List, reused:** lists cleared and refilled, keeping their capacity. This
    is what `ChunkView` does today.
  - **Array, preallocated:** arrays sized once for the worst case: 5 triangles,
    so 15 vertices and 15 indices, in every voxel.
- The core `ChunkMesher` runs as a reference row. Every variant's vertices
  and indices are checked against its output ("Matches core").
- Terrain: fractal 2D Perlin hills (seed 1) through the middle of a cubic
  chunk, scaled so the shape is the same at every size. Voxel size 1, iso 0.5.
- Timing: 5 warmup builds, then 30 timed builds with `Stopwatch`. The median
  is the headline; the mean also catches any garbage collection pauses.
  "Upload" is `SetVertices` + `SetTriangles`, without normals.
- Allocations: `GC.GetAllocatedBytesForCurrentThread` always returns 0 on
  Unity's Mono, so this measures heap growth (`GC.GetTotalMemory`) instead,
  averaged over the runs. The heap grows a page at a time, so figures of a
  few KB are noise.

**Conditions.** Unity 6000.3.25f1, in the Editor with Release code
optimization (Mono), Intel Core i9-10900K (20 threads). This is not a player
build, so treat the numbers as relative to each other.

| Chunk | Variant | Triangles | Mesh ms (median) | Mesh ms (mean) | Upload ms (median) | Allocated per build | Kept between builds | Matches core |
|---|---|--:|--:|--:|--:|--:|--:|:-:|
| 8³ | Core `ChunkMesher` (reference) | 192 | 0.20 | 0.21 | 0.00 | 0 B | 16.0 KB | yes |
| 8³ | List, new each build | 192 | 0.20 | 0.20 | 0.00 | 40.0 KB | 0 B | yes |
| 8³ | List, reused | 192 | 0.19 | 0.20 | 0.00 | 1.9 KB | 16.0 KB | yes |
| 8³ | Array, preallocated | 192 | 0.19 | 0.20 | 0.00 | 0 B | 120.0 KB | yes |
| 16³ | Core `ChunkMesher` (reference) | 674 | 1.42 | 1.42 | 0.01 | 0 B | 32.0 KB | yes |
| 16³ | List, new each build | 674 | 1.32 | 1.32 | 0.01 | 80.0 KB | 0 B | yes |
| 16³ | List, reused | 674 | 1.33 | 1.34 | 0.01 | 0 B | 32.0 KB | yes |
| 16³ | Array, preallocated | 674 | 1.32 | 1.34 | 0.01 | 137 B | 960.0 KB | yes |
| 32³ | Core `ChunkMesher` (reference) | 2,766 | 10.43 | 10.44 | 0.03 | 1.6 KB | 256.0 KB | yes |
| 32³ | List, new each build | 2,766 | 10.15 | 10.15 | 0.03 | 552.0 KB | 0 B | yes |
| 32³ | List, reused | 2,766 | 10.07 | 10.07 | 0.03 | 0 B | 256.0 KB | yes |
| 32³ | Array, preallocated | 2,766 | 9.83 | 9.84 | 0.03 | 1.9 KB | 7.5 MB | yes |
| 64³ | Core `ChunkMesher` (reference) | 10,832 | 80.62 | 80.71 | 0.14 | 2.4 KB | 512.0 KB | yes |
| 64³ | List, new each build | 10,832 | 76.92 | 77.19 | 0.14 | 1.0 MB | 0 B | yes |
| 64³ | List, reused | 10,832 | 76.27 | 76.82 | 0.14 | 2.3 KB | 512.0 KB | yes |
| 64³ | Array, preallocated | 10,832 | 76.20 | 76.75 | 0.14 | 2.3 KB | 60.0 MB | yes |

A second run at 32³ and 64³ gave the same order and was within 2% of these.

**Findings.**
- **Storage isn't the bottleneck.** Reused lists and preallocated arrays are
  within about 1% of each other at every size. That's inside run-to-run noise.
- **Making new lists every build** is about 1% slower and creates garbage:
  roughly 1 MB per build at 64³. It never showed up as a GC pause here, but
  across many chunks streaming in (Chapter 4) it would.
- **Worst-case arrays waste memory.** They hold 60 MB at 64³ to store under
  0.5 MB of mesh. Real surfaces fill a tiny fraction of the worst case.
- **The time goes into visiting voxels**, not into writing the mesh:
  - 64³ takes ~77 ms for 10.8k triangles, about 290 ns per voxel, and most
    voxels are empty.
  - Each voxel does 8 density reads through the `IVoxelStorage` interface,
    and recomputes its case index (through `IReadOnlyList<float>`) and its
    crossed-edge mask from scratch.
  - Neighbouring voxels share 4 corners, yet every corner is read again.
- **Upload is negligible.** Under 0.2 ms even at 64³; it doesn't depend on
  the storage.
- The core `ChunkMesher` is ~4% slower than the benchmark loop. Its vertex
  writer and edge placer are reached through interfaces (A6), and it builds
  a writer per call. That's a fair price for swappable variants.

**Decision.** Keep `List<T>`, reused between builds (the current Core
implementation). Arrays aren't faster, and worst-case preallocation costs a
lot of memory. Speed-ups belong in the per-voxel loop: K32 for
single-threaded changes, K12 for Jobs/Burst.

## K32 — Speeding up the mesher's per-voxel loop

**Question.** K11 showed the mesher's time goes into visiting voxels
(~300 ns each at 64³, most of them empty). How much of that can a
single-threaded loop win back, without leaving the A12 storage interface
or the A6 strategy objects?

**Method.**
- `MesherSpeedBenchmark` (`Clube.Debug`), run from
  *Clube → Benchmarks → Mesher speed (K32)*. It times the core
  `ChunkMesher.Build` itself, flat and smooth, on K11's terrain
  (`BenchmarkTerrain.Hills`: fractal 2D Perlin, seed 1, voxel size 1, iso 0.5).
- Timing as in K11: 5 warmup builds, then the median of 30 timed builds.
  "ns per voxel" spreads the median over every voxel in the chunk.
- Each change was measured on its own, in the order below, and committed
  separately. `ChunkMesherTests.RandomChunk_MatchesPolygonisePerVoxel`
  checks after every step that the chunk loop still gives exactly what
  running `MarchingCubes.Polygonise` voxel by voxel gives (random densities,
  every edge placement and shading).

**Conditions.** As K11: Unity 6000.3.25f1, in the Editor with Release code
optimization (Mono), Intel Core i9-10900K (20 threads).

Median build time in ms, flat / smooth shading:

| Step | 8³ | 16³ | 32³ | 64³ | 64³ ns per voxel (flat) |
|---|--:|--:|--:|--:|--:|
| Before (K11's loop) | 0.20 / 0.23 | 1.39 / 1.49 | 10.48 / 10.91 | 80.99 / 82.83 | 309 |
| 1. Crossed-edge mask as a 256-entry table | 0.18 / 0.21 | 1.20 / 1.30 | 8.93 / 9.35 | 68.59 / 70.41 | 262 |
| 2. Skip voxels with no crossed edge early | 0.16 / 0.20 | 1.10 / 1.21 | 8.23 / 8.63 | 63.22 / 64.85 | 241 |
| 3. Read a Z layer at a time; reuse the 4 shared corners | 0.05 / 0.08 | 0.20 / 0.30 | 1.01 / 1.41 | 5.52 / 7.05 | 21 |

Triangle counts are unchanged at every step (192, 674, 2,766 and 10,832).
A second run of the final loop matched step 3 within 1%.

**Findings.**
- **Reading densities was the cost.** Step 3 alone is an 11× speed-up.
  Before it, every voxel made 8 reads, each one building a `Vector3Int`
  from the corner table, calling `Chunk.GetDensity`, then the
  `IVoxelStorage` interface, then a bounds check. Now each Z layer is
  copied once through `IVoxelStorage.ReadLayer` (one block copy for flat
  storage), and each voxel reads 4 new samples from a plain array, taking
  the other 4 and their solid bits from the voxel before it.
- **The table and the early skip** are worth about 15% and 8% on their own.
  The skip was small because, until step 3, a skipped voxel had already
  paid for its 8 reads.
- **Overall:** 64³ goes from 81 to 5.5 ms flat (15×) and 83 to 7.1 ms
  smooth (12×). That's ~21 ns per voxel, now close to the cost of the
  surface voxels themselves.
- **Smooth shading now costs about 28% more than flat** (it was 2%). Its
  `SharedVertexWriter` looks up every vertex in a `Dictionary`, which K32
  didn't touch; it's the next thing to try if smooth meshing matters.
- **Layer buffers are kept between builds,** one pair per thread, so a
  build allocates nothing new for them. That's ready for meshing off the
  main thread in Chapter 4.

**Decision.** All three changes are in Core. `IVoxelStorage` gains
`ReadLayer`, which the K24/K25 storage schemes (RLE, octree) must also
implement; both can fill a layer efficiently. K12 (Jobs/Burst) is now
measured against this loop, not K11's.

### K32 at world scale

2D was meant to be measured on a multi-chunk world once 3A existed.
`WorldMeshBenchmark` (*Clube → Benchmarks → World meshing (K32)*) loads
and meshes every chunk a `WorldView` streams in around the origin
(`StreamingArea`), using WorldLab's config: 16³ chunks, 2 layers, fractal
2D Perlin, flat shading. It times generation (`World.Load`) and meshing
separately; no renderers or mesh upload. Median of 5 runs after 1 warmup.

Both runs were in batch mode on a copy of the repo, with the same
benchmark: once on `main` plus the benchmark ("before"), once on the K32
branch ("after"). Same machine and Unity version as above, Release code
optimization.

| Render distance | Chunks | Triangles | Generate ms | Mesh ms before | Mesh ms after | Per chunk before → after |
|--:|--:|--:|--:|--:|--:|--:|
| 4 (WorldLab) | 98 | 32,376 | 375 | 127.4 | 12.7 | 1.30 → 0.13 ms |
| 6 (game setting) | 226 | 75,268 | 867 | 294.7 | 29.0 | 1.30 → 0.13 ms |
| 8 | 394 | 131,246 | 1,505 | 514.0 | 50.7 | 1.30 → 0.13 ms |

**Findings.**
- **World meshing is 10× faster** (less than the 15× at 64³, because a
  16³ chunk has more surface per voxel). Every lab and the game get it:
  `ChunkView` and the world's `ChunkRenderer`s both build through
  `ChunkMeshBuilder` → `ChunkMesher`.
- **Generation is now the cost:** ~3.8 ms per chunk against 0.13 ms to
  mesh it, so 97% of loading a world. Before K32 it was 75%. Fractal 2D
  Perlin samples 4 octaves for every sample, although a heightfield only
  needs one height per column. That's K35.

## K12 — Burst-compiled mesher (Jobs + native arrays)

**Question.** How much faster is the K32 mesher as a Burst-compiled job,
and how much more do we get by meshing many chunks at once on the worker
threads (the start of Chapter 4's threading)?

**Method.**
- `BurstChunkMesher` (Core) runs the K32 loop as an `IJob` over native
  arrays: densities copied in a Z layer at a time through
  `IVoxelStorage.ReadLayer` (A12), the Marching Cubes tables flattened, and
  output into `NativeList`s. It gives the same mesh as `ChunkMesher`
  (`BurstChunkMesherTests`: same counts and indices, positions within 1e-5).
- `MesherSpeedBenchmark` now times both meshers per size and shading; the
  Burst time includes copying the densities in. `WorldMeshBenchmark` adds
  the Burst job one chunk at a time, and one job per chunk all scheduled
  before any is waited on.
- In the Editor, Burst jobs run with safety checks (bounds checks on every
  native access) unless *Jobs → Burst → Safety Checks* is off; player
  builds have them off. Both are shown.

**Conditions.** As K32: Unity 6000.3.25f1, Editor, Release code
optimization, Intel Core i9-10900K (20 threads). Burst 1.8.30.

Single chunk, median ms (managed / Burst):

| Chunk | Shading | Managed | Burst, safety checks on | Burst, safety checks off |
|---|---|--:|--:|--:|
| 16³ | Flat | 0.21 | 0.14 | 0.04 |
| 16³ | Smooth | 0.32–0.43 | 0.16 | 0.05 |
| 32³ | Flat | 1.03 | 0.82 | 0.19 |
| 32³ | Smooth | 1.46–1.97 | 0.93 | 0.25 |
| 64³ | Flat | 5.59–7.29 | 5.61 | 1.35 |
| 64³ | Smooth | 7.27–7.55 | 6.06 | 1.59 |

(Managed ranges are the two runs; the second ran alongside Burst
recompiling with checks off.)

World scale (WorldLab's 16³ chunks, 2 layers, flat), safety checks off:

| Render distance | Chunks | Generate ms | Managed ms | Burst ms | Burst, parallel ms |
|--:|--:|--:|--:|--:|--:|
| 4 | 98 | 380 | 12.9 | 3.4 | 1.0 |
| 6 | 226 | 868 | 29.4 | 7.3 | 2.2 |
| 8 | 394 | 1,514 | 51.2 | 12.6 | 4.0 |

With safety checks on, Burst is 11.6 / 27.3 / 47.6 ms one at a time and
1.7 / 3.8 / 6.7 ms in parallel.

**Findings.**
- **Burst is 4–5× the K32 loop** on one chunk once safety checks are off
  (64³: 1.35 against ~5.6 ms), and **13× in parallel** across a world
  (98 chunks: 1.0 against 12.9 ms). The Editor's safety checks cost most of
  the single-chunk gain, so judge Burst with them off.
- **Smooth shading costs Burst almost nothing extra** (a native hash map
  instead of the managed `Dictionary`), where it costs the managed mesher
  30–90%.
- **Meshing is no longer a cost worth chasing:** a whole render distance 4
  world meshes in 1 ms on the workers, while generating it takes 380 ms.
  Generation (K35) is next, and it would parallelise the same way.

**Decision.** Keep both meshers behind `WorldConfig.Mesher` (A6): managed
stays the default and is what step-through records (A11); labs and the
game can pick Burst. Streaming chunks through parallel jobs belongs to
Chapter 4 and isn't wired into `WorldView` yet.

## K26 — Voxel storage schemes (K23-K28)

**Question.** How do the storage schemes compare for memory, serialized
size, meshing reads and brush writes, and does single-byte quantization
change the picture?

**Method.**
- `StorageBenchmark` (`Clube.Debug`), run from
  *Clube → Benchmarks → Voxel storage (K26)*. Every scheme sits behind
  `IVoxelStorage` and passes the same contract tests (`VoxelStorageTests`).
- Schemes: flat float (K23), flat byte (K28), run-length along X, Y and Z
  (K24), and the octree (K25) with 8³ and 4³ bricks (max depth set so a
  leaf at the bottom is a brick of that size).
- Terrains, scaled to the chunk: **Hills** (fractal 2D Perlin, as K11) and
  **Caves** (3D Perlin, overhangs and pockets).
- **Memory** is the storage's own estimate (`MemoryBytes`: array lengths at
  their current capacity plus array headers), not a GC measurement.
  **Serialized** is `VoxelStorages.Write`'s output.
- **Fill**: `ChunkGenerator.Fill` into empty storage, median of 5 (mostly
  the generator's own cost, so differences are the storage's writes).
  **Mesh**: managed `ChunkMesher.Build`, median of 15 (reads go through
  `ReadLayer`). **Brush**: 40 hard strokes near the surface, digging and
  adding in turn, median per stroke.
- **Quantized** rows refill run-length Y and the octrees with densities
  rounded to 1/255, as byte storage would hold them.

**Conditions.** Unity 6000.3.25f1, Editor, Release code optimization,
Intel Core i9-10900K.

64³ chunks (4,225 lines of 65 samples per axis):

| Terrain | Storage | Memory | vs flat | Serialized | Structure | Fill ms | Mesh ms | Brush ms/stroke |
|---|---|--:|--:|--:|---|--:|--:|--:|
| Hills | Flat float | 1.0 MB | 1.00× | 1.0 MB | – | 215.7 | 5.54 | 0.30 |
| Hills | Flat byte | 268 KB | 0.25× | 268 KB | – | 219.3 | 6.92 | 0.31 |
| Hills | Run-length X | 429 KB | 0.40× | 86 KB | 13,180 runs | 216.3 | 5.90 | 0.33 |
| Hills | Run-length Y | 396 KB | 0.37× | 107 KB | 16,900 runs | 217.3 | 5.94 | 0.33 |
| Hills | Run-length Z | 431 KB | 0.40× | 86 KB | 13,254 runs | 216.6 | 7.66 | 0.33 |
| Hills | Octree, 8³ bricks | 524 KB | 0.49× | 366 KB | 601 nodes, 182 bricks | 250.6 | 6.21 | 0.42 |
| Hills | Octree, 4³ bricks | 304 KB | 0.28× | 176 KB | 2,057 nodes, 678 bricks | 231.2 | 6.14 | 0.41 |
| Caves | Flat float | 1.0 MB | 1.00× | 1.0 MB | – | 63.8 | 7.93 | 0.31 |
| Caves | Flat byte | 268 KB | 0.25× | 268 KB | – | 65.2 | 9.11 | 0.32 |
| Caves | Run-length X | 448 KB | 0.42× | 101 KB | 15,763 runs | 68.7 | 8.29 | 0.35 |
| Caves | Run-length Y | 430 KB | 0.40× | 110 KB | 17,291 runs | 69.4 | 8.35 | 0.33 |
| Caves | Run-length Z | 440 KB | 0.41× | 97 KB | 15,189 runs | 66.4 | 10.32 | 0.33 |
| Caves | Octree, 8³ bricks | 524 KB | 0.49× | 470 KB | 713 nodes, 234 bricks | 99.0 | 8.40 | 0.40 |
| Caves | Octree, 4³ bricks | 304 KB | 0.28× | 232 KB | 2,585 nodes, 897 bricks | 80.6 | 8.43 | 0.41 |

Smaller chunks, memory against flat float (Hills / Caves):

| Storage | 16³ | 32³ |
|---|--:|--:|
| Flat byte | 0.25× / 0.25× | 0.25× / 0.25× |
| Run-length X | 1.54× / 1.57× | 0.79× / 0.82× |
| Run-length Y | 1.41× / 1.53× | 0.73× / 0.79× |
| Octree, 8³ bricks | 3.38× / 3.38× | 0.93× / 1.85× |
| Octree, 4³ bricks | 0.99× / 0.99× | 0.54× / 0.54× |

Quantized to 1/255, run-length Y and the octrees change by under 1% (e.g.
Hills 64³ run-length Y: 16,866 runs instead of 16,900).

**Findings.**
- **Run along X or Z, not Y.** K24 expected vertical runs to win because
  terrain is layered, but every column crosses the surface, so each Y line
  is at least three runs (solid, the ramp, air). Most horizontal lines lie
  wholly in solid or air and stay one run: X and Z need ~22% fewer runs and
  serialize ~20% smaller. Z reads cost the most, though (7.7 against 5.9 ms
  to mesh), because a Z layer crosses every line; X keeps reads cheap.
- **Run-length is the best saved form:** 86 KB against 1 MB flat (12×) at
  64³, the natural format for saving edited chunks (S2). In memory it's
  held back by two small arrays per line (0.37-0.42×), and at 16³ that
  overhead makes it bigger than flat.
- **The octree with 4³ bricks is the smallest in memory** (0.28×) at 64³
  and 32³, but larger bricks waste space (8³ bricks: 0.49×, and 1.85× on
  32³ caves) and both cost ~35% more per brush stroke and up to 16% more
  per fill. Its array doubling also shows: memory is capacity, not use.
- **Single bytes (K28) are the simple 4× win:** 0.25× memory with no
  structure, the same brush cost, reads ~20% slower (byte to float). The
  mesh moves by 0.004-0.005 voxels on average; a few samples right at the
  iso level round across it and change a voxel's case (up to ~0.5 voxels,
  2 extra vertices in 5,587 at 64³ after soft-brush edits): no visible
  stepping. 0 and 1 stay exact, so the brush's solid and empty tests hold.
- **Quantization doesn't help compression:** the surface's in-between
  values differ anyway, and solid and air were already exact.
- **Reads are not the bottleneck either way:** every scheme meshes within
  about 40% of flat, and generation dominates the fill.

**Decision (for now, M12 decides at scale).** Flat float stays the
default: fastest and simplest. Candidates for M12 across a streamed world:
flat byte (4× smaller, no structure) and the 4³-brick octree (smallest in
memory). Run-length along X is the format to save edited chunks in.

## M12 — Storage at scale

**Question.** K26 compared the schemes on one chunk. Across a streamed
world, with chunks loading, unloading, edited and kept, which should the
game use?

**Method.**
- `WorldStorageBenchmark` (`Clube.Debug`), run from
  *Clube → Benchmarks → Storage at scale (M12)*, on two worlds made from
  WorldLab's config (fractal 2D Perlin, surface 12 m, amplitude 8 m):
  - **16³ chunks at 1 m voxels**, 2 layers, render distance 6: 226 chunks;
  - **32³ chunks at 0.25 m voxels** (4 per metre, the finer size M23 tests),
    4 layers, render distance 4: 196 chunks.
- For each scheme: load everything around the origin (`World.Load`:
  generation plus the storage's writes); total `MemoryBytes`; mesh every
  chunk (managed); 60 hard 3 m brush strokes across the area
  (`World.ApplyBrush`, so across chunk borders); the serialized size of the
  edited chunks (what a save holds, S2); then walk 8 chunks along X,
  unloading what leaves the area and loading what enters, as `WorldView`
  does. Memory after the walk includes edited chunks kept while unloaded.
- One run per scheme; the load and walk times are dominated by terrain
  generation, so their differences are the storage's.

**Conditions.** Unity 6000.3.25f1, Editor, Release code optimization,
Intel Core i9-10900K.

| World | Storage | Memory | vs flat | Load ms | Mesh ms | Stroke ms | Save (edited) | Walk ms/step | Memory after walk |
|---|---|--:|--:|--:|--:|--:|--:|--:|--:|
| 16³ at 1 m | Flat float | 4.2 MB | 1.00× | 893 | 32.6 | 0.07 | 1.2 MB | 101.7 | 5.3 MB |
| 16³ at 1 m | **Flat byte** | **1.1 MB** | **0.25×** | 892 | 34.5 | 0.07 | 318 KB | 102.6 | **1.3 MB** |
| 16³ at 1 m | Run-length X | 5.8 MB | 1.37× | 905 | 32.5 | 0.07 | 317 KB | 104.1 | 7.4 MB |
| 16³ at 1 m | Run-length Y | 5.4 MB | 1.28× | 902 | 32.2 | 0.07 | 391 KB | 105.2 | 6.9 MB |
| 16³ at 1 m | Octree, 4³ bricks | 2.4 MB | 0.58× | 926 | 33.3 | 0.07 | 795 KB | 104.9 | 3.6 MB |
| 32³ at 0.25 m | Flat float | 26.9 MB | 1.00× | 5,599 | 111.8 | 3.39 | 10.4 MB | 1,026 | 37.3 MB |
| 32³ at 0.25 m | **Flat byte** | **6.7 MB** | **0.25×** | 5,633 | 141.2 | 3.50 | 2.6 MB | 1,039 | **9.3 MB** |
| 32³ at 0.25 m | Run-length X | 21.2 MB | 0.79× | 5,669 | 123.0 | 3.58 | 2.1 MB | 1,041 | 30.9 MB |
| 32³ at 0.25 m | Run-length Y | 21.4 MB | 0.80× | 5,694 | 122.2 | 3.55 | 2.3 MB | 1,042 | 31.1 MB |
| 32³ at 0.25 m | Octree, 4³ bricks | 10.2 MB | 0.38× | 5,800 | 120.3 | 4.13 | 4.2 MB | 1,069 | 17.5 MB |

(66 chunks edited in the first world, 76 in the second.)

**Findings.**
- **Single bytes win at scale:** a quarter of the memory in both worlds,
  loaded and after walking, at the same load, stroke and walk cost. The
  structured schemes can't catch up: across a world most chunks hold some
  surface, and each one pays its own overhead.
- **Run-length loses in memory at scale** (1.28-1.37× flat at 16³, where
  each line's two small arrays outweigh the data); it only wins on save
  size, and there it only ties single bytes at 16³ (317 against 318 KB)
  and beats them by 20% at 32³.
- **The octree is second** (0.38-0.58×), but costs up to 22% more per
  stroke and has the most code to get right.
- **Byte reads cost meshing up to 26%** (converting to floats), small next
  to generation; the Burst mesher (K12) copies layers in either way.
- **Generation is the whole cost of streaming**: ~4 ms per 16³ chunk and
  ~29 ms per 32³ chunk at 0.25 m, so a step of the walk takes 0.1-1 s.
  That's K35, and the reason to move loading onto jobs in Chapter 4.
- **The finer voxel size costs 6× the memory** for the same ground (26.9
  against 4.2 MB in flat floats) and 7× the generation: worth knowing
  before M23 settles the gameplay voxel size.

**Decision.** The game stores densities as **single bytes**
(`VoxelStorageType.FlatByte`, K28), now the `WorldConfig` default and
WorldLab's setting. VoxelLab and ChunkLab stay on flat floats, so their
corner values stay exact for teaching. Saves (S2) can write the bytes as
they are; compressing them (run-length or a general-purpose compressor) is
a choice for S2, worth up to 20% on top.
