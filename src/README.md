# src — C# game code (owner: claude-fable)

One namespace per folder under `BattleCity.*` (architecture.md §2–§3). `Duel/` is a separate Godot-free project.

| Folder | Namespace | Contents |
| --- | --- | --- |
| Core | BattleCity.Core | Game, SaveSystem, Audio, Input wrapper, transitions, Paths |
| Characters | BattleCity.Characters | Character, appearance, controllers, animation glue |
| World | BattleCity.World | interactables, markers, camera rig, encounters, gates |
| Duel | BattleCity.Duel.Core / .Ai | rules engine and AI, no `using Godot;` |
| DuelScene | BattleCity.DuelScene | staging, card views, HUD, VFX hooks |
| Ui | BattleCity.Ui | menus, creator, shop, deck editor, dialogue |
| Data | BattleCity.Data | loaders and records for `data/` JSON |
