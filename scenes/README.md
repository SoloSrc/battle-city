# scenes — reusable scenes (owner: claude-fable)

Leaf scenes instanced by levels (architecture.md §4). Levels set exported properties and never edit children.

- `characters/` Character.tscn, Player.tscn (Character + PlayerController), DuelDisk.tscn
- `world/` CameraRig.tscn (systems.md §4.2, follows the `player` group), CameraBounds.tscn (one per area, `Size` in the inspector, `Interior` override); markers, gates, doors and interactables to follow
- `duel/` DuelStaging.tscn, CardView.tscn, DuelUi.tscn
- `ui/` menus and overlays
- `Boot.tscn` placeholder main scene for the skeleton
