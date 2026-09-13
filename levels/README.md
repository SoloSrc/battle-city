# levels — composed level scenes (owner: gpt-astra)

Levels instance reusable scenes and marker components; no level scripts.
Run `tools/level_check.gd` before opening a pull request.

## Playable district

`district/District.tscn` composes the five approved area scenes under one
NavigationRegion3D. The Game autoload owns the player, CameraRig, transitions
and encounters. Run Boot for the complete placeholder flow; the runtime level
has no extra player or camera instance.

| Area scene | Bounds X / Z (m) | Content |
| --- | --- | --- |
| Plaza | 40–76 / 66–104 | Arrival (58,98), Nico (58,91), `nico` site (58,88), fountain and room door |
| Market | 8–40 / 48–104 | Card shop, `shop_door` spawn (22,76), two talk NPCs |
| Park | 76–112 / 40–104 | Mara (94,71), `mara` site (94,68), river and planting |
| Arcade | 40–100 / 8–40 | Arcade Owner (74,27), `arcade_owner` site (70,26), arcade façade |
| Edge | Perimeter / construction street | Continuous boundaries, two gates, construction NPC; camera/audio volume at X30–40 / Z48–64 |

X increases east, Z south, ground Y=0. Area roots remain at identity.
Each area carries CameraBounds and AmbientZone metadata. Duel clearances are
16×12 m with 7 m east-west stand spacing. The Arcade Owner faces southeast
toward the unlocked approach. Park and Arcade gates use `defeated:d1` and
`defeated:d2`; the corresponding duelists use the same RequiredFlag values.

`interiors/StartRoom.tscn` (8×6 m) and `interiors/ShopInterior.tscn` (10×8 m)
are the canonical interiors. New Game uses room `arrival`; exterior doors target
interior `door` spawns. Exits return to district `arrival` / `shop_door`.
Interior roofs are omitted and door thresholds stay low for camera visibility.

Navigation is committed baked under `district/navigation/`. The artist bake
excludes gate blockers and inaccessible elevated surfaces. Game/checklist
already retain a populated navigation mesh. After editing geometry, rebake with
`assets/source/district/bake_navigation.gd`; a generic fallback bake does not
reproduce those exclusions. `build_composition.py` regenerates scenes and resets
navigation, so update it alongside scene edits and rebake afterwards.

`review/DistrictComposition.tscn` uses the same areas with its own inspection
player/camera. It is for isolated artist inspection, not the Game entry point.
The former duplicate `StartingRoom.tscn` / `CardShop.tscn` review interiors are
retired; all consumers use the canonical paths above.

See [runtime integration](../docs/requests/district-runtime-integration.md) for
results, captures, remaining acceptance and the handoff to Fable.
