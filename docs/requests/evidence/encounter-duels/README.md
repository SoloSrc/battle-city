# Evidence: encounters run real duels (issue #62)

Captures from `tests/scenes/DistrictTest.tscn` run windowed with
`-- --scripted --capture <dir>` (scaled to 1280 px). The scripted path is the
CI acceptance: 54 checks.

| File | Moment |
| --- | --- |
| `district_reveal.png` | Nico spotted the player on Plaza arrival: input locked, the camera holds on Nico with the exclamation before the walk |
| `district_challenge.png` | Both on the `nico` site's stand points, challenge line from `data/duelists.json` |
| `district_duel.png` | The tutorial duel: starter deck vs Rookie Beatdown in the Plaza, duel camera, hand fan, inspector, first tutorial hint under the banner |
| `district_rematch.png` | The rematch mid-duel (heuristic agent in the player's seat), cards on both fields, Life Points moving |
| `district_reward.png` | After the win: cards dissolving, camera back, the reward line (600 coins and one Street Pack listed by card name) |
