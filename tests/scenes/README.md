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

Output goes to the console and the on-screen report. `SmokeTest FAIL`
lines fail CI; `WARN` lines are review notes; `SKIP` means the file is not
there yet. Press `interact` to toggle idle / walk and `menu` to switch
between the gameplay camera (55–60° pitch, 13 m, 35° FOV) and a close
inspect camera. Animation events print as `SmokeTest event: <name>`.

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
- Materials named `toon_*` receive the shared toon material (placeholder
  `StandardMaterial3D` until issue #27; `tools/import/apply_toon.gd` will do
  the same at import time). Anything else imports as PBR for review.
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

- `CameraFraming.tscn` (issue #21)
- `Profiling.tscn` (later)

## CameraFraming.tscn (issue #21)

Readability diagnostic for the overworld camera (systems.md §4.2). The player,
a duelist at the 7 m stand distance, a bystander and 1 m kit cubes sit on the
reference grid under `scenes/world/CameraRig.tscn`. Two `CameraBounds`
volumes, a plaza and an interior with the interior override, sit side by side
so clamping and the profile blend can be seen.

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
