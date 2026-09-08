# Game Design Document

**Status:** draft for director approval · **Issue:** #3 · **Author:** claude-fable
**Depends on:** [pitch.md](pitch.md), [poc-scope.md](poc-scope.md)

This document describes the mechanics of the proof-of-concept slice. Where a
thing can be data, it is written as data so the systems document and the
code can consume it directly. Tuning values are starting points.

---

## 1. Player and avatar

### 1.1 Avatar creation

On New Game the player sees a creator screen with a live 3D preview.

| Field | Options in the slice | Default |
| --- | --- | --- |
| Name | 1–12 characters, letters, digits, space | "Duelist" |
| Body type | A, B (same rig, different base mesh) | A |
| Skin tone | 6 swatches (material parameter) | 3 |
| Hair style | 4 per body type | 1 |
| Hair colour | 8 swatches | 1 |
| Outfit | 3 full sets (top, bottom, shoes as one choice) | 1 |
| Accent colour | 8 swatches, tints outfit trim and duel disk | 1 |

The avatar is one character model built from the shared rig in the style
brief. Every non-player character is built from the same parts, so the
creator doubles as the NPC authoring tool.

### 1.2 Overworld controls

Gamepad primary, keyboard fallback. Actions are Godot input actions.

| Action | Gamepad | Keyboard | Effect |
| --- | --- | --- | --- |
| `move` | Left stick | WASD / arrows | Walk; run above 60 % deflection |
| `interact` | A / Cross | E, Enter | Talk, read sign, enter door, confirm |
| `cancel` | B / Circle | Esc, Backspace | Back, close |
| `menu` | Start | Tab | Pause menu: Deck, Cards, Save, Options |
| `run_toggle` | (none) | Shift | Keyboard-only run modifier |

Movement is 8-directional analogue, camera-relative. The character turns
toward the stick direction with a short blend; there is no strafing.

| Parameter | Value |
| --- | --- |
| Walk speed | 2.2 m/s |
| Run speed | 4.5 m/s |
| Acceleration | 0.12 s to full speed |
| Turn rate | 720 °/s |
| Collision | capsule, radius 0.35 m, height 1.7 m |

### 1.3 Camera

As in style sheet 01: fixed yaw, pitch 55–60°, distance 12–14 m, follows the
player with a 0.15 s smoothing. Camera bounds are per-area volumes so the
view never leaves the built district. Interiors use a tighter distance.

---

## 2. The city district

The slice is **one district of the city**, about 64 × 64 m, built from the
modular kit and composed by the level designer. The layout and route are
specified in [district-layout.md](district-layout.md); this section names
the areas the game logic refers to.

### 2.1 Areas

| Id | Working name | Role | Notes |
| --- | --- | --- | --- |
| `arrival` | Arrival | Spawn point, orientation | Sightline to the shop |
| `shop` | Card shop | Shop exterior and interior scene | Blue awning; only enterable door |
| `site_a` | Shop corner | First duelist (Nico) | Key visual location |
| `site_b` | Garden street | Second duelist (Mara) | Green backdrop |
| `site_c` | Civic square | Final duelist (Arcade Owner) | District landmark |
| `edge` | District edges | Boundaries | Readable façades, an NPC explains the closed streets |

### 2.2 Interactables

| Type | Behaviour |
| --- | --- |
| Talk NPC | Dialogue box, 1–3 lines, may branch once |
| Sign | One-line text |
| Door | Fade to interior scene, return to the same door |
| Shop counter | Opens card shop UI (§5) |
| Duelist | Dialogue, then a duel (§3). Re-duelable after first win |
| Bed (starting room) | Save and heal is not needed; life points reset each duel |

### 2.3 Encounters

Duelists have a **line of sight cone** (8 m, 60°). Entering it triggers the
exclamation, the duelist walks to the player, one line of dialogue, then
the duel. The player may also initiate by interacting. After the player
has beaten a duelist, the duelist no longer auto-triggers but can be
challenged again for a smaller reward.

---

## 3. Duels

Duels use **Goat Format**: the TCG card pool legal on 17 August 2005, the
April 2005 Forbidden and Limited list, and 2005-era rulings as published at
[goatformat.com](https://www.goatformat.com/). The engine follows those
rules even where they contradict modern Yu-Gi-Oh.

### 3.1 Match rules in the slice

| Rule | Value |
| --- | --- |
| Format | Single duel (no best-of-three in the slice) |
| Starting life points | 8000 |
| Main deck | 40–60 cards |
| Fusion deck | 0–? cards (Fusion monsters only) |
| Side deck | Not used in the slice |
| Opening hand | 5 cards |
| First turn | Chosen by coin flip; **the first player draws** on turn 1 (2005 rule) |
| Hand size limit | 6 at End Phase, discard down |
| Win | Opponent's life points reach 0, or opponent cannot draw |
| Exodia | Not in the slice pool |
| Timer | None |

### 3.2 Turn structure

Draw Phase → Standby Phase → Main Phase 1 → Battle Phase (Start Step,
Battle Step, Damage Step, End Step) → Main Phase 2 → End Phase.

2005-era rules the engine must honour:

- **Ignition effect priority.** The turn player may activate an Ignition
  effect of a monster on summon before the opponent can respond.
- **Spell Speeds.** Speed 1: Normal spells, Ignition and Trigger effects.
  Speed 2: Quick-Play spells, Normal traps, Quick effects. Speed 3: Counter
  traps. A chain link must be equal or higher speed than the previous.
- **Damage Step.** Only Counter Traps, cards that modify ATK/DEF, and
  mandatory effects. Flip effects trigger after damage calculation.
- **Set cards** cannot be activated the turn they are set (traps and
  Quick-Play spells).
- **Tribute summon**: one tribute for levels 5–6, two for 7+.
- **Fusion** requires Polymerization or Metamorphosis; monsters go to the
  Fusion Deck.
- One Normal Summon or Set per turn.
- The player who goes first cannot attack in their first turn.

### 3.3 Duel presentation

As in style sheet 03. Both duelists stand in the overworld. The player's
zones are the ten floating cards in front of them; the opponent's mirror
them. Card positions: attack = upright, defence = rotated 90°, set = face
down. The player's hand is fanned at the bottom of the screen.

### 3.4 Duel interface

| Element | Behaviour |
| --- | --- |
| Hand | Left/right to select, up to inspect, A to act; actions listed in a small menu (Summon, Set, Activate) |
| Field | Move cursor across own and opponent zones; A on own monster: Attack, Change position, Activate; on a set card: Activate |
| Card inspector | Full name, type, attribute, level, ATK/DEF, effect text; opened from hand, field, graveyard |
| Phase bar | Current phase highlighted; press Y / Triangle to advance |
| Response prompt | When the opponent acts and the player holds a valid response, time freezes and a prompt appears: Activate / Pass |
| Chain display | Stack of activated cards drawn in order along the top edge |
| Life points | Two counters, animated on change |
| Graveyard, banished | Buttons to open a list |
| Log | Last 20 events, toggle |

Every rule prompt (target selection, cost payment, optional trigger) is a
modal with explicit options. There are no hidden clicks.

### 3.5 Opponent AI

The AI is a rule-based evaluator, not a search. Each duelist has a **deck
list** and a **play style profile** that weights the evaluator.

Per decision the AI enumerates legal actions, scores each with a heuristic
(board advantage, card advantage, damage potential, risk of walking into a
known set card), and picks the best with a small random jitter so duels are
not deterministic. Response decisions use a simpler table: activate a
response if it prevents loss of a monster or ≥ 1000 damage, or if the
profile says "aggressive".

Difficulty levers per duelist: deck quality, jitter size, whether the AI
plays around set cards, whether it bluffs sets.

### 3.6 Duelists in the slice

| Id | Name | Where | Deck | Profile | Reward first win / rematch |
| --- | --- | --- | --- | --- | --- |
| `d1` | Nico | Shop corner (`site_a`) | Beatdown (§4.2) | aggressive, high jitter, ignores set cards | 600 / 200 coins, 1 booster |
| `d2` | Mara | Garden street (`site_b`) | Warrior Toolbox (§4.3) | balanced, medium jitter, plays around one set | 900 / 300 coins, 2 boosters |
| `d3` | The Arcade Owner | Civic square (`site_c`) | Goat Control (§4.4) | control, low jitter, bluffs | 1500 / 500 coins, 3 boosters, ending |

Dialogue and losses: losing a duel returns the player to the spot with a
line from the duelist. There is no coin loss or any other penalty beyond
the time spent. `d2` is only
challengeable after beating `d1`; `d3` after beating `d2`.

---

## 4. Card pool for the slice

### 4.1 Rules for the subset

- Every card is Goat Format legal and respects the April 2005 list
  (Forbidden: 0, Limited: 1, Semi-Limited: 2 copies).
- The subset supports three complete 40-card decks plus a starting deck and
  shop stock, at **72 cards** total.
- Each card is tagged with an **implementation tier** for the systems
  document: T1 vanilla or stat-only, T2 single simple effect, T3 targeting,
  costs or continuous effects, T4 replacement effects or unusual timing.

### 4.2 Deck: Beatdown (Nico, and the player's starting deck)

Straightforward high-ATK monsters and removal. Teaches summon, attack,
tribute, and the basic spells and traps.

Monsters (20): Gemini Elf ×3, Archfiend Soldier ×3, Luster Dragon ×3,
Mad Dog of Darkness ×2, Berserk Gorilla ×2, Enraged Battle Ox ×2, Summoned
Skull ×2, Jinzo ×1, Sangan ×1, Cyber Jar ×1
Spells (14): Pot of Greed, Graceful Charity, Heavy Storm, Mystical Space
Typhoon, Premature Burial, Snatch Steal, Nobleman of Crossout ×2, Smashing
Ground ×2, Fissure ×2, Axe of Despair ×2
Traps (6): Mirror Force, Torrential Tribute, Call of the Haunted, Sakuretsu
Armor ×2, Dust Tornado

### 4.3 Deck: Warrior Toolbox (Mara)

Searchable warriors, tempo and removal. Teaches searching, flip effects,
and Quick-Play timing.

Monsters (18): Blade Knight ×2, Don Zaloog ×2, D.D. Warrior Lady ×1, D.D.
Assailant ×2, Exiled Force ×1, Mystic Swordsman LV2 ×2, Marauding Captain
×2, Command Knight ×2, Goblin Attack Force ×2, Zaborg the Thunder Monarch
×1, Mobius the Frost Monarch ×1
Spells (15): Pot of Greed, Graceful Charity, Heavy Storm, Mystical Space
Typhoon, Premature Burial, Snatch Steal, Reinforcement of the Army ×2, The
Warrior Returning Alive ×2, Book of Moon ×2, Enemy Controller ×2, Smashing
Ground ×1
Traps (7): Mirror Force, Torrential Tribute, Ring of Destruction, Call of
the Haunted, Sakuretsu Armor ×2, Bottomless Trap Hole ×1

### 4.4 Deck: Goat Control (The Arcade Owner)

The format's namesake. Scapegoat tokens, Metamorphosis into Thousand-Eyes
Restrict, Tsukuyomi loops, Chaos monsters. The final test of the engine.

Monsters (17): Black Luster Soldier - Envoy of the Beginning ×1, Chaos
Sorcerer ×1, Airknight Parshath ×1, Breaker the Magical Warrior ×1, Tribe-
Infecting Virus ×1, Sinister Serpent ×1, Magician of Faith ×2, Tsukuyomi
×2, Sangan ×1, Morphing Jar ×1, Asura Priest ×1, D.D. Warrior Lady ×1,
Exiled Force ×1, Cyber Jar ×1, Gravekeeper's Spy ×1
Spells (16): Pot of Greed, Graceful Charity, Delinquent Duo, Heavy Storm,
Mystical Space Typhoon, Premature Burial, Snatch Steal, Scapegoat ×3,
Metamorphosis ×3, Book of Moon ×2, Nobleman of Crossout ×1
Traps (7): Mirror Force, Torrential Tribute, Ring of Destruction, Call of
the Haunted, Sakuretsu Armor ×2, Dust Tornado ×1
Fusion deck: Thousand-Eyes Restrict ×3, Dark Balter the Terrible ×1, Reaper
on the Nightmare ×1

### 4.5 Full subset

Union of the three decks plus shop-only cards, with tier tags. This table is
the source for `data/cards/` in the systems document.

| Card | Type | Tier | Limit | Decks |
| --- | --- | --- | --- | --- |
| Gemini Elf | Monster | T1 | 3 | BD |
| Archfiend Soldier | Monster | T1 | 3 | BD |
| Luster Dragon | Monster | T1 | 3 | BD |
| Mad Dog of Darkness | Monster | T1 | 3 | BD |
| Berserk Gorilla | Monster | T2 | 3 | BD |
| Enraged Battle Ox | Monster | T3 | 3 | BD |
| Summoned Skull | Monster | T1 | 3 | BD |
| Jinzo | Monster | T3 | 1 | BD |
| Sangan | Monster | T2 | 1 | BD, GC |
| Cyber Jar | Monster | T4 | 1 | BD, GC |
| Blade Knight | Monster | T3 | 3 | WT |
| Don Zaloog | Monster | T3 | 3 | WT |
| D.D. Warrior Lady | Monster | T3 | 1 | WT, GC |
| D.D. Assailant | Monster | T3 | 3 | WT |
| Exiled Force | Monster | T2 | 1 | WT, GC |
| Mystic Swordsman LV2 | Monster | T3 | 3 | WT |
| Marauding Captain | Monster | T3 | 2 | WT |
| Command Knight | Monster | T3 | 3 | WT |
| Goblin Attack Force | Monster | T2 | 3 | WT |
| Zaborg the Thunder Monarch | Monster | T2 | 3 | WT |
| Mobius the Frost Monarch | Monster | T2 | 3 | WT |
| Black Luster Soldier - Envoy of the Beginning | Monster | T4 | 1 | GC |
| Chaos Sorcerer | Monster | T3 | 3 | GC |
| Airknight Parshath | Monster | T3 | 3 | GC |
| Breaker the Magical Warrior | Monster | T3 | 1 | GC |
| Tribe-Infecting Virus | Monster | T3 | 1 | GC |
| Sinister Serpent | Monster | T3 | 1 | GC |
| Magician of Faith | Monster | T2 | 3 | GC |
| Tsukuyomi | Monster | T3 | 3 | GC |
| Morphing Jar | Monster | T2 | 1 | GC |
| Asura Priest | Monster | T3 | 3 | GC |
| Gravekeeper's Spy | Monster | T2 | 3 | GC |
| Thousand-Eyes Restrict | Fusion | T4 | 3 | GC |
| Dark Balter the Terrible | Fusion | T3 | 3 | GC |
| Reaper on the Nightmare | Fusion | T3 | 3 | GC |
| Pot of Greed | Spell | T1 | 1 | all |
| Graceful Charity | Spell | T2 | 1 | all |
| Delinquent Duo | Spell | T3 | 1 | GC |
| Heavy Storm | Spell | T2 | 1 | all |
| Mystical Space Typhoon | Spell (Quick) | T2 | 1 | all |
| Premature Burial | Spell (Equip) | T3 | 1 | all |
| Snatch Steal | Spell (Equip) | T3 | 1 | all |
| Nobleman of Crossout | Spell | T3 | 2 | BD, GC |
| Smashing Ground | Spell | T2 | 3 | BD, WT |
| Fissure | Spell | T2 | 3 | BD |
| Axe of Despair | Spell (Equip) | T2 | 3 | BD |
| Reinforcement of the Army | Spell | T2 | 2 | WT |
| The Warrior Returning Alive | Spell | T2 | 3 | WT |
| Book of Moon | Spell (Quick) | T2 | 3 | WT, GC |
| Enemy Controller | Spell (Quick) | T3 | 3 | WT |
| Scapegoat | Spell (Quick) | T3 | 3 | GC |
| Metamorphosis | Spell | T3 | 3 | GC |
| Mirror Force | Trap | T2 | 1 | all |
| Torrential Tribute | Trap | T2 | 1 | all |
| Ring of Destruction | Trap | T3 | 1 | WT, GC |
| Call of the Haunted | Trap (Cont.) | T3 | 1 | all |
| Sakuretsu Armor | Trap | T2 | 3 | all |
| Dust Tornado | Trap | T2 | 3 | BD, GC |
| Bottomless Trap Hole | Trap | T3 | 3 | WT |
| Shop only: Giant Orc | Monster | T2 | 3 | shop |
| Shop only: Skilled Dark Magician | Monster | T3 | 3 | shop |
| Shop only: Kycoo the Ghost Destroyer | Monster | T3 | 3 | shop |
| Shop only: Mystic Tomato | Monster | T3 | 3 | shop |
| Shop only: Shining Angel | Monster | T3 | 3 | shop |
| Shop only: Spirit Reaper | Monster | T4 | 3 | shop |
| Shop only: Gravekeeper's Guard | Monster | T2 | 3 | shop |
| Shop only: Swords of Revealing Light | Spell | T3 | 1 | shop |
| Shop only: Creature Swap | Spell | T3 | 2 | shop |
| Shop only: Lightning Vortex | Spell | T2 | 1 | shop |
| Shop only: Waboku | Trap | T3 | 3 | shop |
| Shop only: Trap Hole | Trap | T2 | 3 | shop |
| Shop only: Widespread Ruin | Trap | T2 | 3 | shop |

Tier counts: T1 8 · T2 30 · T3 30 · T4 4. Total 72.

Card names and effect text are Konami's; this project stores them only as
game data needed to implement the rules, with no card art. See the IP
section of the pitch.

---

## 5. Cards, currency and the shop

### 5.1 Currency

**Coins**, earned from duels (§3.6). Starting balance 500.

### 5.2 Card shop

One shop on Market Street. Two ways to get cards:

| Item | Price | Contents |
| --- | --- | --- |
| Single card | 100–600 by tier and rarity | Any card in the stock table |
| Booster "Street Pack" | 300 | 5 random cards from the subset, weighted by rarity, at most one Limited card. Duel rewards are boosters, so progression is intentionally not deterministic |

Stock: every card in the subset except Limited cards, which appear only in
boosters or as duel rewards. The player's collection has no cap.

### 5.3 Deck editor

From the pause menu. Shows the collection on the left and the current deck
on the right, with counts, the Forbidden and Limited indicator per card,
and a validity check: 40–60 cards, copies within limits, Fusion deck
separate. Only one deck in the slice. An invalid deck cannot leave the
editor for a duel.

---

## 6. Progression and ending

1. New Game → avatar creation → arrival point, with the shop in view.
2. Nico waits at the shop corner and challenges the player on approach.
   This is the tutorial duel: the interface explains each phase the first
   time it appears.
3. Beat Nico → the garden street opens → Mara.
4. Beat Mara → the civic square opens → the Arcade Owner.
5. Beat the Arcade Owner → ending screen with the avatar, a line of text and
   the credits. The game returns to the district for free play.

Estimated playtime for the slice: 45–90 minutes.

---

## 7. Saving

A single autosave slot written after every duel, shop transaction, deck
edit, and area change. Save contains: avatar, position, coins, collection,
deck, defeated duelists, flags.

---

## 8. Audio

| Context | Need |
| --- | --- |
| District | One ambient loop, footstep set on stone and grass |
| Duel | One battle theme, intensity layer when a player is below 2000 LP |
| Duel SFX | draw, summon, set, flip, attack, damage, LP tick, chain link, win, lose |
| UI | move, confirm, cancel, error, purchase |
| Ending | One short theme |

---

## 9. Data files

The systems document defines schemas. The GDD commits to these data sets:

| File | Contents |
| --- | --- |
| `data/cards/*.json` | One file per card in §4.5: id, name, type, attribute, level, ATK, DEF, text, limit, tier, effect script id |
| `data/decks/*.json` | The three duelist decks and the starting deck |
| `data/duelists.json` | §3.6 table with dialogue lines and profile |
| `data/shop.json` | Stock and prices |
| `data/avatar.json` | Creator options |
| `levels/district/` | The district scene and area volumes, owned by the level designer |
