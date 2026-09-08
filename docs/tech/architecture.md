# Technical Design

**Status:** draft for director approval · **Issue:** #7 · **Author:** claude-fable
**Related:** [systems.md](../design/systems.md), [style-brief.md](../art/style-brief.md), [AGENTS.md](../../AGENTS.md)

Conventions for building the project: toolchain, folder layout, C# rules,
scene composition, the Blender-to-Godot pipeline, the level-authoring
workflow, and how to build, run and test. The systems document says what
the systems do; this document says how the repository is organised so
three collaborators can work in it without stepping on each other.

---

## 1. Toolchain

| Tool | Version | Notes |
| --- | --- | --- |
| Godot | 4.7.2 .NET (mono) | Pinned in `project.godot` and this table; upgrade by pull request |
| .NET SDK | 8.0 | `global.json` pins the major version |
| C# language | 12 | `LangVersion` in the csproj |
| Blender | 4.x | Artist's tool; export settings in §6 |
| Renderer | Forward+ | Required for the toon shader and MSAA |
| Target platforms | Windows x64, macOS universal, Linux x64 | Export presets committed |

`project.godot`, `BattleCity.csproj`, `BattleCity.sln` and `global.json` live at
the repository root so the repo *is* the Godot project.

---

## 2. Repository layout

```
/
├── AGENTS.md, README.md, LICENSE, docs/          documentation (unchanged)
├── project.godot, BattleCity.csproj, .sln, global.json
├── src/                       C# code, one namespace per folder
│   ├── Core/                  Game, SaveSystem, Audio, Input wrapper, transitions
│   ├── Characters/            Character, appearance, controllers, animation glue
│   ├── World/                 interactables, markers, camera rig, encounters, gates
│   ├── Duel/                  Duel.Core engine (no Godot), Duel.Ai
│   ├── DuelScene/             staging, card views, HUD, VFX hooks
│   ├── Ui/                    menus, creator, shop, deck editor, dialogue
│   └── Data/                  loaders and records for data/ JSON
├── scenes/                    reusable .tscn owned by claude-fable
│   ├── characters/            Character.tscn, DuelDisk.tscn
│   ├── world/                 markers, gates, doors, camera rig, interactables
│   ├── duel/                  DuelStaging.tscn, CardView.tscn, DuelUi.tscn
│   └── ui/
├── levels/                    composed level scenes owned by gpt-astra
│   └── district/              District.tscn, interiors/, and area sub-scenes
├── assets/                    delivered art and audio owned by gpt-astra
│   ├── characters/<part>/     body, hair, outfits, accessories (.glb + textures)
│   ├── props/                 duel disk and world props
│   ├── kit/                   modular building and street kit (.glb)
│   ├── cards/frames/, cards/art/, cards/icons/
│   ├── vfx/                   particle and shader scenes for effects
│   ├── audio/music/, audio/sfx/, audio/ambience/
│   └── source/                .blend, layered sources, audio sessions (Git LFS)
├── data/                      game data JSON: cards/, decks/, duelists.json, shop.json, tuning.json, avatar.json, rig/
├── shaders/                   toon.gdshader, outline.gdshader, hologram.gdshader
├── tests/
│   ├── Duel.Core.Tests/       xUnit project, runs with `dotnet test`
│   └── scenes/                in-editor test scenes (CameraFraming.tscn, SmokeTest.tscn)
└── tools/                     editor plugins and scripts (level checklist, card data validator)
```

Ownership follows AGENTS.md: `src/`, `scenes/`, `shaders/`, `data/`,
`tests/`, `tools/` are claude-fable's; `levels/` and `assets/` are
gpt-astra's. Cross-boundary changes go through `docs/requests/`.

Large binaries (`.blend`, `.psd`, `.wav` sources, textures over 1 MB) are
tracked with **Git LFS**; `.gitattributes` lists the patterns.

---

## 3. C# conventions

- One class per file, file name equals class name, namespace equals folder
  path under `src/` (`BattleCity.Duel.Core`, `BattleCity.World`).
- Godot nodes: `partial class Foo : Node3D` with `[Export]` fields in
  PascalCase, `[Signal]` delegates suffixed `EventHandler`.
- `Duel.Core` has **no** `using Godot;`. Enforced by keeping it in its own
  csproj (`src/Duel/Duel.Core.csproj`) referenced by the game project.
- Nullable enabled, warnings as errors in `Duel.Core`, `TreatWarningsAsErrors`
  off elsewhere until the skeleton stabilises.
- Records for immutable data (`CardDefinition`), classes for mutable state.
- No static mutable state outside autoloads. Autoloads are accessed through
  `Game.Instance` style properties, never by node path strings in code.
- `.editorconfig` at the root fixes formatting; `dotnet format` runs in CI.
- Scene references use `[Export] PackedScene` or `GD.Load<PackedScene>("res://...")`
  with `res://` paths written once in a `Paths` static class.

---

## 4. Scene composition rules

1. **Reusable scenes are leaves.** `Character.tscn`, `Door.tscn`,
   `EncounterSite.tscn` never contain level geometry and never reference a
   level.
2. **Levels instance, never edit.** A level scene instances reusable scenes
   and sets exported properties. If a level needs a reusable scene changed,
   that is a request to claude-fable, not an editable-children override.
3. **Markers over scripts.** The level designer places `Marker`-type scenes
   (§7) and sets ids; behaviour is discovered by group and id at runtime.
   Levels contain no scripts of their own.
4. **Kit pieces are `MeshInstance3D` with baked collision** imported from
   glTF (§6). Levels may add `StaticBody3D` blockers for camera bounds and
   invisible walls using the `world` layer.
5. **Groups** used by code: `player`, `npc`, `duelist`, `interactable`,
   `encounter_site`, `camera_bounds`, `spawn`, `gate`, `ambient_zone`.
6. Every level scene has exactly one `NavigationRegion3D`, one
   `WorldEnvironment`, one `DirectionalLight3D`, all children of the root.

---

## 5. Data conventions

- All game data is JSON under `data/`, UTF-8, two-space indent, keys in
  `snake_case`, ids in `snake_case` matching file names.
- Card ids are the card name in snake case with punctuation removed
  (`black_luster_soldier_envoy_of_the_beginning`).
- `tools/validate_data.py` checks every file against the schemas in
  `docs/design/systems.md` §5.3, §8, §9 and fails CI on unknown ids,
  bad copy limits or missing effect classes.

---

## 6. Blender to Godot pipeline

### 6.1 Units and axes

Blender scene units metric, 1 unit = 1 m, scale 1.0. Export glTF with
**+Y up** (Godot convention; the exporter converts from Blender's Z up).
Characters face **−Z** in Godot, which is **−Y** in Blender before export.
Apply all transforms before export; origin at the feet for characters and
props that stand, at the bounds-min corner for kit pieces that tile.

### 6.2 Export settings (glTF 2.0, `.glb`)

| Setting | Value |
| --- | --- |
| Format | glTF Binary (.glb) |
| Include | Selected objects, custom properties |
| Transform | +Y up |
| Mesh | Apply modifiers, UVs, normals, tangents, vertex colours if used |
| Materials | Export, images packed |
| Animation | Export, NLA tracks as separate actions, sampling 30 fps, always sample |
| Skinning | Include all bone influences off; limit 4 |
| Compression | None (Godot's import compresses) |

One `.glb` per character part, per prop, per kit piece. Animations for
the shared rig ship in one `character_anims.glb` containing the rig and
all clips, imported once and applied through the `AnimationLibrary`.

### 6.3 Naming

| Object type | Blender object name | Result in Godot |
| --- | --- | --- |
| Collision mesh | `<name>-col` suffix | Trimesh `StaticBody3D`, mesh hidden |
| Convex collision | `<name>-convcol` | Convex shape |
| No-import helper | `<name>-noimp` | Skipped |
| Navmesh hint | `<name>-navmesh` | NavigationMesh source |
| Kit piece | `kit_<category>_<piece>` (`kit_wall_2m_window`) | Same name as scene |
| Character part | `char_<body>_<slot>_<variant>` (`char_a_hair_02`) | Same |
| Prop | `prop_<name>` | Same |
| Animation action | `anim_<name>` (`anim_walk`) | Clip `walk` |

Suffixes follow Godot's importer conventions so no import script is needed
for the common cases.

### 6.4 Materials and textures

- One material per part where possible; texture set `<name>_albedo.png`,
  `_normal.png` optional, `_orm.png` optional. Power-of-two sizes, PNG.
- Godot import replaces glTF materials with the shared `toon.gdshader`
  material through an import script (`tools/import/apply_toon.gd`) keyed on
  the material name prefix `toon_`. Materials named otherwise import as
  standard PBR for review.
- Skin tone and accent colour are shader parameters (`skin_tint`,
  `accent_tint`) set by `CharacterAppearance`, so no per-colour textures.

### 6.5 Rig

Skeleton bone names follow Godot's `SkeletonProfileHumanoid`. After the
first import, the bone map is saved in the import settings so retargeting
is automatic. Rest pose A-pose. The `LeftLowerArm` bone must exist with
that exact name for the duel disk mount.

### 6.6 Import presets

`assets/.import_presets/` holds the import settings used for characters,
kit and props; the first import of a new file copies the preset. Textures
import with mipmaps on and `sRGB` for albedo, `linear` for normal and ORM.

### 6.7 Audio

Music and ambience `.ogg` (Vorbis, quality 6) with loop points in the import
settings; SFX `.wav` 48 kHz 24-bit mono. Buses: Master → Music, SFX, UI,
Ambience, defined in `default_bus_layout.tres`.

---

## 7. Level-authoring workflow

The level designer composes levels in Godot from kit pieces and marker
scenes provided by claude-fable. This is the whole workflow; nothing else
is required to make a level work.

### 7.1 Components provided

| Scene (`scenes/world/`) | Exports | Purpose |
| --- | --- | --- |
| `PlayerSpawn.tscn` | `id` | Where the player appears; `arrival` is the New Game spawn |
| `EncounterSite.tscn` | `id`, `duelist_id`, `axis` (Vector3), `stand_distance` (7), clearance box | Duel staging site; draws its footprint and stand points in the editor |
| `Duelist.tscn` (instances Character) | `duelist_id`, `cone_range`, `cone_angle`, `facing` | The opponent NPC; cone drawn in editor |
| `TalkNpc.tscn` | `dialogue_id`, `appearance` | Talking NPC |
| `Sign.tscn` | `text` | Sign |
| `Door.tscn` | `target_scene`, `target_spawn`, `return_spawn` | Interior transitions |
| `ShopCounter.tscn` | `shop_id` | Opens shop UI |
| `CameraBounds.tscn` | box size | Camera clamp volume for an area |
| `AmbientZone.tscn` | `music_id`, `ambience_id` | Audio per area |
| `ProgressionGate.tscn` | `required_flag`, blocker mesh slot | Opens when the flag is set |
| `SurfaceTag` (script on StaticBody3D) | `surface` enum | Footstep sounds |
| `LevelBounds.tscn` | box | Keeps the player in the built area |
| `KitSnap` (editor plugin) | grid 1 m, rotation 90°, detail grid 0.25 m | Snapping for kit placement |

### 7.2 Steps

1. Branch from `main` in the `gpt-astra` worktree.
2. Open `levels/district/District.tscn` (created by claude-fable with the
   root, environment, light and navigation region already in place).
3. Place kit pieces with `KitSnap` on; kerbs, walls and roofs snap to the
   1 m grid, props to 0.25 m.
4. Place one `PlayerSpawn` per arrival point, `CameraBounds` per area,
   `AmbientZone` per area, `EncounterSite` per duel, `Duelist` near each
   site, `ProgressionGate` at each gate, `Door` for each interior.
5. Bake navigation (toolbar button on the `NavigationRegion3D`).
6. Run the level checklist (§7.3) from the Project → Tools menu.
7. Press Play with `tests/scenes/SmokeTest.tscn` set as the run scene; it
   loads the district with a placeholder avatar and prints the checklist
   result to the output.
8. Open a pull request. Screenshots of each encounter site from the duel
   camera are attached (the `EncounterSite` inspector has a "Capture duel
   camera" button).

### 7.3 Level checklist (`tools/level_check.gd`)

Fails the pull request if any item fails:

- Exactly one `PlayerSpawn` with id `arrival` and one per `Door.return_spawn`.
- Every `EncounterSite` has a `Duelist` within 10 m with a matching id, both
  stand points are on the navmesh, and the clearance box contains no `world`
  collision.
- Every `Duelist.duelist_id` exists in `data/duelists.json`.
- Every area of walkable navmesh is inside at least one `CameraBounds`.
- No kit piece has an unapplied scale; all names follow §6.3.
- All `Door.target_scene` paths exist and the targets have the named spawn.
- `ProgressionGate.required_flag` values are known flags.
- Triangle count and draw calls under the level budget (§9).

### 7.4 Interiors

Interiors are separate scenes under `levels/district/interiors/`, same
rules, with a `CameraBounds` matching the room and one `PlayerSpawn` per
door.

---

## 8. Build, run and test

```bash
# restore and build the C# solution (also validates data)
dotnet build

# unit tests for the duel engine
dotnet test tests/Duel.Core.Tests

# run the game from the command line
godot --path . 

# run a specific scene (e.g. the framing comparison)
godot --path . tests/scenes/CameraFraming.tscn

# export (presets committed in export_presets.cfg)
godot --path . --headless --export-release "macOS" build/BattleCity.app
```

`godot` refers to the Godot .NET binary; on this machine it is
`/Applications/Godot.app/Contents/MacOS/Godot`.

### 8.1 Continuous integration

GitHub Actions on every pull request: `dotnet format --verify-no-changes`,
`dotnet build`, `dotnet test`, `python tools/validate_data.py`, and a
headless Godot import check (`godot --headless --import`) to catch broken
scenes and missing dependencies. Level checklist runs headless on
`levels/**` changes.

---

## 9. Budgets

| Budget | Value |
| --- | --- |
| Frame time target | 16.6 ms at 1080p on integrated graphics (Apple M1 class) |
| Dressed character | ≤ 12 k triangles, ≤ 4 materials |
| Duel disk | ≤ 1.5 k triangles, 1 material |
| District visible set | ≤ 400 k triangles, ≤ 600 draw calls from any camera position |
| Textures | ≤ 2048² for kit atlases, ≤ 1024² per character part |
| Card frames | 590 × 860 PNG; art 512² |
| Music | ≤ 4 MB per track after Vorbis |

`tests/scenes/Profiling.tscn` prints these from the editor and is run
before dense set dressing, per the district proposal.

---

## 10. Branching and reviews

As in AGENTS.md: collaborator branches, pull requests to `main`, director
merges. Additional rules for the codebase:

- A pull request touching `src/Duel/` must keep `dotnet test` green and add
  a scenario test for each new card.
- A pull request touching `levels/` must pass the level checklist and
  attach the site screenshots.
- A pull request touching `assets/` must list each asset's source file
  under `assets/source/` and its licence in the pull request body.
- Merge method: **squash** for feature branches so `main` has one commit per
  pull request. Collaborator branches then merge `main` back rather than
  rebasing, to avoid force pushes.

---

## 11. First engineering milestone: the skeleton

Before any level or asset production, claude-fable delivers a runnable
project on `main` containing: the folder layout, csproj and CI, the
`Character` scene with a placeholder capsule and rig, `PlayerController`,
`CameraRig`, all §7.1 marker scenes, `District.tscn` with a 1 m cube grid
greybox, the smoke test scene, `CameraFraming.tscn` at 60, 100 and 120 px,
and a `Duel.Core` project that plays a vanilla-only duel in tests. This is
the "runnable Godot project" the district proposal lists as its blocking
dependency.
