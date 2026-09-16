# tests/scenes (owner: claude-fable)

In-editor diagnostic scenes. They are not shipped.

## SmokeTest.tscn (issue #24)

Validates the pipeline smoke-test deliverables from
[asset-list.md §0](../../docs/art/asset-list.md) against the contract in
[architecture.md §6](../../docs/tech/architecture.md). Run it from the
editor (open the scene, F6) or headless:

```bash
godot --headless --path . res://tests/scenes/SmokeTest.tscn --quit-after 60
```

The scene loads whatever exists at these paths and skips the rest, so it
runs on a fresh clone. Drop the files in and run again; no scene or code
change is needed.

| Deliverable | Path | Checks |
| --- | --- | --- |
| Metric cube | `assets/kit/kit_test_cube_1m.glb` | 1 × 1 × 1 m, origin at the bounds-min corner, `-col` collision present, `toon_` material |
| Neutral character | `assets/characters/body/char_a_body.glb` | `Skeleton3D` with `SkeletonProfileHumanoid` bone names, `LeftLowerArm` present, 1.7 m ± 0.1 tall, feet at y = 0, ≤ 12 k triangles |
| Character clips | `assets/characters/anims/character_anims.glb` | `idle` (4 s) and `walk` (1 s) clips, events injected from `data/rig/animation_events.json`; played through the `AnimationTree` state machine |
| Duel disk | `assets/props/prop_duel_disk.glb` | ≤ 1.5 k triangles; shown at its spot and mounted on `LeftLowerArm` with the offset from `data/rig/disk_mount.json` |

The scene also loads the shared materials of issue #27: it checks the toon
and hologram shaders compile and expose the uniforms code sets, converts
every `toon_*` surface with `ToonMaterials.Apply` (a `FAIL` if any is left),
and floats three sample cards at `CardSpot` (player attack, selected;
opponent defence; face-down showing the back).

Output goes to the console and the on-screen report. `SmokeTest FAIL`
lines fail CI; `WARN` lines are review notes; `SKIP` means the file is not
there yet. Press `interact` to toggle idle / walk and `menu` to switch
between the gameplay camera (55–60° pitch, 13 m, 35° FOV) and a close
inspect camera; `-- --inspect` starts on the inspect camera. Animation
events print as `SmokeTest event: <name>`.

### glTF import contract (Blender export)

- glTF Binary (`.glb`), **+Y up**, 1 unit = 1 m, scale 1.0, all transforms
  applied. Characters face −Z in Godot, which is **+Y in Blender** before
  export.
- Origin at the feet for characters and standing props; at the bounds-min
  corner for kit pieces that tile on the 1 m grid.
- Suffixes: `<name>-col` adds trimesh collision and keeps the mesh visible
  (one suffixed object is enough), `-convcol` convex, `-noimp` skipped,
  `-navmesh` navigation source.
- Animation: NLA tracks as separate actions, 30 fps, always sample, no
  root motion. Action `anim_walk` imports as clip `walk` (the scene strips
  the `anim_` prefix). glTF cannot carry call-method tracks: report event
  timings in the handoff and the engine injects them from
  `data/rig/animation_events.json` (systems.md §3.2).
- Materials named `toon_*` receive the shared toon material
  (`shaders/materials/toon.tres`, applied at load by `ToonMaterials`, see
  `shaders/README.md`). Anything else imports as PBR for review.
- Rig: `SkeletonProfileHumanoid` bone names, A-pose, `LeftLowerArm` must
  exist. Godot generates a `.import` file next to each `.glb` on first
  import; commit it, it is the per-asset import preset.

## CharacterTest.tscn (issue #19)

Instances `scenes/characters/Character.tscn` on a ground plane and drives it
idle → walk → run → duel ready on a frame schedule, moving the body at the
matching speed. Checks the skeleton animates, data-injected footstep events
fire, the disk mounts and the state machine reaches the duel states.
`CharacterTest FAIL` lines fail CI.

```bash
godot --headless --path . res://tests/scenes/CharacterTest.tscn --fixed-fps 60 --quit-after 300
```

## PlayerTest.tscn (issue #20)

A test course for `scenes/characters/Player.tscn`: a 0.15 m kerb, a 0.30 m
step, a 30° ramp onto a platform, and a test interactable. Headless, the
scene feeds scripted input (walk, then run, then interact) and checks
distance, climb, peak speed, airborne frames, single-frame drops (jitter)
and the interaction. In the editor it is a free-roam course for gamepad
and keyboard. `PlayerTest FAIL` lines fail CI.

```bash
godot --headless --path . res://tests/scenes/PlayerTest.tscn --fixed-fps 60 --quit-after 400
```

## Planned

- `Profiling.tscn` (later)

## CameraFraming.tscn (issue #21)

Readability diagnostic for the overworld camera (systems.md §4.2). The player,
a duelist at the 7 m stand distance, a bystander and 1 m kit cubes sit on the
reference grid under `scenes/world/CameraRig.tscn`. Two `CameraBounds`
volumes, a plaza and an interior with the interior override, sit side by side
so clamping and the profile blend can be seen. The cubes take the toon
material and the player and duelist each get the ten hologram card anchors of
systems.md §6.1 (2 × 5, 1 m forward, 0.22 × 0.28 m), so the shaders can be
judged from the overworld camera.

```bash
godot --headless --path . res://tests/scenes/CameraFraming.tscn --fixed-fps 60 --quit-after 700
```

The HUD shows the live pitch, distance, FOV, yaw, rig position, active
volume, clamp state and the avatar's pixel height normalised to 1080p. That
height is an outcome to look at, not a target (director decision
2026-09-08); the camera values change only by director decision. Live
controls: move as in the game, `menu` cycles framing presets (GDD baseline
57° / 12 m, GDD min 55° / 12 m, GDD max 60° / 14 m, far 57° / 14 m), `cancel`
shows the bounds volumes. Scripted mode (headless or `-- --scripted`) runs
the player east through the plaza/interior seam, past the east edge and
around the corner, and checks the snap at start, follow lag, the interior
takeover and blend, the edge clamp with the 1 m margin and the corner hold.
`CameraFraming FAIL` lines fail CI.

## MarkersTest.tscn and MarkersBad.tscn (issue #22)

`MarkersTest.tscn` is a small level built only from the `scenes/world`
components: PlayerSpawn, CameraBounds, LevelBounds, AmbientZone, Sign, Door,
ShopCounter, EncounterSite, Duelist, TalkNpc, ProgressionGate and a
SurfaceTag ground under a NavigationRegion3D that bakes at load. Scripted
mode walks the player into the zone, reads the sign through the interaction
probe, gets spotted by the duelist's cone and challenges it, opens the gate
with `defeated:d1`, fires the Door / ShopCounter / TalkNpc contracts and runs
off the built area to be recovered by LevelBounds. `MarkersTest FAIL` lines
fail CI.

```bash
godot --headless --path . res://tests/scenes/MarkersTest.tscn --fixed-fps 60 --quit-after 520
godot --headless --path . -s tools/level_check.gd -- res://tests/scenes/MarkersTest.tscn
```

`MarkersBad.tscn` seeds the faults the checklist must catch (two `arrival`
spawns, overlapping reservations, a site without its duelist, a wall inside a
clearance box, an unapplied scale, a door to a missing scene, an unknown gate
flag, no CameraBounds). CI expects the checklist to exit 1 on it.

## DuelStagingTest.tscn (issue #60)

The acceptance of `DuelStaging` and `CardView` (systems.md §6.1–§6.2): two
`Character.tscn` instances on an `EncounterSite`, the rig blended to the duel
framing, and a real `DuelEngine` (the starter against Rookie Beatdown, the
Mara and Nico profiles from `data/duelists.json`) driven one agent command
every 15 frames while the card views follow the engine. After every command
the scene checks that each card view sits on the anchor and in the
orientation its `CardInstance` says; at the start it checks the anchor grid
distances, the stand points and the §6.2 layout contract as numbers (level 1
centered, level 12 from x=26 to 467 with 24 px to the attribute, the six
frames, the icons, a level 12 and a Ritual fixture composing). At the end it
checks the command, move and event counts, the camera framing and the duel
states. `DuelStagingTest FAIL` lines fail CI.

```bash
godot --headless --path . res://tests/scenes/DuelStagingTest.tscn --fixed-fps 60 --quit-after 2700
```

Headless runs keep the face viewports (nothing is drawn to read back). In
the editor, or with a window, `-- --capture <dir>` saves `staging.png` and the
baked faces of the cards in play a few commands in, for visual review of the
six frame types, stars, attribute, stats and badges.

## DistrictTest.tscn (issues #23, #62)

The acceptance path of the district through the real `Game` autoload: New
Game → starting room → exit door (fade) → Plaza `arrival` → Nico's cone
starts the tutorial encounter (input locks, the camera reveals Nico with the
exclamation and returns, both walk to the `nico` site's stand points,
challenge line) → the real duel (starter deck against Rookie
Beatdown, staging, HUD with the tutorial hints, duel camera). Scripted mode
surrenders the first duel (lose line, HUD and cards torn down, return to the
encounter spot, no coin loss, `tutorial_done` set, 3 s cone disarm with no
retrigger on the standing player), challenges Nico by interacting and plays
the rematch with a heuristic agent in the player's seat to a win (`defeated:d1`,
600 coins, one Street Pack of five library cards in the collection, win and
reward lines, Park gate opens, Mara unlocks, no hints, the cone stays off
afterwards), then walks back into the room through its door. Phases advance
on conditions with a timeout each. The game seed is fixed (`Seed`) so the
rematch is a known win. Live, it is simply the game from the starting room
with the report label on top. `DistrictTest FAIL` lines fail CI.

```bash
godot --headless --path . res://tests/scenes/DistrictTest.tscn --fixed-fps 60 --quit-after 9000
```

## DuelUiTest.tscn (issue #61)

The staging scene plus `DuelSession` and `DuelUi`: Goat Control in the
human seat against Mara's deck. A scripted player asks a heuristic agent what
it would do and then reaches that command only through the HUD, by pressing
the input actions (grid moves, `interact`, `cancel`, `duel_phase`) and, every
other time, by moving the mouse over the 3D card or the fan card and
clicking. The command the engine receives must be the one aimed for; menus
must offer only legal commands; the chain display, hand fan and Life Point
counters must match the state; the mouse must move the cursor; the pile
lists and the log must open. `DuelUiTest FAIL` lines fail CI.

```bash
godot --headless --path . res://tests/scenes/DuelUiTest.tscn --fixed-fps 60 --quit-after 5600
```

Windowed, `-- --capture <dir>` saves `ui_<mode>.png` the first time each
kind of prompt is open (free cursor, menu, targets, picker, response, pile).
Injected mouse events carry window pixels (headless windows are 64 × 64 and
stretched), so the scene maps viewport positions through the viewport's
final transform before injecting them.
