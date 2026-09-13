# tests (owner: claude-fable)

- `Duel.Core.Tests/` xUnit, runs with `dotnet test tests/Duel.Core.Tests`: `TurnFlowTests` and `BattleTests` cover the systems.md §5.5 rules one by one, `AiTests` play the real tier 1 cards to a winner with the heuristic agents (acceptance for #25), `FuzzTests` run random agents from 20 fixed seeds and assert the §5.7 invariants after every command, `CardLoaderTests` load `data/cards/` and reject bad documents. `Fixtures.cs` builds unshuffled decks whose top cards are scripted and places monsters straight on the field.
- `scenes/` in-editor diagnostic scenes: SmokeTest.tscn (#24), CharacterTest.tscn (#19), PlayerTest.tscn (#20), CameraFraming.tscn (#21), Profiling.tscn; see `scenes/README.md`
