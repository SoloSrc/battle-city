# data — game data JSON (owner: claude-fable)

UTF-8, two-space indent, snake_case keys and ids (architecture.md §5). Schemas
in systems.md §5.3 (cards) and §8 (decks, duelists, shop, avatar).
`tools/validate_data.py` checks every file in CI; the C# loaders apply the
same rules at runtime (`Duel.Core` `CardLoader`, `BattleCity.Data` for the rest,
`GameData.Load("data")` for everything at once).

| Path | Contents |
| --- | --- |
| `cards/<id>.json` | The 79-card slice subset (GDD §4.5: the 72-card table plus the seven rookie vanillas). Tier 1 cards are playable; tier 2+ cards name their effect id (`effects: ["<card_id>"]`) as a stub until the effect class exists, and the engine refuses a deck that contains one |
| `decks/<id>.json` | `beatdown`, `warrior_toolbox`, `goat_control` (GDD §4.2–§4.4), `rookie_beatdown` (Nico's tutorial list) and `starter` (the Beatdown list). Card id → copy count, `main` and `fusion` |
| `duelists.json` | `d1`–`d3`: area, deck, `required_flag`, AI profile weights (systems.md §7), dialogue lines, rewards (GDD §3.6) |
| `shop.json` | Stock (every non-Limited card, priced by tier: 100/200/400/600) and the Street Pack booster (GDD §5.2) |
| `avatar.json` | Creator option lists and defaults (GDD §1.1) |
| `rig/` | Disk mount offsets and animation event timings (systems.md §3.2) |
| `tuning.json` | The systems.md §10 numbers: `player`, `camera.overworld`, `camera.duel`, `encounter`, `duel`, `anchors`. Single source; the nodes read them through `Tuning.Current` and no longer export them |

Card `text` is a short rules summary written for this project, not the printed
card text; no card art is stored (pitch, IP section).
