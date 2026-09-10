# tools (owner: claude-fable)

- `level_check.gd`: the level checklist (architecture.md §7.3) from the command line:

  ```bash
  godot --headless --path . -s tools/level_check.gd -- res://levels/district/District.tscn
  ```

  Prints `level_check PASS/FAIL/WARN/INFO` lines and a summary; exits 1 on any
  FAIL. The same checks run from the editor menu **Project → Tools → Battle
  City: Run level checklist** on the scene being edited (they live in
  `addons/battle_city/level_checker.gd`). CI runs it on
  `tests/scenes/MarkersTest.tscn` (must pass) and `MarkersBad.tscn` (must fail).
- `addons/battle_city/` (editor plugin, enabled in `project.godot`): the
  checklist menu item, **KitSnap** (Project → Tools toggle; selected `kit_*`
  nodes snap to 1 m and 90°, `prop_*` to 0.25 m while you move them) and the
  gizmos for EncounterSite (footprint, axis, stand points), Duelist (cone),
  PlayerSpawn and Door (facing arrows).
- `validate_data.py`: card and deck data validator (issue #26).
