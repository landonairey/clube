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
