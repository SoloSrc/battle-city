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
| pitch | 57° | 50° | 12–18° (per site) |
| distance | 12 m (GDD baseline 12–14) | 7 m | 5.5 m from player |
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
2. Freeze player input; exclamation; duelist walks to the nearer stand
   point of the closest `EncounterSite` (or 3.5 m from its own position if
   no site is within 10 m, snapped to the navmesh). The cone is disarmed
   for 3 s after a duel ends so a loss cannot retrigger while the player
   is still inside it, and stays disarmed after the first victory.
3. Player is walked to the opposite stand point by the controller.
4. Dialogue line. Both play `duel_ready`; on `disk_deploy` the `DuelStaging`
   spawns anchors and the camera blends to the duel camera over 1.2 s.
5. `Game.Mode = Duel`, engine starts.
6. On result: `win`/`lose` clips, cards dissolve, camera blends back,
   duelist state updated, rewards granted, autosave.

Meeting positions that fail a capsule sweep are resolved by searching
outward along the site axis in 0.5 m steps for up to 3 m. No teleport to
another location, ever.

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
  Flags: NormalSummonUsed, BattlePhaseUsed, Attacker/AttackTarget, FirstTurn
}
record PlayerState {
  int LifePoints;
  Zone Deck, Hand, Graveyard, Banished, FusionDeck;
  Zone[] MonsterZones (5), SpellTrapZones (5);
  Zone FieldZone;             // present, unused in the slice
}
record CardInstance {
  Guid Id; CardDefinition Def; int Owner; int Controller;
  Location Loc; int ZoneIndex;
  Position Pos;               // FaceUpAttack, FaceUpDefense, FaceDownDefense, FaceDown (S/T), FaceUp (S/T)
  int AtkMod, DefMod; List<Modifier> Modifiers;
  bool SetThisTurn; bool ChangedPositionThisTurn; bool AttackedThisTurn;
  Dictionary<string,int> Counters;
}
```

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
  EffectKind Kind;            // Ignition, Trigger, Quick, Continuous, Flip, Activation (spell/trap), Condition
  SpellSpeed Speed;           // 1, 2, 3
  bool IsMandatory;
  TriggerWindow? Trigger;     // OnSummon, OnDestroyedByBattle, OnFlip, OnSentToGrave, Standby, EndPhase
  bool UsableInDamageStep;    // speed 2 ATK/DEF modifiers may activate before damage calculation
  bool CanActivate(DuelState s, CardInstance src, ActivationContext ctx);  // card-specific condition only
  IReadOnlyList<Choice> Costs(DuelState s, CardInstance src);    // asked in order at activation
  IReadOnlyList<Choice> Targets(DuelState s, CardInstance src);  // asked after the costs are paid
  void PayCosts(DuelEngine e, ChainLink link);   // once, before targets; answers in link.Costs
  void Resolve(DuelEngine e, ChainLink link);    // skipped for a negated link; answers in link.Targets
}
```

`EffectBase` supplies defaults for everything an effect does not use. The
engine owns the timing every effect shares (speed, priority, Set turn,
Damage Step limits); `CanActivate` holds only the card's own condition and
reads the window it needs from `DuelState.Window`, the chain, or the
`ActivationContext` (the trigger window and, for graveyard triggers, the
location the card came from).

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

Continuous effects (Jinzo, Command Knight, equips) register `Modifier`s
that the rules query when computing ATK/DEF, activation legality and
targeting. Modifiers are recomputed after every state change.

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
| Position changes | Once per turn, not on the turn summoned, not after attacking |
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
(`AttackCancelled`); there is no replay.

### 5.6 Implementation tiers → build order

| Tier | What it needs | Cards (from GDD §4.5) |
| --- | --- | --- |
| T1 | Vanilla monsters, Pot of Greed | 8 |
| T2 | Single simple effect: draw/discard, destroy, search, flip-reveal, stat equip | 30 |
| T3 | Targets, costs, continuous modifiers, quick timing, tokens, position control | 30 |
| T4 | Replacement and unusual effects: Cyber Jar, BLS, Thousand-Eyes Restrict, Spirit Reaper | 4 |

Build the engine T1 → T4; each tier has a test suite. The Beatdown duel is
playable at T2, Warrior Toolbox at T3, Goat Control at T4.

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
   1.0 m in front of the duelist's chest, 0.22 m apart horizontally, rows
   0.28 m apart, monsters in front, spells and traps behind. Anchors are
   generated from the stand point and axis, **never from the disk mesh**,
   so bay spacing on the prop is cosmetic. Deck, graveyard and banished
   anchors sit on the disk's mount points via `DuelDisk` markers.
3. Positions the duel camera from the site (§4.2).

Cards are `CardView` instances: a quad with the frame texture, art
texture, stars, attribute and stat labels rendered as a `SubViewport`
texture at 590 × 860, plus an emissive edge material. Attack = upright,
Defence = rotated 90° about Y, face-down = flipped, with a 0.15 s tween.

### 6.2 Card face layout and art window

**Director-approved reference change (2026-09-13):** follow the six frame
styles and badge sheets supplied in `~/Downloads/cards`. This supersedes the
layout in PR #39 and the previous subtype placement on Spell/Trap faces.
The card remains 590×860; source monster art remains 512×512.

The layout contract is `assets/source/cards/frame-layout.json`, revision 4:

```json
{
  "revision": 4,
  "size": [
    590,
    860
  ],
  "art_window": [
    14,
    14,
    562,
    616
  ],
  "data_panel": [
    0,
    645,
    590,
    215
  ],
  "star_size": [
    34,
    34
  ],
  "star_step": 36,
  "star_row_y": 696,
  "star_row_center_x": 295,
  "star_row_right_max": 460,
  "attribute": [
    514,
    696
  ],
  "attribute_size": [
    64,
    64
  ],
  "atk": [
    158,
    781
  ],
  "def": [
    430,
    781
  ],
  "stat_alignment": "center",
  "stat_font_px": 70,
  "stat_plates": [
    [
      46,
      735,
      224,
      92
    ],
    [
      318,
      735,
      224,
      92
    ]
  ],
  "spell_trap_badge": [
    295,
    750
  ],
  "spell_trap_badge_size": [
    72,
    72
  ],
  "subtype_placement": "tooltip_only"
}
```

The art window is [14,14,562,616], with a thin beveled border. Centered cover
scales square art to 616² and clips 27 pixels from each side, without stretching.
The visible source region is approximately [22.44,0,467.12,512]. Keep essential
art inside this safe region. The lower panel begins at y=645.

For a level L, star row width is `(L-1)*36+34`. Center the row at
`min(295,460-width/2)`; its left edge is that center minus half the width.
Stars are 34² at top y=679. This centers ordinary levels and shifts long rows
left to preserve clearance from the 64² attribute centered at (514,696).
ATK/DEF are centered inside their plates, using a 70 px italic serif starting
size; use font metrics to vertically center rather than assuming a baseline.
The coordinate fields `atk`/`def` now denote centers, not top-left origins.

Spell/Trap faces contain one 72² main type badge centered at (295,750),
inside a single ornate plate. **Do not draw subtype badges on card faces.**
Continuous, Equip, Quick-Play, Counter, Field and Ritual subtype icons are
for tooltips/inspectors only. Normal subtypes need no extra glyph. This is a
presentation change; card data and subtype rules are unchanged.

Fable must apply this contract to CardView and tooltips when #60 lands.
See [reference frame handoff](../requests/card-reference-frames.md) for preview,
asset provenance and integration details.

### 6.3 HUD and input

`DuelUi` (CanvasLayer): hand fan, phase bar, LP counters, chain display,
prompt modals from `Choice`, inspector, graveyard list, log. Selection is a
cursor over hand cards and 3D anchors; the 3D cards carry colliders on
the `card` layer for mouse picking. Gamepad moves the cursor by grid.

### 6.4 VFX hooks

Events → effects: `CardMaterialise`, `CardSelected`, `SummonFlash`,
`AttackTrail(from,to)`, `HitPulse(player)`, `Dissolve(side)`. Each is a
scene under `vfx/` the artist owns; the code only instantiates and passes
anchor transforms.

---

## 7. Opponent AI

`HeuristicAgent : IDuelAgent`. On its priority it:

1. Enumerates `LegalActions`.
2. Simulates each one step on a cloned state (no lookahead beyond
   immediate resolution; opponent responses are not simulated).
3. Scores with `Evaluate(state, profile)`:

```
score = w_board  * (ΣownATK − ΣoppATK)/1000
      + w_cards  * (ownHand + ownField − oppHand − oppField)
      + w_lp     * (ownLP − oppLP)/1000
      − w_risk   * (setCardsOpp * exposure)
      + jitter   * N(0,1)
```

| Profile | w_board | w_cards | w_lp | w_risk | jitter | bluff_set |
| --- | --- | --- | --- | --- | --- | --- |
| Nico (aggressive) | 1.2 | 0.6 | 1.0 | 0.0 | 0.6 | 0.0 |
| Mara (balanced) | 1.0 | 1.0 | 0.8 | 0.6 | 0.3 | 0.2 |
| Arcade Owner (control) | 0.8 | 1.4 | 0.5 | 1.0 | 0.1 | 0.5 |

Responses: activate a legal response if it saves a monster, prevents ≥1000
damage, or negates a summon/activation, weighted by profile. `bluff_set`
is the chance to set a non-trap card face-down as a decoy.

`Choice` answering: targets chosen by the same evaluator; costs paid with
the lowest-value cards.

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

Card `effects` for tiers 2–4 name the effect id (the card id by convention)
before the class exists; the loader accepts these stubs, the validator checks
that tier 1 ids are implemented, and `DuelEngine.Start` rejects a deck whose
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
| anchors.forward | 1.0 | DuelStaging |
| anchors.spacing_x | 0.22 | DuelStaging |
| anchors.spacing_z | 0.28 | DuelStaging |
| ai.jitter.* | per profile | HeuristicAgent |
| shop.booster_price | 300 | Shop |

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
