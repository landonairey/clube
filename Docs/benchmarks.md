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

## K35 and P3 — Generation in jobs, and streaming without stalls

**Question.** WorldLab dropped to about 5 fps while chunks loaded, worst at
8 voxels per metre (M25's lead: 4 m chunks of 32³ voxels, 8 layers). Where
did the frame go, and how far can generation, meshing and colliders move off
the main thread?

**Before** (main at `deae91e`, measured in the Editor with a script around
`World.Load`, `ChunkMeshBuilder.Build` and a `MeshCollider` assignment;
WorldLab's terrain, render distance 2):

| World | Chunks | With surface | Generate ms/chunk | Mesh + materials ms (surface chunk) | Collider cook ms (surface chunk) |
|---|--:|--:|--:|--:|--:|
| 16³ at 1 m, 2 layers | 26 | 13 | 5.68 | 1.04 | 0.22 |
| 32³ at 1/8 m, 8 layers | 104 | 14 | 31.46 | 4.16 | 0.67 |

`WorldView` loaded 4 chunks a frame, each generated, meshed (with the
material pass) and its collider cooked on the main thread: ~130 ms a frame
at 1/8 m, so 5-8 fps, and render distance 6 (904 chunks) took about 28 s.
Every chunk paid the full generation cost, though **87% of them had no
surface** (all air above the ground or all solid below it).

**What changed** (see `architecture.md` for the design):
- Generation is Burst jobs (`Generation/Fill`): one height per column,
  shared by the chunks stacked on it; uniform chunks keep no density storage;
  a chunk above its column's highest point needs no job at all.
- Meshing is one Burst job per chunk, materials, normals and the vertex and
  index buffers included, written into `Mesh.MeshData`.
- Colliders are cooked in jobs (`Physics.BakeMesh`), only within 24 m of the
  focus: assigning a pre-baked 28,800-triangle mesh takes 0.03 ms, against
  8.2 ms to cook it on assignment.
- `WorldStreamer` keeps many jobs in flight across frames and holds taking
  their results to a main-thread budget (3 ms); chunk arrays are pooled.

**Conditions.** Unity 6000.3.25f1, Editor, Release code optimization,
**Burst safety checks on** (the Editor default; a player build is faster),
Intel Core i9-10900K (20 threads, 19 job workers). WorldLab's config:
fractal 2D Perlin (surface 12 m, amplitude 8 m), blended materials, three
ores, byte storage.

Generation alone, 32³ chunks at 1/8 m: **0.28-0.45 ms per chunk** on one
thread (was 31.5 ms); render distance 6's 904 chunks in **21 ms** with every
job in flight at once.

Streaming from nothing (`StreamingBenchmark`, *Clube → Benchmarks → World
streaming (P3)*: the real `WorldStreamer` with renderers, mesh upload and
colliders, one simulated 60 fps frame at a time). "Streamer ms/frame" is the
main-thread time it takes per frame while loading:

| World | Render distance | Chunks | With surface | Frames to settle | Time to settle | Streamer ms/frame (mean) | p95 | max |
|---|--:|--:|--:|--:|--:|--:|--:|--:|
| 16³ at 1 m | 4 | 98 | 49 | 8 | 0.17 s | 7.82* | 36.1* | 36.1* |
| 16³ at 1 m | 6 | 226 | 113 | 10 | 0.20 s | 2.25 | 4.28 | 4.28 |
| 16³ at 1 m | 8 | 394 | 197 | 16 | 0.37 s | 2.37 | 4.30 | 4.30 |
| 16³ at 1 m | 10 | 634 | 317 | 23 | 0.59 s | 2.35 | 3.62 | 3.78 |
| 32³ at 1/8 m | 4 | 392 | 57 | 14 | 0.38 s | 1.65 | 5.53 | 5.53 |
| 32³ at 1/8 m | 6 | 904 | 153 | 27 | 0.71 s | 1.88 | 4.94 | 5.66 |
| 32³ at 1/8 m | 8 | 1,576 | 274 | 48 | 1.26 s | 2.99 | 6.35 | 8.23 |
| 32³ at 1/8 m | 10 | 2,536 | 431 | 69 | 1.87 s | 2.69 | 5.75 | 6.06 |

\* The first row of a fresh Editor session includes the one-off JIT and
Burst compilation of the streaming code.

Walking at 5 m/s after settling (1/8 m, render distance 6), the streamer
took **0.83 ms a frame on average, 4.7 ms at most**.

**Findings.**
- **Most of a world is uniform.** At 1/8 m, three quarters of the chunks
  are all air or all solid and five in six have no surface: skipping their
  work is worth more than making it fast.
- **Jobs were never the limit once they existed.** Generation and meshing
  for a whole render distance take tens of milliseconds spread over 19
  workers. What decided the frame time was the main thread's share:
  a budget and an order for taking results (meshes before new chunks, so
  finished meshes aren't starved), no allocation per chunk (pooled arrays:
  a garbage collection on the Editor's heap stalled frames by 5-10 ms), and
  no work hidden in "cheap" calls (filling a new storage before
  overwriting it cost 0.4 ms per surface chunk; sorting the wanted list on
  every chunk border crossing cost 5.6 ms at render distance 10).
- **Remaining frame cost is scheduling**, not taking results: starting a
  few dozen jobs with their ore lookups and snapshots is outside the budget
  (`architecture.md`, target 1).
- **8 voxels per metre is now affordable to stream**, but render distance
  is in chunks, and 10 chunks is only 40 m at 4 m per chunk. Seeing further
  needs level of detail (P7).

**Decision.** The streamed world always generates and meshes with Burst
jobs through `ChunkPipeline`; `WorldConfig.Mesher` only picks the
single-chunk labs' mesher. K35 is done; P1-P3 are done for the lab world
(the lean `Game` scene, GW, builds on the same path).

## M17 — Chunk shape: tall columns vs stacked cubes

**Question.** Should a chunk be one tall column the full world height
(Minecraft-style) or a cube stacked in layers? And how big can a chunk be
before one mesh passes the 65,535 vertices a 16-bit index buffer addresses?

### The vertex limit (checkerboard)

A 16-bit index buffer can address 65,535 **unique vertices**; the triangle
count only matters through how many vertices the triangles need. The worst
case for marching cubes is a 3D checkerboard (alternating solid and air
samples): every edge is crossed and every voxel makes 4 triangles. Measured
with `ChunkMeshBuilder` (managed and Burst agree), for Flat shading (3
unshared vertices per triangle, what the game draws) and Smooth (one vertex
per crossed edge, shared):

| Voxels | Triangles | Unshared vertices | Shared vertices | Fits 16-bit |
|---|--:|--:|--:|---|
| 15³ | 13,500 | 40,500 | 11,520 | both |
| 16³ | 16,384 | 49,152 | 13,872 | both |
| 17³ | 19,652 | 58,956 | 16,524 | both |
| 16 x 48 x 16 | 49,152 | 147,456 | 40,528 | shared only |
| 27³ | 78,732 | 236,196 | 63,504 | shared only |
| 28³ | 87,808 | 263,424 | 70,644 | neither |
| 32³ | 131,072 | 393,216 | 104,544 | neither |

At most 5 triangles per voxel, so unshared vertices are at most 15 per voxel:
**16³ is the largest cube guaranteed to fit 16-bit unshared**, and shared
vertices (3 edges per sample, 3n(n+1)² at most) fit up to **27³**. 16 x 48 x
16 is 49,152 triangles but 147,456 unshared vertices; it fits only with shared
vertices.

The mesher switches a mesh to 32-bit indices past 65,535 vertices
(`ChunkMeshJob`, `ChunkMeshBuilder`), but not every GPU has 32-bit index
buffers (`SystemInfo.supports32bitsIndexBuffer`: older OpenGL ES 2.0 parts,
WebGL 1), so a mesh past the limit can fail to draw there. The material pass
doesn't raise the bound: its splitters copy a vertex at most once per
triangle using it, so any mode stays at 3 vertices per triangle (a 16³
checkerboard with a random material per sample gives the same counts in every
mode). On real terrain the largest mesh below is 16,860 vertices for a 32 x 640 x 32
column, a quarter of the limit.

### Columns vs cubes on the same terrain

`ChunkShapeBenchmark` (*Clube → Benchmarks → Chunk shape (M17)*) loads the
same 6 x 6 chunk columns twice: as 32³ cubes stacked 20 layers, and as
32 x 640 x 32 columns. It generates them (one after another, then in
parallel through `ChunkPipeline`), meshes them with the Burst mesher (same
two ways), counts the stored bytes, then makes 25 one-metre digs at surface
spots and times each one from the edit to every dirtied chunk remeshed,
uploaded and its collider baked.

Unity 6000.3.25f1, in the Editor with Release code optimization, Intel(R)
Core(TM) i9-10900K CPU @ 3.70GHz (20 threads). Terrain:
`ProceduralWorldLabWorldConfig`, Landscape, 0.25 m voxels, 160 m tall,
storage FlatByte, Flat shading, materials Blended.

| Area | Shape | Chunks | With surface | Stored | Generate ms | Generate ms (parallel) | Mesh ms | Mesh ms (parallel) | Vertices | Largest mesh | Over 16-bit | Dig ms (median) | Dig ms (max) | Chunks per dig |
|--|--|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| Plains and foothills | 32³ cubes x 20 layers | 720 | 67 | 6.8 MB | 670.9 | 12.8 | 64.3 | 8.1 | 368,250 | 12,351 | 0 | 2.33 | 6.32 | 1.4 |
| Plains and foothills | 32 x 640 x 32 columns | 36 | 36 | 47.9 MB | 296.4 | 45.7 | 492.5 | 64.0 | 368,250 | 13,362 | 0 | 14.80 | 44.55 | 1.3 |
| Mountains | 32³ cubes x 20 layers | 720 | 79 | 7.6 MB | 779.3 | 23.1 | 77.0 | 9.8 | 459,882 | 13,863 | 0 | 2.73 | 7.70 | 1.6 |
| Mountains | 32 x 640 x 32 columns | 36 | 36 | 47.9 MB | 295.7 | 45.3 | 502.9 | 65.0 | 459,882 | 16,860 | 0 | 15.43 | 45.90 | 1.3 |

- **Memory: cubes store 7x less.** About 90% of the cubes are all air or
  all solid and keep no density storage (K35); a column always spans the
  surface, so it stores all 655,360 samples.
- **Generation in parallel: cubes 2-3.5x faster.** One after another the
  columns win (36 jobs against 720, so less overhead per job), but 36 jobs can't fill 20 threads the way
  720 small ones do, and the streamer runs them in parallel.
- **Meshing: cubes 6-8x faster.** Only the surface cubes are meshed; a column
  walks its whole height of air and rock.
- **Digs: cubes 6x faster.** A dig remeshes and re-bakes about 1.4 cubes of
  32³ (2-3 ms, worst 8 ms) against a 640-high column (15 ms, worst 46 ms),
  which is most of a 60 fps frame.
- **Vertices are the same** (same surface), and neither shape comes near the
  16-bit limit on real terrain.

**Decision.** Keep cubic chunks stacked in `WorldHeightInChunks` layers (the
grid M1 already built). Columns only win on sequential generation, which the
streamer never does.

### Chunk size: 16³ vs 32³ cubes

The same benchmark at 16³ (4 m chunks, 40 layers) over the same 48 m
squares (the vertex totals match, so it is the same ground):

| Area | Shape | Chunks | With surface | Stored | Generate ms | Generate ms (parallel) | Mesh ms | Mesh ms (parallel) | Vertices | Largest mesh | Over 16-bit | Dig ms (median) | Dig ms (max) | Chunks per dig |
|--|--|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| Plains and foothills | 16³ cubes x 40 layers | 5,760 | 313 | 4.5 MB | 1533.5 | 41.3 | 51.5 | 6.4 | 368,250 | 3,312 | 0 | 1.35 | 4.53 | 2.4 |
| Plains and foothills | 16 x 640 x 16 columns | 144 | 144 | 50.9 MB | 320.3 | 39.3 | 510.0 | 56.4 | 368,250 | 3,570 | 0 | 7.73 | 15.87 | 1.8 |
| Mountains | 16³ cubes x 40 layers | 5,760 | 370 | 4.9 MB | 1660.6 | 45.3 | 61.6 | 7.6 | 459,882 | 3,501 | 0 | 1.39 | 2.90 | 2.6 |
| Mountains | 16 x 640 x 16 columns | 144 | 144 | 50.9 MB | 320.3 | 40.2 | 513.3 | 57.4 | 459,882 | 5,118 | 0 | 7.95 | 16.06 | 1.8 |

Streaming (`StreamingBenchmark`, 3 ms frame budget, same terrain):

| Chunks | Render distance | View | Chunks | With surface | Frames to settle | Time to settle ms | Streamer ms/frame (mean) | p95 | max |
|--|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| 32³ x 20 | 6 | 48 m | 2,260 | 162 | 63 | 1021 | 2.11 | 5.28 | 10.52 |
| 16³ x 40 | 6 | 24 m | 4,520 | 196 | 122 | 1988 | 1.70 | 5.57 | 7.52 |
| 16³ x 40 | 12 | 48 m | 17,640 | 684 | 488 | 8013 | 2.94 | 7.20 | 26.12 |

Against 32³, 16³ chunks store a third less (more of them are uniform), mesh a
little faster, and halve a dig (1.4 ms). They cost 3x the parallel generation
time (8x the chunks, each with its own job and bookkeeping), 5x the meshes
(renderers and draw calls), and render distance counts chunks, so the same
setting sees half as far; at the same 48 m view the world takes 8 s to settle
instead of 1 s. WorldLab's fixed 100 m square is 25,000 chunks (2,280 with a
surface) and loads in about 9 s of frames.

**Decision.** Keep cubic chunks stacked in `WorldHeightInChunks` layers (the
grid M1 already built); columns only win on sequential generation, which the
streamer never does. **16³ is the plan of record** (2026-10-08): every chunk
mesh then fits a 16-bit index buffer for any terrain and materials (at most
61,440 vertices; 49,152 for the checkerboard), so it draws on every GPU, and digs are cheapest. The world labs use it
(`ProceduralWorldLabWorldConfig`, `WorldLabWorldConfig`: 4 m chunks, 40
layers), and the voxels-per-metre picker keeps world chunks at 16 per side.
The cost is per-chunk overhead: streaming the same distance is slower, which
P16 (render distance in metres) and P7 (LOD for distant chunks) address. `ChunkIndexBudget` (Core) holds the bound;
`ChunkIndexBudgetTests` meshes the 16³ checkerboard in every shading, material
mode and mesher and requires 16-bit indices; the panel says when a chunk size
can pass the limit, and `WorldView` warns when that size runs on a GPU without
32-bit indices. Labs can still try bigger chunks where 32-bit indices exist.
M17 is done.
