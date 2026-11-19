Let's park these future improvement notes in the main objectives file and close out the work here today
(said at the end of batch X18, GL778; the notes are what that batch left, from [PR 196](https://github.com/landonairey/clube/pull/196))
- Walking still has about 3 garbage collections a 15 s stretch (goal: 15 or fewer over the 150 s walk, now 46). The load stretches hold 23 of the 46 while the managed heap grows 350 -> 1100 MB (chunk data).
- Chunk data outside the managed heap: the only thing that fixes the load stretches.
- Allocators left while walking (KB a frame): chunk adds 13, animals 4-7, grass 4-9, carts 1-6, distant trees 3; the water and carve arrays in `Chunk` are not pooled; `World.Add` makes a closure per chunk (`BecameDirty`).
- Never call `Mathf.Max` / `Mathf.Min` with three or more arguments in a per-frame loop (allocates a `params` array).
