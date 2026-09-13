# Duel.Core (owner: claude-fable)

Pure C# rules engine (systems.md §5). No Godot dependency; warnings are
errors. Tested by `tests/Duel.Core.Tests`. Tier 1 landed with issue #25:
vanilla monsters and Pot of Greed; tiers 2–4 add effects on top of the same
command, event and chain structure.

## Layout (namespace = folder under `BattleCity.Duel.Core`)

| Folder | Contents |
| --- | --- |
| `Model/` | `DuelState`, `PlayerState`, `CardInstance`, `CardDefinition`, `ChainLink`, enums (`Phase`, `BattleStep`, `DamageSubstep`, `Position`, `Location`, …) |
| `Rules/` | `TurnFlow` (phases, priority), `SummonRules`, `BattleRules`, `DamageStep`, `ChainResolver`, `ActionEnumerator`, `Zones` (card movement) |
| `Effects/` | `IEffect`, `EffectRegistry`, `Choice`, `PotOfGreedEffect` |
| `Commands/` | `PlayerCommand` records: `Pass`, `NormalSummon`, `SetMonster`, `ChangePosition`, `FlipSummon`, `ActivateSpell`, `SetSpellTrap`, `EnterBattlePhase`, `DeclareAttack`, `Discard`, `Surrender` |
| `Events/` | `DuelEvent` records consumed by presentation (draws, phases, summons, attacks, damage, destruction, chain links, duel end) |
| `Ai/` | `IDuelAgent`, `HeuristicAgent` + `AiProfile` (Nico, Mara, Arcade Owner presets), `RandomAgent`, `DuelRunner` |
| `Rng/` | `DuelRng`, seeded xoshiro256**; every shuffle, coin flip and AI jitter goes through it |
| `Data/` | `CardLoader` for `data/cards/<id>.json`, `CardLibrary`, `CardDataException` |

`DuelEngine.Start(deck0, deck1, options)` builds the duel and runs turn 1 up
to the first decision. `LegalActions(player)` lists what that player may do;
`Submit(command)` validates with the same rules and applies it, or rejects it
with the rule that blocked it. `Clone()` deep-copies the state for the AI's
one-step lookahead.

## Priority and passing

`Pass` passes priority. Two consecutive passes on an empty chain advance the
phase or step; with a pending attack they enter the Damage Step; with links
on the chain they resolve it last in, first out. After a summon, activation
or attack the turn player keeps or regains priority (ignition-effect
priority). With `DuelOptions.AutoPass` (default on) the engine passes for a
player whose only legal action is `Pass` in a window with nothing to decide:
response windows, Draw, Standby and End Phases, Start and End Steps. The
turn player is never passed for in a Main Phase or the Battle Step, so
ending the turn is always their explicit `Pass`. `Pass` in Main Phase 1 goes
straight to the End Phase; `EnterBattlePhase` opens the Battle Phase, and
Main Phase 2 follows it.

## Tier 1 scope and stubs

- Rules in: first player draws on turn 1, no Battle Phase on turn 1, one
  Normal Summon or Set per turn, tributes by level, position change once per
  turn (not on arrival, not after attacking), Flip Summon, Damage Step
  sub-steps with face-down flip before calculation, direct attacks only on
  an empty field, hand limit 6 with End Phase discards, loss by 0 LP or by
  drawing from an empty deck, surrender.
- `IEffect` carries `Costs`/`Targets` as `Choice` prompts, `TriggerWindow`
  and `SpellSpeed`; `ChainResolver.CanChain` holds the speed rule. Only
  `Activation` effects on Normal Spells are wired; Quick-Play, Traps and
  monster effects are rejected with a message naming tier 2.
- The `EffectRegistry` refuses unknown effect ids at load time, so a card
  file for an unimplemented tier fails in `CardLoader`, not mid-duel.
- Copy limits (`limit`) are data for the deck editor (systems.md §8); the
  engine only checks deck size 40–60 and Fusion monsters in the Fusion Deck.
- Replays and modifiers are stubbed: `DamageStep` ends the attack when the
  attacker or target left the field, `AtkMod`/`DefMod` exist but nothing
  writes them yet.
