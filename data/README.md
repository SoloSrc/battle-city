# data — game data JSON (owner: claude-fable)

UTF-8, two-space indent, snake_case keys and ids (architecture.md §5). Schemas in systems.md §5.3, §8, §9. `tools/validate_data.py` (issue #26) checks every file in CI.

- `cards/` one file per card (systems.md §5.3); the six tier 1 cards are in, loaded by `Duel.Core` `CardLoader`, which rejects effect ids that are not implemented
- `decks/`, `rig/`
- `duelists.json`, `shop.json`, `tuning.json`, `avatar.json`
