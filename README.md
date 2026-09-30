# clube

A destructible voxel-terrain demo in Unity, built on Marching Cubes. The
longer-term direction is a medieval economic game about ore extraction,
refining, crafting and trade.

The full plan, architecture rules and objective IDs are in
[Docs/clube-objectives.md](Docs/clube-objectives.md).

## Requirements

- Unity **6.3 LTS** (6000.3.x) with Windows Build Support (IL2CPP)
- Universal Render Pipeline (already configured in the project)
- Visual Studio (or any C# IDE Unity supports)

## Running

1. Clone the repo and open the folder in Unity Hub (**Add → Add project from disk**).
2. Open `Assets/Scenes/VoxelLab.unity`.
3. Press **Play**.

### VoxelLab controls

| Input | Action |
|---|---|
| Hold right mouse | Look around |
| W / A / S / D | Move |
| Q / E | Move down / up |
| Shift | Move faster |
| Scroll wheel | Change base speed |

Select the **Voxel** object and change its corner values and iso level in
the Inspector during Play mode to see the mesh update.

## Project layout

```
Assets/
  Scenes/          VoxelLab (later ChunkLab, WorldLab, Game)
  Scripts/
    Core/          Clube.Core   - engine code: data, meshing, generation
    Debug/         Clube.Debug  - lab components, gizmos, debug camera
    Game/          Clube.Game   - player, UI, settings
Docs/              plan, benchmarks, decision records
```

`Clube.Debug` and `Clube.Game` reference `Clube.Core`. `Clube.Core` never
references either of them.

## Contributing workflow

- Each objective in the plan is a GitHub issue, referenced by ID (e.g. `C0.2`).
- One sub-section of the plan (e.g. 1A) = one branch = one PR, listing the
  objective IDs it closes.
- Move, rename and delete assets inside Unity (or keep each `.meta` file
  with its asset) so GUIDs and scene references stay intact.

### Optional: Unity Smart Merge

`.gitattributes` routes scene, prefab and asset files through Unity's
YAML merge tool. To enable it locally, add this to `.git/config`
(adjust the path to your Unity install):

```ini
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = "'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
    recursive = binary
```

Without it, git uses its normal text merge for those files.
