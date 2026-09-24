# Systems Design Document

**Status:** approved by the director (merged #13); camera decision applied 2026-09-08 · **Issue:** #4 · **Author:** claude-fable
**Depends on:** [gdd.md](gdd.md) · **Answers:** gpt-astra's [technical handoff](../requests/art-direction-review.md)

This is the specification the C# code is written against. It turns the
mechanics in the GDD into systems, data schemas and tuning values. Engine
conventions (folders, naming, export pipeline) live in the technical design
(issue #7); this document says *what* each system does and *how it is
modelled*.

Toolchain: Godot 4.7 .NET, C# on .NET 8, Forward+ renderer.

---

## 1. Principles

1. **The duel engine is pure C#.** No Godot types inside it. It is a
   deterministic state machine driven by commands and emitting events, so it
   can be unit-tested without the editor and replayed from a seed.
2. **Cards are data plus small effect classes.** Card stats and text are
   JSON; behaviour is a C# class per effect keyed by id. No scripting
   language in the slice.
3. **Presentation subscribes to events.** The 3D duel scene, HUD and VFX
   react to engine events and never mutate engine state directly.
4. **One rig, one character system.** The avatar, NPCs and duelists are the
   same `Character` scene with different part data.
5. **Levels are composed from components I provide.** The level designer
   places markers, volumes and kit pieces; behaviour lives in reusable
   scenes and scripts.

---

## 2. Runtime structure

### 2.1 Autoloads (singletons)

| Autoload | Responsibility |
| --- | --- |
| `Game` | Top-level state: current mode (Overworld, Duel, Menu), scene transitions, pause |
| `SaveSystem` | Serialise and restore `SaveData` (§9) |
| `CardDatabase` | Loads `data/cards/*.json` into `CardDefinition` records |
| `Collection` | Player's owned cards, deck, coins |
| `Audio` | Buses Music, SFX, UI, Ambience; music cross-fades; layered duel music (base plus intensity stem faded in below 2000 LP); SFX pooling |
| `Input` (wrapper) | Maps actions, tracks last device for prompt icons |

### 2.2 Modes

```
Boot → MainMenu → AvatarCreator → Overworld ⇄ Interior
                                      ↓ encounter
                                    Duel → Result → Overworld
                                      ↓ final win
                                    Ending → Overworld (free play)
```

`Game.Mode` is a small state machine. Only Overworld and Interior allow
movement. Duel freezes all overworld actors but does not unload the scene.

Menu (issue #168) is entered when the first screen is pushed onto
`Game.Menus` (`MenuStack`, a `CanvasLayer` child of `Game`) and left when
the last one is popped, restoring the previous mode. While open, the stack
locks player input (lock reason `menu`) and stops the whole `World` subtree
(`Game.PauseWorld`), so actors, cones and the camera freeze while the UI
layers and the autoload keep processing. The stack refuses to open unless
the mode is Overworld or Interior with no transition, encounter or message
running (`MenuStack.CanOpen`). The gamepad, keyboard and mouse rules live in
one place, the stack: `move_*` moves the cursor with wrap-around, `interact`
presses, `cancel` pops (the top screen can consume it), and hovering moves
the cursor. Screens extend `MenuScreen` and register their focusable
controls; the screen below the top one is hidden but keeps its cursor.

---

## 3. Entity model

### 3.1 Character

One scene, `Character.tscn`, used for the avatar, NPCs and duelists.

```
Character (CharacterBody3D)
├── Rig (Skeleton3D, humanoid profile)
│   ├── Body (MeshInstance3D, skinned)     ← body type mesh
│   ├── Hair (MeshInstance3D, skinned)
│   ├── Top / Bottom / Shoes / Accessory   ← swappable, skinned to Rig
│   └── DuelDiskMount (BoneAttachment3D → LeftLowerArm)
│       └── DuelDisk.tscn
├── Collision (CapsuleShape3D r 0.35, h 1.7)
├── AnimationTree (state machine, §4.1)
├── InteractionShape (Area3D, for Talk)
└── Controller (one of PlayerController, NpcController, DuelistController)
```

`CharacterAppearance` is a resource: body type, skin tone index, hair
style, hair colour, outfit id, accent colour. Applying it swaps meshes and
sets shader parameters. The avatar creator edits this resource live.

### 3.2 Rig contract (answers the handoff)

- Skeleton uses Godot's `SkeletonProfileHumanoid` bone names so retargeting
  and the bone map work in the importer.
- Rest pose: A-pose, facing −Z, Y up, 1 unit = 1 m, origin at the feet.
- Both body types share the skeleton hierarchy; bone lengths may differ.
  Hand-to-disk alignment is solved by the mount, not the animation.
- **Duel disk attachment:** a `BoneAttachment3D` on `LeftLowerArm`, with a
  per-body-type offset transform stored in `data/rig/disk_mount.json`. The
  disk's own origin is the forearm strap centre, blade pointing +X in its
  local space when deployed.
- **Animation events** are Godot call-method tracks added by the engine
  from `data/rig/animation_events.json` (clip → event → seconds) when it
  builds a character's animation library; glTF cannot carry method tracks,
  so the artist reports timings in the handoff. Every key calls
  `OnAnimationEvent(name)` on the `Character`, which re-emits it as the
  `AnimationEvent` signal. The engine listens for these names:

| Event | Emitted by clip | Used for |
| --- | --- | --- |
| `disk_deploy` | duel_ready | Start blade unfold, zones light up |
| `card_draw` | draw_card | Card leaves deck anchor into hand |
| `card_release` | play_card | Card flies from hand to its zone anchor |
| `card_to_grave` | play_card / take_damage | Card slides into graveyard slot |
| `hit` | take_damage | LP tick and hit VFX |
| `footstep_l`, `footstep_r` | walk, run | Footstep SFX by surface |

### 3.3 Interactables

All implement `IInteractable { string Prompt; void Interact(Character by); }`
and live in the `interactable` collision layer.

| Scene | Data | Behaviour |
| --- | --- | --- |
| `TalkNpc` | dialogue id | Runs a `Dialogue` resource |
| `Sign` | text | One box |
| `Door` | target scene, spawn id | `Game.Transition` with fade |
| `ShopCounter` | shop id | Opens `ShopUi` |
| `Duelist` | duelist id | Dialogue then `EncounterSystem.Start` |

### 3.4 Level markers (components for the level designer)

| Marker | Placed by level designer | Consumed by |
| --- | --- | --- |
| `PlayerSpawn` (id) | start and door return points | `Game.Transition` |
| `EncounterSite` | centre, axis, two stand points 7 m apart, clearance box | `DuelStaging` |
| `CameraBounds` (Area3D) | walkable region camera limits | `CameraRig` |
| `SurfaceTag` on collision | stone, grass, wood | footstep SFX |
| `AmbientZone` (Area3D) | music and ambience ids | `Audio` |
| `LevelBounds` | kill or block volume | `PlayerController` |
| `ProgressionGate` | blocker mesh plus collision, `requiredFlag` (e.g. `defeated:d1`) | `Game` flags; gate opens with a short animation when the flag is set |

---

## 4. Overworld systems

### 4.1 PlayerController

Character-relative analogue movement; camera yaw is fixed so input maps
directly to world XZ.

State machine (in `AnimationTree` and code):

```
Idle ⇄ Walk ⇄ Run
  ↓ interact          ↓ encounter
Talking            DuelReady → (Duel mode)
```

| Parameter | Value |
| --- | --- |
| walk_speed | 2.2 m/s |
| run_speed | 4.5 m/s |
| run_threshold | 0.6 stick deflection |
| accel_time | 0.12 s |
| decel_time | 0.08 s |
| turn_rate | 720 °/s |
| step_height | 0.3 m (`floor_max_angle` 40°) |

NPCs use the same movement with a `NpcController` that idles, wanders
between waypoints, or faces the player when talked to.

### 4.2 CameraRig

```
CameraRig (Node3D, follows target on XZ with smoothing)
└── Pivot (rotation.x = −pitch)
    └── Camera3D (position.z = distance, fov = vfov)
```

| Parameter | Overworld | Interior | Duel |
| --- | --- | --- | --- |
| pitch | 57° | 50° | 40° |
| distance | 12 m (GDD baseline 12–14) | 7 m | 9 m from field midpoint |
| vertical fov | 35° | 35° | 40° |
| follow smoothing | 0.15 s | 0.15 s | fixed |

**Framing (director decision, 2026-09-08):** the GDD values stand: pitch
55–60°, distance 12–14 m, 35° vertical FOV, 0.15 s smoothing. The resulting
avatar pixel height is an outcome, not a target. The "nine
character-heights" and pixel lines on sheet 01 are withdrawn. The
`tests/scenes/CameraFraming.tscn` scene remains a diagnostic for outfit and
disk readability, not a basis for changing the camera without the director.

Bounds: the rig clamps its XZ to the union of `CameraBounds` volumes with a
1 m soft margin.

### 4.3 EncounterSystem

1. `Duelist` detects the player in its cone (8 m, 60°) or is interacted with.
2. Input freezes on the spot and the camera **reveals the duelist**: it
   blends to frame them (overworld pitch and distance, 0.6 s) while the
   exclamation shows over their head, and holds for 1.4 s in total. The
   player always sees who spotted them before anything moves (director
   note, 2026-09-13: Nico's cone reaches the room door, so the player used to
   be walked away by someone off screen).
3. The camera blends back to the player; the duelist walks to the nearer
   stand point of the closest `EncounterSite` (or 3.5 m from its own
   position if no site is within 10 m, snapped to the navmesh) while the
   controller walks the player to the opposite one. The cone is disarmed
   for 3 s after a duel ends so a loss cannot retrigger while the player is
   still inside it, and stays disarmed after the first victory.
4. Challenge line from `data/duelists.json`. Both play `duel_ready`; on the
   player's `disk_deploy` event (or after 1 s) the `DuelDirector` stages the
   duelists, deploys the disks, starts the engine, binds `DuelStaging` and
   `DuelUi` and blends the camera to the duel framing over 1.2 s.
5. `Game.Mode = Duel`; the player's collection deck (§8) faces the duelist's
   deck and AI profile; the duel seed and the agent's seed come from
   `Game.Rng`.
6. On result: `win`/`lose` clips and the HUD banner for 2 s, then the cards
   dissolve, the disks fold and the camera blends back; the duelist's
   `win_line` or `lose_line` plays, a win adds `defeated:<id>`, coins and
   boosters (§8, opened into the collection and listed in a second line);
   `tutorial_done` is set after the first duel either way. A loss fades the
   player back to the encounter spot. Autosave lands with #63.

Meeting positions that fail a capsule sweep are resolved by searching
outward along the site axis in 0.5 m steps for up to 3 m. No teleport to
another location, ever.

Implementation (issue #62): `EncounterSystem` states are Idle → Reveal
(camera on the duelist, `CameraRig.Reveal`) → Approach → Dialogue → Duel →
Result → Lines → Returning (loss only). `Game.Duels` is a `DuelDirector`
under the world node that owns the staging, the session and the HUD for the
whole run; encounters call `Begin`/`End` on it and listen to `Finished`.
`Game.Data` loads `data/` on first use, `Game.Collection` starts as the
`starter` deck on New Game, `Game.Rng` is a `DuelRng` seeded from
`Game.Seed`. `EncounterSystem.LastRewardCoins` / `LastRewardCards` expose
the last reward for the acceptance.

### 4.4 Collision layers

| Bit | Layer | Contents | Player mask | NPC mask | Camera |
| --- | --- | --- | --- | --- | --- |
| 1 | `world` | Kit geometry, kerbs, planters | ✓ | ✓ | ✓ |
| 2 | `character` | All `Character` bodies | ✓ | ✓ | |
| 3 | `interactable` | Interaction areas | ✓ (area query) | | |
| 4 | `trigger` | Encounter cones, ambient zones, bounds | ✓ (area) | | |
| 5 | `card` | Holographic card colliders for cursor picking | | | ✓ (ray) |
| 6 | `camera_blocker` | Optional occluders that fade | | | ✓ |

Navigation: one `NavigationRegion3D` per level, baked from `world`, agent
radius 0.4 m. Used only for duelist approach paths in the slice.

---

## 5. Duel engine

### 5.1 Architecture

```
Duel.Core (class library, no Godot)
├── Model:   DuelState, PlayerState, Zone, CardInstance, CardDefinition
├── Rules:   TurnFlow, SummonRules, BattleRules, ChainResolver, DamageStep
├── Effects: IEffect implementations keyed by effect id
├── Commands: PlayerCommand (what a player wants to do)
├── Events:  DuelEvent (what happened), consumed by presentation and AI
├── Ai:      IDuelAgent, HeuristicAgent
└── Rng:     seeded xoshiro; all shuffles and coin flips go through it
```

`DuelEngine.Submit(PlayerCommand)` validates against `LegalActions`,
mutates state, and appends events. Presentation reads the event stream;
the AI reads state and `LegalActions`.

### 5.2 State model

```csharp
record DuelState {
  PlayerState[] Players;      // 0 = player, 1 = opponent
  int TurnPlayer; int TurnNumber;
  Phase Phase; BattleStep BattleStep;
  Chain Chain;                // list of ChainLink, resolves LIFO
  int Priority;               // which player may act now
  Window Window;              // Open, Summon, AttackDeclared, DamageBeforeCalc, DamageAfterCalc
  List<PendingTrigger> Triggers;   // fired, not on the chain yet
  ChainLink? PendingLink;     // activation collecting cost/target answers
  PendingChoice? PendingChoice;    // the question the engine waits on
  List<Modifier> Modifiers;   // timed modifiers from resolved effects (the registry)
  Flags: NormalSummonUsed, BattlePhaseUsed, Attacker/AttackTarget, FirstTurn
}
record PlayerState {
  int LifePoints;
  Zone Deck, Hand, Graveyard, Banished, FusionDeck;
  Zone[] MonsterZones (5), SpellTrapZones (5);
  Zone FieldZone;             // the Field Spell in play
  PlayerRestriction Restrictions;  // computed: NoBattleDamage, TrapsNegated
}
record CardInstance {
  Guid Id; CardDefinition Def; int Owner; int Controller;
  Location Loc; int ZoneIndex;
  Position Pos;               // FaceUpAttack, FaceUpDefense, FaceDownDefense, FaceDown (S/T), FaceUp (S/T)
  int AtkBonus, DefBonus; Restriction Restrictions;   // computed from the modifiers, never written by effects
  Guid? EquippedTo;           // equips: the monster this card is attached to
  int? ControlReturnsAfterTurn;    // temporary control changes
  bool SetThisTurn; bool ArrivedThisTurn; bool FlippedThisTurn; bool ChangedPositionThisTurn; bool AttackedThisTurn;
  Dictionary<string,int> Counters; Dictionary<string,int> ActivationsThisTurn;   // once-per-turn tracking
}
record Modifier(ModifierKind Kind, int Value, Guid Source, Guid? Card, int? Player, int? ExpiresAfterTurn);
// Kinds: Atk, Def, CannotAttack, CannotBeAttacked, CannotChangePosition, CannotBeDestroyedByBattle,
// CannotBeTributed, Piercing, CanAttackDirectly, EffectsNegated (card kinds); NoBattleDamage, TrapsNegated (player kinds).
// Scope: one card, one player (their monsters for card kinds), or the whole duel.
```

Tokens are `CardInstance`s whose definition has `IsToken`; they are created
on the field and leave the duel the moment they leave it (`TokenRemoved`),
so they never appear in a hand, Deck, Graveyard or Banished pile.

### 5.3 Card definition schema (`data/cards/<id>.json`)

```json
{
  "id": "gemini_elf",
  "name": "Gemini Elf",
  "kind": "monster",
  "monster": { "type": "Spellcaster", "attribute": "EARTH", "level": 4,
               "atk": 1900, "def": 900, "category": "normal" },
  "spell": null,
  "trap": null,
  "text": "Gemini elf twin sisters who alternate their attacks.",
  "limit": 3,
  "tier": 1,
  "effects": []
}
```

`kind` ∈ monster, spell, trap, fusion. `spell.subtype` ∈ normal, quick,
equip, continuous, field, ritual. `trap.subtype` ∈ normal, continuous,
counter. `monster.category` ∈ normal, effect, fusion, ritual, flip, spirit,
toon, union. `effects` is a list of effect ids; each maps to a C# class
registered in `EffectRegistry`. Fusion monsters carry `"materials"`.

Names and text are stored as needed to implement the rules; no card art.

### 5.4 Effect model

```csharp
interface IEffect {
  EffectKind Kind;            // Ignition, Trigger, Quick, Continuous, Flip, Activation (spell/trap), Condition, SummonProcedure
  SpellSpeed Speed;           // 1, 2, 3
  bool IsMandatory;
  TriggerWindow? Trigger;     // OnSummon, OnDestroyedByBattle, OnFlip, OnSentToGrave, Standby, EndPhase, OnBattle, OnBattleDamage, SpellActivated
  bool FiresIn(TriggerWindow w);  // Trigger == w by default; Tsukuyomi fires in OnSummon and OnFlip
  SummonLimit Limits;         // CannotBeNormalSummoned, CannotBeSet (Chaos Sorcerer, Mystic Swordsman LV2)
  bool RemainsOnField;        // a Spell that stays after resolving (Swords of Revealing Light)
  bool UsableInDamageStep;    // speed 2 ATK/DEF modifiers may activate before damage calculation
  bool OncePerTurn;           // the engine counts activations per card and turn
  bool CanActivate(DuelState s, CardInstance src, ActivationContext ctx);  // card-specific condition only
  IReadOnlyList<Choice> Costs(DuelState s, CardInstance src);    // asked in order at activation
  IReadOnlyList<Choice> Targets(DuelState s, CardInstance src);  // asked after the costs are paid
  void PayCosts(DuelEngine e, ChainLink link);   // once, before targets; answers in link.Costs
  void Resolve(DuelEngine e, ChainLink link);    // skipped for a negated link; answers in link.Targets; may ask more through e.Ask(link, choice)
  IEnumerable<Modifier> Modifiers(DuelState s, CardInstance src);   // what the card grants while face-up on the field
  void OnLeftField(DuelEngine e, CardInstance src, Location from, Guid? equippedTo);   // continuous consequences, no chain
}
```

`EffectBase` supplies defaults for everything an effect does not use. The
engine owns the timing every effect shares (speed, priority, Set turn,
Damage Step limits); `CanActivate` holds only the card's own condition and
reads the window it needs from `DuelState.Window`, the chain, or the
`ActivationContext` (the trigger window, for graveyard triggers the
location the card came from, for summon triggers the `SummonKind`: Normal,
Tribute, Flip or Special; the summon window also records it in
`DuelState.LastSummon` for Trap Hole; for battle triggers `Battled`, the
monster it fought, null for a direct attack). `OnBattle` fires after damage
calculation for both monsters wherever they ended up (D.D. Warrior Lady),
`OnBattleDamage` for a monster that inflicted battle damage to a player
(Don Zaloog, Airknight Parshath, Kycoo, Reaper on the Nightmare),
`SpellActivated` for every face-up monster when a Spell is activated
(Skilled Dark Magician). Standby and End Phase triggers are also offered to
cards in the Graveyards (Sinister Serpent). A `SummonProcedure` effect is
an inherent Special Summon from the hand (Chaos Sorcerer): it is activated
like an Ignition effect, its costs are the materials, then the monster
arrives with no chain link. A card whose effects list a `SummonLimit`
cannot be Normal Summoned or Set accordingly. An effect may carry
`Modifiers` whatever its kind, so a Trigger monster with a continuous side
(Airknight's piercing, Reaper's protection) is one class; a card with two
activatable effects carries two ids (Breaker, Chaos Sorcerer, Skilled Dark
Magician, Snatch Steal's upkeep).

**Choice protocol.** Everything that asks a player something pauses the
engine in a `PendingChoice { Player, Kind, Source, Choice }` and is
answered with the `AnswerChoice(player, selected)` command; while one is
pending no other command is legal and `LegalActions` enumerates every
valid selection. Kinds: `Cost` and `Target` (the activation in progress
collects the answers on its `ChainLink` before joining the chain),
`OptionalTrigger` (activate this trigger, yes or no) and `TriggerOrder`
(which of several mandatory triggers goes on the chain next). The player
UI renders the prompt as a modal, the AI answers it programmatically, and
no card needs UI code. `DuelEngine.Clone` copies pending state, so the AI
can look ahead through a prompt.

**Questions during resolution.** Costs and targets are settled before a
link joins the chain, but Graceful Charity discards after drawing, Sangan
picks from the Deck and Dust Tornado offers a Set only after the destroy.
A resolving effect asks with `engine.Ask(link, choice)` (or
`engine.Ask(link, choice, player)` to put the question to the other player:
Delinquent Duo's discard, Creature Swap's second pick): when the answer is
already in `ChainLink.Answers` it comes straight back; otherwise the
question becomes a `PendingChoice` of kind `Resolution`, the link parks on
`DuelState.ResolvingLink`, chain resolution stops and the effect returns.
The answer re-runs `Resolve` from the top, where the same `Ask` now
returns it; work done before the question is guarded with
`ChainLink.Stage` so it is not repeated. Once the link finishes, the rest
of the chain resolves. Clones copy the answers and the stage, so the AI's
one-step lookahead evaluates each answer like any other prompt. A
question with no options answers itself with an empty selection.

**Modifier registry.** Two sources feed one computation. A resolved effect
that lasts ("gains 700 ATK until the End Phase", "cannot change position
until the end of your next turn", Waboku) registers a timed `Modifier` on
`DuelState.Modifiers` with `engine.AddModifier`; it expires at the End
Phase of `ExpiresAfterTurn` (`DuelState.NextTurnOf(player)` gives "your
next turn") or when its card leaves the field or is flipped face-down. A
Continuous effect or an equip (Jinzo, Command Knight, Axe of Despair)
returns its modifiers from `IEffect.Modifiers` every time they are asked
for, so nothing stored points at a card that left. `Modifiers.Recompute`
runs after every state change (every card move, position or control
change, draw, counter, life point change and chain link) and projects both
onto the computed fields the rules read: `CardInstance.AtkBonus`,
`DefBonus` and `Restrictions`, `PlayerState.Restrictions`. Effects never
write those fields. A card whose effects are negated contributes nothing
and its links resolve to nothing; while a player's Traps are negated they
cannot activate Traps, their Continuous Traps contribute nothing and their
Trap links resolve to nothing.

**Engine helpers for card effects.** `Draw`, `Damage` (battle) and
`InflictDamage` (effect), `PayLifePoints` (a cost; paying the last point
loses), `GainLifePoints`, `Destroy`, `SendToGraveyard`, `Discard`,
`DiscardRandom` (through `DuelRng`, so replays match), `Banish`,
`ReturnToHand`, `ReturnToDeck`, `ShuffleDeck`, `SpecialSummon` (hand,
Deck, Graveyard, Banished or Fusion Deck; refuses Spirits and full fields;
fires summon triggers and opens the summon window), `CreateToken`,
`ChangeControl` (permanent or until the End Phase of a turn; needs a free
zone; control always returns when the monster leaves the field), `Equip`
(an Equip Spell that attached to nothing is spent), `FlipFaceDown` (Book
of Moon; the equips are destroyed once the monster is face-down, so
Premature Burial and Call of the Haunted leave it alone), `FlipFaceUp`
(Swords of Revealing Light; the Flip Effect fires), `SwitchPosition`
(Enemy Controller; destroys a Berserk Gorilla), `SwapControl` (Creature
Swap; the two monsters trade zones, so no free zone is needed),
`SetSpellTrap` (from the hand by an effect, Dust Tornado), `AddCounter`,
`AddModifier`, `Negate`, `Ask` (a question while a link resolves).
`Damage` returns whether damage was inflicted, `Destroy` takes the monster
that destroyed it by battle and a flag that silences every trigger of the
destroyed card (Dark Balter). When a monster leaves the field its equips
are destroyed and its effects' `OnLeftField` hooks run (Snatch Steal
returns control, Premature Burial and Call of the Haunted destroy their
monster) before its field state is cleared. A card with `LeavesAfterTurn`
set (Swords of Revealing Light) is destroyed at that turn's End Phase.

### 5.5 Turn flow and timing

Phases and steps as in GDD §3.2. The engine implements 2005 rules:

| Rule | Implementation |
| --- | --- |
| First player draws on turn 1 | `TurnFlow.Draw` has no first-turn exception |
| No attack on the first player's first turn | `BattleRules.CanEnterBattlePhase` checks `TurnNumber == 1` |
| Ignition-effect priority | A summon opens the `Summon` window with priority on the turn player, who may activate the monster's Ignition effects (and speed 2 cards) before passing; the opponent's responses to the summon wait for that pass |
| Spell speed chain rule | `ChainResolver.CanChain(link)` requires `Speed >= previous.Speed`, so only speed 3 answers a Counter Trap; a negated link is skipped at resolution and its card still goes where it would have gone |
| Set cards wait a turn | `SetThisTurn` blocks Traps and Set Quick-Play Spells; Quick-Play Spells from the hand need the turn player's own turn |
| Trigger ordering | Fired triggers queue on `DuelState.Triggers`; mandatory ones go on the chain first (turn player's, then opponent's; a controller with several is asked to order them), then optional ones are offered one at a time in the same order. The opponent of the last link gets priority to respond |
| Damage Step | Sub-steps: StartDamage, BeforeCalc (face-down target flips; only Counter Traps and effects flagged `UsableInDamageStep` may activate), Calc, AfterCalc (flip effects and battle-destruction triggers go on the chain; nothing else activates on an empty chain), EndDamage. Each half is a window closed by two passes |
| Position changes | Once per turn, not on the turn summoned, not after attacking, not under a `CannotChangePosition` modifier (position locks carry a turn number); a monster flagged `DestroyedInDefensePosition` (Berserk Gorilla) is destroyed when its controller switches it to Defense Position |
| Battle restrictions | `CannotAttack`, `CannotBeAttacked` and `CanAttackDirectly` shape the legal attacks; `CannotBeDestroyedByBattle` and `NoBattleDamage` apply in damage calculation; a `Piercing` attacker inflicts the difference over a Defense Position target; an `AttacksEveryMonster` monster (Asura Priest) may attack each of the opponent's monsters once (`CardInstance.AttackTargetsThisTurn`); a `DestroysFaceDownTargets` attacker (Mystic Swordsman LV2) destroys a face-down target at the start of the Damage Step with no flip and no damage calculation |
| Negation by battle | A monster destroyed by battle by a `NegatesEffectsOfDestroyed` monster (Dark Balter) fires no flip, battle-destruction or graveyard trigger; one destroyed by a `NegatesFlipEffectsOfDestroyed` monster (a lone Blade Knight) fires no Flip Effect |
| Targeting | A monster flagged `DestroyedWhenTargeted` (Reaper on the Nightmare) is destroyed the moment it is chosen as a `Target` |
| Summon locks | `PlayerRestriction.CannotSummon` (Scapegoat, for the turn) blocks Normal, Flip and Special Summons but not Sets; `CannotBanishFromGraveyard` (Kycoo's opponent) blocks Chaos Sorcerer's summon |
| Must attack | A face-up Attack Position monster flagged `MustAttack` (Berserk Gorilla) that could attack blocks its controller's `Pass` in Main Phase 1 (while the Battle Phase can still be entered) and in the Battle Step, so the attack cannot be skipped; `LegalActions` leaves the pass out |
| After attacking | Monsters flagged `DefenseAfterAttack` (Goblin Attack Force, Giant Orc) that attacked switch to Defense Position when the Battle Phase ends and get a `CannotChangePosition` modifier until the end of their controller's next turn |
| Summon responses | The summon window records `LastSummon`; Torrential Tribute answers any summon, Trap Hole only the opponent's Normal, Tribute and Flip Summons of 1000+ ATK; the Monarchs' triggers fire only with `ActivationContext.Summon == Tribute` |
| Once per turn | `IEffect.OncePerTurn`; activations are counted per card and effect on `CardInstance.ActivationsThisTurn` and reset with the turn or when the card leaves the field |
| End of turn | Entering the End Phase expires timed modifiers, returns temporary control and sends Spirit monsters Normal Summoned or flipped face-up this turn back to the hand, before the End Phase triggers fire |
| Tokens | Never tributed for a Tribute Summon when flagged `CannotBeTributed`; removed from the duel on leaving the field |
| Tribute count | Level 5–6 one, 7+ two |
| Fusion | Polymerization or Metamorphosis; result from FusionDeck |
| Hand limit | 6 at End Phase; discard prompt |
| Loss | LP ≤ 0, or draw with empty deck |

Priority passes alternate. Two consecutive passes resolve the chain; on an
empty chain they close the current window: `Open` advances the phase or
step, `Summon` returns to the open Main Phase, `AttackDeclared` enters the
Damage Step, `DamageBeforeCalc` runs damage calculation, `DamageAfterCalc`
ends the Damage Step. After an activation the opponent of the activator
holds priority; after a chain resolves or a window closes, the turn player
does. With `DuelOptions.AutoPass` (the default) the engine passes for a
player whose only legal action is `Pass` in a window or in the Draw,
Standby and End Phases and the Start and End Steps; the turn player is
never passed for in the open Main Phase or Battle Step. An attack whose
attacker or target left the field before the Damage Step is cancelled
(`AttackCancelled`); there is no replay. A monster that leaves the field
and comes back is a new monster: `CardInstance.FieldStay` counts up on every
exit, the declaration records the attacker's and target's stay
(`DuelState.AttackingMonster`, `AttackedMonster`), and a revived attacker
has no attack pending and may declare one of its own. Damage calculation
emits `BattleFought` with both monsters and the values they fought with,
then `BattleDamage` or `NoBattleDamage`, then the destructions
(`MonsterDestroyed` carries the reason), so the log tells the whole battle.

### 5.6 Implementation tiers → build order

| Tier | What it needs | Cards (from GDD §4.5) |
| --- | --- | --- |
| T1 | Vanilla monsters, Pot of Greed | 8 |
| T2 | Single simple effect: draw/discard, destroy, search, flip-reveal, stat equip | 30 |
| T3 | Targets, costs, continuous modifiers, quick timing, tokens, position control | 30 |
| T4 | Replacement and unusual effects: Cyber Jar, BLS, Thousand-Eyes Restrict, Spirit Reaper | 4 |

Build the engine T1 → T4; each tier has a test suite. The Beatdown duel is
playable at T2, Warrior Toolbox at T3, Goat Control at T4.

Status: all four tiers are implemented (T1 issue #25, T2 #56
`TierTwoTests`, T3 #57 `TierThreeTests`, T4 #58 `TierFourTests`) in
`Effects/Cards/` with one scenario test each; every list in `data/decks/`
is accepted by `DuelEngine.Start` and the heuristic agents play all of
them to a winner. The validator requires every effect id to be
implemented. Cards with two activatable effects carry two ids: Axe of
Despair (`axe_of_despair_recycle`), Breaker (`_destroy`), Chaos Sorcerer
(`_banish`), Skilled Dark Magician (`_summon`), Snatch Steal (`_upkeep`),
Black Luster Soldier (`_banish`, `_double_attack`), Thousand-Eyes
Restrict (`_absorb`); Goblin Attack Force and Giant Orc, Mystic Tomato and
Shining Angel, Chaos Sorcerer and Black Luster Soldier, Spirit Reaper and
Reaper on the Nightmare share one class registered under both ids.
Thousand-Eyes Restrict's absorbed monster becomes an Equip Card in a
Spell & Trap Zone (`DuelEngine.AbsorbMonster`, `MonsterAbsorbed`): its own
effects are off (`IsAbsorbed`), it is destroyed in the Restrict's place
when battle would destroy it, and it leaves with it. Cyber Jar reveals
with `CardsRevealed` and summons in Attack Position. Scapegoat's Sheep Tokens are
defined in code (`sheep_token`), not in `data/cards/`. Skilled Dark
Magician's summon looks for a card named "Dark Magician", which the pool
does not contain.

### 5.7 Tests

`Duel.Core.Tests` (xUnit): rule tests per §5.5 row, one scenario test per
card, and fuzz duels between two random agents that assert invariants
(zone counts, LP bounds, chain empties, no orphan instances). Fuzz runs
from fixed seeds so failures replay.

---

## 6. Duel presentation

### 6.1 DuelStaging

Owns everything 3D during a duel. On start it:

1. Places both characters at the site's stand points, facing each other.
2. Spawns `CardAnchors` per side: **ten `Marker3D`s in a 2 × 5 grid**
   1.8 m in front of the duelist's chest, 0.92 m apart horizontally, rows
   1.2 m apart, monsters in front, spells and traps behind. Anchors are
   generated from the stand point and axis, **never from the disk mesh**,
   so bay spacing on the prop is cosmetic. Deck, graveyard and banished
   anchors sit on the disk's mount points via `DuelDisk` markers.
3. Positions the duel camera from the site (§4.2).

Director-requested location sizes (2026-09-20): the base quad remains
0.20 × 0.292 m. Field cards scale 3× (0.60 × 0.875 m), disk piles scale
0.25× (0.05 × 0.073 m), and the 3D hand scales 1.1×. Mesh, picking collider,
and anchored effects share the presentation transform; moves tween size with
position. The production HUD hand remains in screen space, now 156 px wide
per card (previously 140), with its bottom edge inside the viewport.

Cards are `CardView` instances: a quad with the frame texture, art
texture, stars, attribute and stat labels rendered as a `SubViewport`
texture at 590 × 860, plus an emissive edge material. Attack = upright,
Defence = rotated 90° in the card's plane (about its Z), with a 0.15 s
tween. Set cards lie flat, perpendicular to the upright cards, face to the
ground and back up, dropped half a card height so they rest at the foot of
the upright cards: a Set Spell/Trap keeps its long side along the duel axis,
a Set monster is turned 90° so its long side runs across the row. The wider row spacing separates the flat and upright silhouettes from the raised duel camera. Upright face-down
(turned 180° about Y) remains for the opponent's hand. On the disk the deck
lies face to the floor so nobody reads it, the graveyard and the banished pile
face the sky, and piles grow upward, whichever way the disk's markers point.
Upright cards mirror by the side they are on, not by owner, so a monster
taken by the opponent still faces the player's camera.

Implementation (issue #60):

- `DuelStaging.Stage` builds a side root per duelist at the stand point,
  looking at the other; under it the `CardAnchors` grid (`m1..m5`,
  `st1..st5`), a `hand` row beside the body on the free hand's side (0.62 m
  out, 0.25 m forward, 0.95 m up, 0.11 m between cards) so the camera behind
  the duelist sees it, and the deck, graveyard and banished piles on the
  `DuelDisk` markers, or on fallback anchors when the prop is missing. Piles
  stack 1.5 mm per card along the marker normal.
- `DuelStaging.Bind(engine)` makes one `CardView` per `CardInstance` and
  marks itself dirty on every engine event; `Sync` then reconciles every
  card with its `CardInstance` (`Loc`, `ZoneIndex`, `Controller`, `Pos`):
  the position of a card is always what the state says, the events only
  drive the transient effects (reveal on draw and activation, the attack
  lunge, the character's draw / play / hit / win / lose states, dissolve for
  tokens that left). Tokens get a view when they first appear.
- The owner's face-up cards face +Z of their anchor (toward the owner and
  the camera behind them); the opponent's are mirrored so both sides read
  from the player's camera. The opponent's hand is face-down.
- `CardFaces` composes a face once per card id in a 590 × 860 `SubViewport`
  (§6.2), bakes it to an `ImageTexture` after two frames and frees the
  viewport; headless runs keep the viewport texture. Stats use a system
  serif (`DejaVu Serif`, `Georgia`, `Times New Roman`, then the default
  font) at 80 px, centered on the plates by the font's metrics.
- `CameraRig.EnterDuel(focus, yaw, pitch, distance, fov, blendTime)` blends
  from the current pose to a held framing and ignores bounds while it holds;
  `ExitDuel` blends back to following. `DuelStaging.EnterCamera` uses the
  §4.2 duel column: raised above the player's right shoulder, yawed 12°
  right of the duel axis toward the field midpoint at 0.8 m height,
  40° pitch / 9 m distance / 40° FOV over 1.2 s.
- `tests/scenes/DuelStagingTest.tscn` is the acceptance (tests/scenes/README.md).

### 6.2 Card face layout and art window

`CardArtwork` prefers decoded `local-card-art/<id>.png` in project runs, with
committed generated art as fallback; exports use generated art only. The
centered-cover layout below is the same for both sources. Duel staging/UI and district
acceptance scenes disable local art for captures; `--generated-art` opts out in
other project runs. Restart after changing overrides because faces are cached.


**Director-approved generated design (2026-09-13):** the final visual authority
is [the approved sheet](../art/references/card-design/approved-final-layout.png).
This supersedes reference-cropped frames and prior front layout specifications.
See [reference provenance and review guide](../art/references/card-design/README.md).
The exported card remains 590×860; source gameplay art remains 512×512.

The exact contract is `assets/source/cards/frame-layout.json`, revision 5:

```json
{
  "revision": 5,
  "size": [
    590,
    860
  ],
  "bevel_width": 10,
  "art_window": [
    10,
    10,
    570,
    610
  ],
  "data_panel": [
    0,
    630,
    590,
    230
  ],
  "star_size": [
    34,
    34
  ],
  "star_step": 37,
  "star_row_y": 686,
  "star_row_center_x": 295,
  "star_row_right_max": 467,
  "attribute": [
    524,
    686
  ],
  "attribute_size": [
    66,
    66
  ],
  "atk": [
    156,
    785
  ],
  "def": [
    434,
    785
  ],
  "stat_alignment": "center",
  "stat_font_px": 80,
  "stat_font_style": "normal",
  "stat_plates": [
    [
      34,
      738,
      244,
      94
    ],
    [
      312,
      738,
      244,
      94
    ]
  ],
  "spell_trap_plate": [
    34,
    693,
    522,
    104
  ],
  "spell_trap_badge": [
    295,
    745
  ],
  "spell_trap_badge_size": [
    72,
    72
  ],
  "subtype_placement": "tooltip_only"
}
```

The art window uses centered cover: square art scales to 610² and clips 20px
on each side, without stretching or letterboxing. Both the gray artwork bevel
and colored outer bevel are 10px wide. The 230px lower panel occupies 26.7% of
the face, matching the director-approved shortened-panel iteration. Use soft
muted cloud mottling, not bright emissive veins; keep plate interiors translucent.

For level L, row width is `(L-1)*37+34`; center at `min(295,467-width/2)`.
The left edge is that center minus half the width. Stars and attribute share
center y=686. Level 12 starts at x=26, ends at x=467 and leaves 24px before the
attribute at x=491. Keep the attribute clear of the lowered stat plates.
ATK/DEF positions denote centers. Use 80px upright serif starting size and font
metrics for vertical centering; never italicize or enlarge the plate to fit.

Spell/Trap's single rectangle and main badge are exactly centered within the
colored panel at (295,745). Subtype badges remain tooltip/inspector-only:
Continuous, Equip, Quick-Play, Counter, Field and Ritual. Normal needs no glyph.
Card data and gameplay rules are unchanged. Example level-12 Fusion and Ritual
review cards are layout fixtures, not new game card definitions.

Fable must adopt revision 5 in CardView and tooltips during #60 integration.
See [handoff](../requests/card-reference-frames.md). Static previews and texture
import validation do not constitute runtime CardView acceptance.

### 6.3 HUD and input

`DuelUi` (CanvasLayer): hand fan, phase bar, LP counters, chain display,
prompt modals from `Choice`, inspector, graveyard list, log. Selection is a
cursor over hand cards and 3D anchors; the 3D cards carry colliders on
the `card` layer for mouse picking. Gamepad moves the cursor by grid.

Implementation (issue #61):

- `DuelSession` (Node) drives one duel in the tree: the HUD submits the
  human's commands through it, the opponent's `IDuelAgent` answers after a
  short delay (`AiDelay`, 0.7 s) so its moves read as moves; `Changed` fires
  after every accepted command and `Finished` once at the end. Encounters
  (§4.3) start a session, bind `DuelStaging` and `DuelUi` to it, and wait for
  `Finished`.
- The HUD knows no card. After every command it reads `LegalActions` and
  decides what to ask: nothing legal → waiting; a `PendingChoice` for the
  player → the picker; only `Discard`s → the hand-limit banner; a chain, an
  open window or the other player's turn → the response prompt; otherwise the
  cursor is free. `ActionCatalog` (Duel.Core, no Godot) groups the legal
  commands that name a card into its menu entries (Summon, Set, Activate,
  Special Summon, Attack, position change, Flip Summon, Discard) and picks
  the phase advance (`EnterBattlePhase` when legal, else `Pass`); `DuelText`
  writes the log lines, command labels, inspector lines and window text.
- The log (`DuelLogPanel`, issue #205) keeps every line of the duel as a
  `LogLine`: text segments with the card reference per name, built by
  `DuelText.Line`; "a face-down card" never carries one. Names render as
  underlined links in the owner's side colour. Hovering a name, or moving
  the log cursor onto it, shows the card in the inspector (off the field
  too) and leaving it restores the previous view. The log key opens the log
  with the cursor in it (`DuelUiMode.Log`, also while waiting or asked to
  respond); up and down walk the lines, left and right the names of a line,
  Interact jumps to the newest line, Cancel closes. The wheel and the
  scrollbar scroll it; new lines keep it pinned to the end unless the player
  scrolled up, when a "new lines" marker counts them. Input over the log
  never reaches the duel.
- Cursor grid: five rows from the far side (opponent's Spell & Trap row,
  their monsters, the player's monsters, their Spell & Traps, the hand);
  the opponent's rows read mirrored so "right" is right on screen. Moving
  between the hand and the zones maps the column proportionally. Empty zones
  are selectable and show nothing.
- Menus, the attack target list, the response prompt, the picker and the
  pile lists are one `DuelListPanel` (a titled column of buttons with a
  highlighted entry): up/down moves it, `interact` presses it, the mouse
  presses buttons directly. The highlighted entry's card and the cursor card
  glow in 3D (`CardView.SetSelected`). Tribute selection and attack targets
  are second-level lists, so every prompt has explicit options.
- Mouse: hovering a 3D card (ray on the `card` layer, `DuelStaging.Pick`)
  or a fan card moves the cursor; a click acts; a click on a pile opens its
  list. The player's 3D hand row is hidden while the fan is up
  (`DuelStaging.ShowPlayerHand`); the opponent's stays face-down in 3D.
- Tutorial (GDD §6 step 2): with `DuelUi.Tutorial` on, the first time each
  phase and each kind of prompt (menu, attack targets, picker, response,
  hand-limit discard) appears, a one-line hint from `TutorialHints`
  (Duel.Core) shows under the banner for 6 s, queued one at a time.
  Encounters turn it on while `tutorial_done` is unset.
- Input actions added to `project.godot`: `duel_phase` (Y / Space),
  `duel_graveyard` (LB / G), `duel_banished` (RB / B), `duel_log`
  (Back / L); `interact`, `cancel` and the move actions are reused.
- `tests/scenes/DuelUiTest.tscn` is the acceptance (tests/scenes/README.md):
  a scripted player reaches every command a heuristic agent picks only
  through the HUD, half of the time with the mouse.

### 6.4 VFX hooks

Events → effects: `CardMaterialise`, `CardSelected`, `SummonFlash`,
`AttackTrail(from,to)`, `HitPulse(player)`, `Dissolve(side)`. Each is a
scene under `vfx/` the artist owns; the code only instantiates and passes
anchor transforms.

Implementation (issue #64): `DuelEffects` (a child of `DuelStaging`,
`Staging.Effects`) instantiates the scenes per `vfx/README.md` (autoplay off,
side colour set, `configure(from, to)` with world transforms, `bind_card` for
the card-state effects, then `play`; `finished` is counted and the scene frees
itself) and plays the GDD §8 cues `assets/audio/sfx/duel_<cue>.wav` through
one `AudioStreamPlayer` per cue. A missing scene or cue is reported once and
the hook does nothing. Event mapping in `DuelStaging.ApplyEvent`: draw →
CardMaterialise + `draw`; Normal/Special/Flip Summon and tokens →
CardMaterialise + SummonFlash on the zone + `summon`; Set → `set`; Spell,
Trap and effect activation → CardMaterialise + `activate`; attack →
AttackTrail from the attacker's rest transform to the target's (or the
defender's chest) + `attack` + the lunge; battle or effect damage → HitPulse
at the duelist's chest + `hit` + the flinch; duel end → `win` or `lose` from
the human seat, then `DissolveAll` spawns one Dissolve per card that frees
the view on `finished`. Each card uniform has one writer: `CardView.Reveal`,
`Dissolve` and `SetSelected` spawn the scene when it exists and fall back to
their tweens otherwise; the HUD's highlight is one persistent CardSelected per
card, stopped when the selection leaves. The HUD adds the screen-edge damage
overlay (`DuelUi.DamageFlashes`, a red vignette fading over 0.5 s) whenever
the human seat loses Life Points. `DuelStagingTest` proves every scene loads
and is spawned per event, cues play per event, selection stops, finite
effects free themselves and the end dissolve frees every card.

---

## 7. Opponent AI

`HeuristicAgent : IDuelAgent`. On its priority it:

1. Enumerates `LegalActions`.
2. Rolls each one out on a cloned engine: the command is applied, then both
   sides pass until the chain is empty and no window waits for responses.
   The agent's own prompts inside the rollout take the best-scoring answer
   (branching at most twice, on prompts of at most 48 answers); the
   opponent's prompts follow the static policy below. The opponent's
   responses are not simulated: their Set cards stay Set.
3. Scores the quiet state with `Evaluate(state, profile)`:

```
score = w_board  * (ΣownValue − ΣoppValue)/1000
      + w_cards  * (ownHand + ownField − oppHand − oppField + readiness)
      + w_lp     * ((ownLP − oppLP)/1000 + 0.5 * potentialDamage/1000)
      − w_risk   * attackRisk
      + jitter   * N(0,1)
```

- A monster's value is its ATK in Attack Position, its DEF in Defense
  Position; a face-down monster counts half its DEF for its owner and a
  fixed 700 for the opponent, who cannot see it. The evaluator never reads
  the opponent's hand or face-down cards.
- `readiness` is +0.25 per Set Trap or Quick-Play Spell (it gained
  activation windows) and −0.1 per Set Normal or Equip Spell (a decoy).
- `potentialDamage` is what the monsters that have not attacked yet could
  still deal this turn.
- `attackRisk` is the ATK of the monsters that attacked this turn, scaled
  by the chance that at least one of the opponent's Set cards answers an
  attack (35 % per card). It is zero with no Set cards, so attacks are
  judged on merit; `w_risk` says how much the agent plays around the rest.

| Profile | w_board | w_cards | w_lp | w_risk | jitter | bluff_set |
| --- | --- | --- | --- | --- | --- | --- |
| Nico (aggressive) | 1.4 | 0.4 | 1.2 | 0.0 | 1.6 | 0.0 |
| Mara (balanced) | 1.0 | 1.0 | 0.8 | 0.6 | 1.2 | 0.2 |
| Arcade Owner (control) | 1.0 | 1.4 | 0.8 | 1.0 | 0.1 | 0.5 |

`data/duelists.json` carries the same numbers; `AiProfile` holds them as
presets. Jitter is the main difficulty lever: it is measured in score
points, where a card is worth about `w_cards` and 1000 ATK about
`w_board`.

**Responses.** When only `Pass` and responses are open (a chain is being
built, a window waits, or it is the opponent's turn) the agent rolls out
`Pass` and every response and compares the quiet states. A response fires
when it is in the response table and its profile-weighted score does not
drop, or when it gains about a card (1.0) regardless. The table: it saves
one of the agent's monsters (more monsters on its field than after
passing), prevents 1000 or more damage, negates a Summon (fewer monsters
on the opponent's field) or an activation (`ChainLinkNegated` in the
rollout), or wins the duel.

**Prompts.** Every `Choice` (cost, target, optional trigger, trigger order,
resolution question) is answered by rolling out each legal answer and
keeping the best score, so targets go where the evaluator likes them and
costs are paid with the cards whose loss scores least. Prompts with more
than 48 legal answers, and the opponent's prompts inside a rollout, use
the static policy (`HeuristicAgent.StaticAnswer`): optional triggers are
taken, mandatory ones in option order, costs are paid with the lowest-value
cards, targets point at the highest-value ones, and a prompt over the
opponent's cards takes as many of their best cards as it may. A card's
value is its ATK (the larger of ATK and DEF on the field), 1500 for a
Spell or Trap.

**Bluffing.** When the best action is to leave the Main Phase, with
probability `bluff_set` the agent Sets its cheapest Normal or Equip Spell
face-down instead, keeping one Spell & Trap Zone free. A Set Spell can
still be activated later.

**Harness.** `tools/duel_sim` plays seeded matchups between the duelist
profiles, the decks of `data/decks/` and a random agent, alternating sides,
and prints a win-rate table; `--check` turns the targets below into an
exit code and CI runs a two-game smoke. The targets (GDD §3.5, issue #59):
Nico with Rookie Beatdown wins at most 20 % against Mara-level play with
the starter and at least 90 % against random play; Mara and the Arcade
Owner beat random play with their own decks at least 90 % of the time; the
ladder Nico < Mara < Arcade Owner holds in mirror matches on the starter,
Warrior Toolbox and Goat Control. The rollouts see the clone's hidden
information (deck order, face-down cards) only through the evaluator,
which reads none of it; the outcome of an attack into a face-down monster
does reach the score, which is accepted for the slice.

---

## 8. Collection, shop and deck

| Type | Fields |
| --- | --- |
| `Collection` | `Dictionary<cardId,int> Owned`, `Deck` (list of ids, 40–60), `FusionDeck`, `Coins` |
| `Shop` (`data/shop.json`) | stock: list of {cardId, price}; boosters: {id, price, count, weights by tier, limitedMax} |
| `DeckRules` | validates size, copy limits from `limit`, fusion separation |

Booster draw: weighted random by tier (T1 40 %, T2 35 %, T3 20 %, T4 5 %)
with at most `limitedMax` Limited cards, from the seeded RNG so drops are
reproducible from the save's seed counter.

Rewards: `data/duelists.json` carries `reward_first`, `reward_rematch`
(coins, boosters).

Implementation (issue #62): `Collection` (BattleCity.Data) holds `Owned`,
`Deck` and `FusionDeck` as id lists; `Collection.Starter(data)` is the
`starter` deck owned and built, `ToDeck` expands it for the engine. Coins
stay on `Game`. `BoosterDraw.Open(pack, library, rng)` picks a tier by the
pack's weights and a uniform card of that tier; cards with limit 1 stop
appearing once `limited_max` are in the pack, limit 0 and tokens never
appear. The first duelist reward for a duelist is `reward_first`, every
later win `reward_rematch`.

### 8.1 Data schemas (`data/`, issue #26)

Keys are snake_case; ids match file names. `tools/validate_data.py` and the
`BattleCity.Data` loaders enforce these.

```json
// data/decks/<id>.json — card id → copies; DeckRules: 40–60 main, copies ≤ limit, Fusion only in "fusion"
{ "id": "beatdown", "name": "Beatdown", "description": "...",
  "main": { "gemini_elf": 3, "pot_of_greed": 1 }, "fusion": {} }

// data/duelists.json
{ "duelists": [ { "id": "d1", "name": "Nico", "area": "plaza", "deck": "beatdown",
    "required_flag": "", "profile": { "board": 1.2, "cards": 0.6, "life": 1.0, "risk": 0.0, "jitter": 0.6, "bluff_set": 0.0 },
    "challenge_line": "...", "win_line": "...", "lose_line": "...",
    "reward_first": { "coins": 600, "boosters": 1 }, "reward_rematch": { "coins": 200, "boosters": 1 },
    "ending": false } ] }

// data/shop.json — stock must not contain Limited cards (GDD §5.2)
{ "stock": [ { "card": "gemini_elf", "price": 100 } ],
  "boosters": [ { "id": "street_pack", "name": "Street Pack", "price": 300, "count": 5,
                  "weights": { "1": 40, "2": 35, "3": 20, "4": 5 }, "limited_max": 1 } ] }

// data/avatar.json — option lists per GDD §1.1; defaults index into them
{ "name": { "default": "Duelist", "min_length": 1, "max_length": 12, "pattern": "^[A-Za-z0-9 ]+$" },
  "body_types": ["a", "b"], "skin_tones": ["#f6e0c8", "..."], "hair_styles": { "a": ["a_short", "..."], "b": ["..."] },
  "hair_colors": ["..."], "outfits": ["street", "school", "duelist"], "accent_colors": ["..."],
  "defaults": { "body_type": "a", "skin_tone": 2, "hair_style": 0, "hair_color": 0, "outfit": 0, "accent_color": 0 } }
```

Card `effects` for tiers 3–4 name the effect id (the card id by convention)
before the class exists; the loader accepts these stubs, the validator checks
that tier 1 and tier 2 ids are implemented (`IMPLEMENTED_EFFECTS` in
`tools/validate_data.py`), and `DuelEngine.Start` rejects a deck whose
effects are missing from the `EffectRegistry`.

---

## 9. Save data

```json
{ "version": 1, "seed": 123456, "appearance": {...}, "name": "Duelist",
  "level": "district", "spawn": "arrival", "coins": 500,
  "owned": {"gemini_elf": 3}, "deck": ["..."], "fusionDeck": [],
  "defeated": ["d1"], "flags": {"tutorial_done": true} }
```

Written to `user://save.json` after every duel, purchase, deck edit and
transition. Load validates against the card database and drops unknown ids.

Implementation (issue #63): `SaveCodec` (BattleCity.Data) serialises a
`SaveData` record and parses it back against the `CardLibrary`, dropping
unknown ids into `LoadResult.Dropped`; `defeated` holds the ids of the
`defeated:<id>` flags, `flags` the rest (`SplitFlags`/`JoinFlags`). The file
also carries `rng_draws`, how far `Game.Rng` advanced from `seed`, so duel
seeds and booster drops resume in order (`DuelRng.Draws`/`Skip`). `Game.Save`
runs at the end of every transition and when an encounter that duelled
finishes; `Game.Continue` loads the slot and transitions to its level and
spawn (Boot continues when a save exists, else New Game); `Game.NewGame`
deletes it. Diagnostics point `Game.SavePath` at a test slot so runs never
touch a real save. `appearance` and `name` are stored but the creator that
fills them is later work.

---

## 10. Tuning parameters (single source: `data/tuning.json`)

| Key | Value | Used by |
| --- | --- | --- |
| player.walk_speed | 2.2 | PlayerController |
| player.run_speed | 4.5 | PlayerController |
| camera.overworld.pitch | 57 | CameraRig |
| camera.overworld.distance | 12 | CameraRig |
| camera.overworld.fov | 35 | CameraRig |
| camera.target_px_1080 | (diagnostic only) | CameraFraming test |
| camera.duel.blend_time | 1.2 | EncounterSystem |
| encounter.cone_range | 8 | Duelist |
| encounter.cone_angle | 60 | Duelist |
| encounter.stand_distance | 7 | DuelStaging |
| duel.start_lp | 8000 | Duel.Core |
| duel.hand_limit | 6 | Duel.Core |
| duel.opening_hand | 5 | Duel.Core |
| duel.card_tween | 0.15 | CardView |
| anchors.forward | 1.8 | DuelStaging |
| anchors.spacing_x | 0.92 | DuelStaging |
| anchors.spacing_z | 1.2 | DuelStaging |
| ai.jitter.* | per duelist | `data/duelists.json` `profile.jitter` (not duplicated in tuning.json) |
| shop.booster_price | 300 | `data/shop.json` `boosters[].price` (not duplicated in tuning.json) |

Implementation (issue #63): `TuningLoader` (BattleCity.Data) reads the file
into a `TuningDefinition` record (every key required, distances and times
positive, angles in range; `tools/validate_data.py` applies the same rules);
`GameData.Tuning` carries it and the Godot side reads it through the static
`Tuning.Current`, which loads the file once and falls back to
`TuningDefinition.Default` after an error. The nodes that exported these
numbers now initialise from it and the exports are gone: `PlayerController`
(`player.*`), `CameraRig` (`camera.overworld.*`; a `CameraBounds` interior
override still replaces them per level), `Duelist` (`encounter.cone_*`),
`EncounterSite` (`encounter.stand_distance`), `EncounterSystem`
(`encounter.reveal_time`, `reveal_blend`, `walk_speed`, `result_time`,
`disarm_time`), `DuelStaging` (`anchors.*`, `camera.duel.*`), `CardView`
(`duel.card_tween`), `DuelSession` (`duel.ai_delay`) and `DuelDirector`
(`duel.start_lp`, `hand_limit`, `opening_hand` into `DuelOptions`).
`camera.target_px_1080` stays a printed diagnostic of the CameraFraming
scene, not a key.

---

## 11. Answers to the art handoff

| Request | Answer |
| --- | --- |
| Sheet 01 conflicting targets | Director decision: keep the GDD camera values. Sheet 01's pixel and character-height lines withdrawn. `CameraFraming.tscn` ships as a diagnostic only. |
| Shader, renderer, budgets | Forward+; cel shading via a shared `toon.gdshader` with 3-band ramp and inverted-hull outline; budgets confirmed: ≤ 12 k tris dressed character, ≤ 1.5 k disk, ~512 texels/m, 512 px disk texture. |
| Shared skeleton, events, attachment | §3.2. Humanoid profile bone names, `LeftLowerArm` mount with per-body offset, named call-method tracks. |
| Square art in portrait window, border | §6.2 is the proposed policy. **Deferred by the director until after the Skeleton milestone.** |
| Five bays vs ten zones | §6.1. Ten anchors per side generated from the stand point, independent of the prop. Disk bays are cosmetic; deck and graveyard anchors come from disk markers. |
| Level authoring components | §3.4 and §4. Full workflow in the technical design (#7). |
| Issue #6 | Generated after this document and #7 merge. |
| District | The GDD's 120 × 120 m district with areas `plaza`, `market`, `park`, `arcade`, `edge` stands; gpt-astra's revised `district-layout.md` places anchors inside it. Encounter sites carry two stand points 7 m apart plus a clearance box. |
