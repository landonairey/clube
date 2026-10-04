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
