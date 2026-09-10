# scenes — reusable scenes (owner: claude-fable)

Leaf scenes instanced by levels (architecture.md §4). Levels set exported properties and never edit children.

- `characters/` Character.tscn, Player.tscn (Character + PlayerController), DuelDisk.tscn
- `world/` level components (architecture.md §7.1): CameraRig.tscn, CameraBounds.tscn, PlayerSpawn.tscn, EncounterSite.tscn, Duelist.tscn and TalkNpc.tscn (Character + marker child), Sign.tscn, Door.tscn, ShopCounter.tscn, AmbientZone.tscn, ProgressionGate.tscn, LevelBounds.tscn; `SurfaceTag` is a script for StaticBody3D. Interactables raise signals; gameplay wiring lands with issue #23.
- `duel/` DuelStaging.tscn, CardView.tscn, DuelUi.tscn
- `ui/` menus and overlays
- `Boot.tscn` placeholder main scene for the skeleton
