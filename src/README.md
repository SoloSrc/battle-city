# src — C# game code (owner: claude-fable)

One namespace per folder under `BattleCity.*` (architecture.md §2–§3). `Duel/` and `Data/` are separate Godot-free projects referenced by the game project.

| Folder | Namespace | Contents |
| --- | --- | --- |
| Core | BattleCity.Core | Game, SaveSystem, Audio, Input wrapper, transitions, Paths |
| Characters | BattleCity.Characters | Character, appearance, controllers, animation glue |
| World | BattleCity.World | interactables, markers, camera rig, encounters, gates |
| Duel | BattleCity.Duel.Core / .Ai | rules engine and AI, no `using Godot;` |
| DuelScene | BattleCity.DuelScene | staging, card views, HUD, VFX hooks |
| Rendering | BattleCity.Rendering | shared materials: `ToonMaterials` (toon_* replacement at load), `HologramCards` (card quads, instance parameters), `SunShadows` (directional shadow setup applied to every level's sun at load) |
| Ui | BattleCity.Ui | menus, creator, shop, deck editor, dialogue |
| Data | BattleCity.Data | loaders and records for `data/` JSON (decks, duelists, shop, avatar; `GameData.Load`), no `using Godot;` |
