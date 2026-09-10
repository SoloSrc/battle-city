# Asset List and Specs

**Status:** approved by the director (merged #16) · **Issue:** #6 · **Author:** claude-fable
**Sources:** [gdd.md](../design/gdd.md), [systems.md](../design/systems.md), [architecture.md](../tech/architecture.md), [direction.md](direction.md), [district-layout.md](../design/district-layout.md)

The authoritative list of every asset the slice needs. gpt-astra delivers
against it and ticks items as they land; claude-fable adds rows when a
system needs something new, through a pull request. Paths are under
`assets/` unless stated. Formats and naming follow the technical design §6.

Priority: **P0** blocks the engineering skeleton or the first playable
duel; **P1** needed for the complete slice; **P2** polish, cut if late.

Status column: `todo` → `blockout` → `review` → `final`.

---

## 0. Pipeline smoke test (P0, first delivery)

| # | Asset | Spec | Path | Status |
| --- | --- | --- | --- | --- |
| 0.1 | Metric cube | 1 m cube, `-col` collision, toon material | `kit/kit_test_cube_1m.glb` | final (smoke test) |
| 0.2 | Neutral character | Body type A, one hair, one outfit, humanoid skeleton, A-pose, ≤ 12 k tris, idle + walk clips | `characters/body/char_a_body.glb`, `characters/anims/character_anims.glb` | final (smoke test) |
| 0.3 | Duel disk blockout | Folded and deployed, `LeftLowerArm` mount origin, ≤ 1.5 k tris | `props/prop_duel_disk.glb` | final (smoke test) |

Acceptance: the three load in `tests/scenes/SmokeTest.tscn`, scale reads
correctly against the 1.7 m capsule, disk sits on the left forearm, and
the walk clip plays through the `AnimationTree`.

## 1. Direction deliverables (P0)

| # | Asset | Spec | Path | Status |
| --- | --- | --- | --- | --- |
| 1.1 | Key visual | Painted, 3840 × 2160, duel camera at site A | `docs/art/concepts/` | review (v02 merged) |
| 1.2 | Character turnaround, body A | Front, side, back, 3/4; proportions per sheet 02 | `source/characters/turnaround_a.png` | final (approved reference) |
| 1.3 | Character turnaround, body B | Same | `source/characters/turnaround_b.png` | final (approved reference) |
| 1.4 | Duel disk orthographic sheet | Top, side, front; folded and deployed; hinge; mount points | `source/props/duel_disk_ortho.png` | final (approved reference) |
| 1.5 | Card frame set | Six frames (normal, effect, fusion, ritual, spell, trap), 590 × 860 PNG + layered source; window per `frame.json` | `cards/frames/frame_<type>.png` | review (#30; shader pending) |
| 1.6 | Card back | Symmetric, one accent colour, 590 × 860 | `cards/frames/card_back.png` | review (#30; shader pending) |
| 1.7 | Attribute icons | 7 originals (DARK, LIGHT, EARTH, WATER, FIRE, WIND, DIVINE), 128² PNG with alpha | `cards/icons/attr_<name>.png` | review (#30; shader pending) |
| 1.8 | Spell/trap glyphs | Spell, trap, equip, continuous, quick-play, counter, field; 128² | `cards/icons/st_<name>.png` | review (#30; shader pending) |
| 1.9 | Level star | 64² | `cards/icons/star.png` | review (#30; shader pending) |

## 2. Characters (P0 unless noted)

Shared rig, one `.glb` per part, skinned to the rig, ≤ 12 k tris dressed,
~512 texels/m, toon material with `skin_tint` and `accent_tint` parameters.

| # | Asset | Count | Spec | Path | Status |
| --- | --- | --- | --- | --- | --- |
| 2.1 | Body meshes | 2 | Types A and B, same skeleton | `characters/body/char_<a|b>_body.glb` | todo |
| 2.2 | Hair styles | 8 | 4 per body type, front/back split, hair colour via tint | `characters/hair/char_<a|b>_hair_<01-04>.glb` | todo |
| 2.3 | Outfit sets | 6 | 3 per body type; each set = top + bottom + shoes meshes; accent trim masked | `characters/outfits/char_<a|b>_outfit_<01-03>_<top|bottom|shoes>.glb` | todo |
| 2.4 | Accessories (P1) | 3 | Hat, glasses, headband; fit both bodies | `characters/acc/char_acc_<name>.glb` | todo |
| 2.5 | Skin tone ramp | 1 | 6 swatches as shader constants, documented | `characters/skin_tones.json` | todo |
| 2.6 | Face textures | 2 | Eyes and mouth per body, atlas with blink and talk frames | `characters/body/char_<a|b>_face.png` | todo |
| 2.7 | Duelist looks | 3 | Nico, Mara, Arcade Owner as `CharacterAppearance` data using the parts above; the Arcade Owner may have one unique accessory | `data/duelists.json` (appearance block) | todo |
| 2.8 | NPC looks | 5 | Market ×2, edge ×1, plaza ×2 from parts | `data/npcs.json` | todo |

### 2.9 Animations (P0)

One `character_anims.glb` with the rig and all clips, 30 fps, root motion
off, call-method events per systems §3.2.

| Clip | Length | Events | Status |
| --- | --- | --- | --- |
| idle | 4 s loop | | todo |
| walk | 1 s loop | footstep_l, footstep_r | todo |
| run | 0.7 s loop | footstep_l, footstep_r | todo |
| turn_l, turn_r | 0.3 s | | todo |
| talk | 3 s loop | | todo |
| duel_ready | 1.2 s | disk_deploy at 0.5 s | todo |
| draw_card | 0.8 s | card_draw at 0.4 s | todo |
| play_card | 0.9 s | card_release at 0.5 s | todo |
| card_to_grave | 0.6 s | card_to_grave at 0.3 s | todo |
| take_damage | 0.7 s | hit at 0.1 s | todo |
| win | 2.5 s | | todo |
| lose | 2.5 s | | todo |
| duel_idle | 4 s loop | | todo |

## 3. Duel disk (P0)

| # | Asset | Spec | Path | Status |
| --- | --- | --- | --- | --- |
| 3.1 | Disk mesh | Folded and deployed as two poses of one rig, hinge bone; ≤ 1.5 k tris; one 512² texture; accent mask; markers `deck`, `graveyard`, `banished`, `bay_1..5` | `props/prop_duel_disk.glb` | todo |
| 3.2 | Disk animations | `disk_deploy` 0.6 s, `disk_fold` 0.6 s, `card_insert_deck`, `card_insert_grave` | in 3.1 | todo |
| 3.3 | Life counter display | 128 × 48 emissive texture region, digits rendered by code | in 3.1 texture | todo |

## 4. Cards (P0 frames, P1 art)

| # | Asset | Count | Spec | Path | Status |
| --- | --- | --- | --- | --- | --- |
| 4.1 | Frames, back, icons | see §1 | | | |
| 4.2 | Placeholder art | 72 | 512² flat-colour or silhouette per card, original, no third-party art | `cards/art/<card_id>.png` | todo |
| 4.3 | Final art (P2) | 72 | Original illustrations, 512², central 80 % safe column | same | todo |
| 4.4 | Hologram material | 1 | Emissive edge, hover bob, additive glow, per-side colour | `shaders/hologram.gdshader` (claude-fable) with artist parameters | todo |

## 5. Environment kit (P0 greybox, P1 dressed)

Grid 1 m, detail 0.25 m, origin at bounds-min corner, `-col` collision,
toon materials, atlas ≤ 2048². Sized for a 1.7 m character and 7 m
two-storey shop.

| # | Piece | Variants | Spec | Path | Status |
| --- | --- | --- | --- | --- | --- |
| 5.1 | Ground tiles | street, pavement, plaza stone, grass, path | 2 × 2 m tiles + 1 m edge strips | `kit/kit_ground_<name>.glb` | review (#32 greybox) |
| 5.2 | Kerbs | straight 2 m, corner in, corner out, ramp | | `kit/kit_kerb_<name>.glb` | review (#32 greybox) |
| 5.3 | Walls | 2 m plain, window, door, shop front, corner | 3.5 m storey height | `kit/kit_wall_<name>.glb` | review (#32 greybox) |
| 5.4 | Roofs | flat, pitched 2 m segment, pitched end, ridge, in warm and green | | `kit/kit_roof_<name>.glb` | review (#32 greybox) |
| 5.5 | Shop façade set | Blue awning, sign, door, window display | | `kit/kit_shop_<name>.glb` | review (#32 greybox) |
| 5.6 | Arcade façade set | Sign with lights, marquee, closed doors | | `kit/kit_arcade_<name>.glb` | review (#32 greybox) |
| 5.7 | Boundaries | Hedge 2 m, fence 2 m, construction barrier, river edge 2 m, river water plane | | `kit/kit_bound_<name>.glb` | review (#32 greybox) |
| 5.8 | Props | Bench, lamp post, planter (2 sizes), tree (2), bush, fountain, sign post, bin, crate | | `props/prop_<name>.glb` | review (#32 greybox) |
| 5.9 | Interiors | Shop: counter, shelves, card display, floor, walls; starting room: bed, desk, door | | `kit/kit_int_<name>.glb` | review (#32 greybox) |
| 5.10 | Sky and lighting | Sky gradient texture, one sun direction, environment resource | `env/` | todo |

Level scenes themselves (`levels/district/District.tscn` and interiors) are
composed by gpt-astra and are not assets on this list.

## 6. VFX (P1)

Scenes under `assets/vfx/`, instantiated by code with anchor transforms.

| # | Effect | Trigger | Spec | Status |
| --- | --- | --- | --- | --- |
| 6.1 | Disk deploy pulse | `disk_deploy` | Ring from the disk, 0.4 s | todo |
| 6.2 | Card materialise | card reaches anchor | Fade + scanline, 0.3 s | todo |
| 6.3 | Card selected | cursor on card | Edge brighten | todo |
| 6.4 | Summon flash | monster summoned | 0.5 s burst at anchor | todo |
| 6.5 | Attack trail | attack declared | Streak from attacker to target, 0.4 s | todo |
| 6.6 | Hit pulse | damage dealt | Screen-edge and character pulse | todo |
| 6.7 | End dissolve | duel result | Loser's cards dissolve, 1 s | todo |
| 6.8 | Exclamation | encounter | Overhead "!" sprite, 0.6 s | todo |
| 6.9 | Gate open | progression flag | Barrier retracts, dust | todo |

## 7. UI (P0 for duel HUD, P1 elsewhere)

Vector or layered sources plus PNG exports; 9-slice where noted.

| # | Asset | Spec | Path | Status |
| --- | --- | --- | --- | --- |
| 7.1 | Font | One geometric sans (open licence), regular and bold | `ui/fonts/` | todo |
| 7.2 | Dialogue box | 9-slice panel, name tag, continue arrow | `ui/dialogue/` | todo |
| 7.3 | Duel HUD | LP counter panel, phase bar with 6 phases, chain link chip, prompt modal panel, button prompts (gamepad + keyboard glyphs) | `ui/duel/` | todo |
| 7.4 | Card inspector | Panel layout with art window, text area | `ui/duel/` | todo |
| 7.5 | Menus | Main menu, pause menu, options; title treatment | `ui/menu/` | todo |
| 7.6 | Avatar creator | Option chips, swatches, arrows | `ui/creator/` | todo |
| 7.7 | Shop and deck editor | List rows, counters, validity indicators, booster pack icon | `ui/shop/` | todo |
| 7.8 | Ending card | Full-screen frame for the avatar and credits text | `ui/ending/` | todo |
| 7.9 | Cursor and selection ring | 3D selection ring for anchors | `ui/duel/` | todo |

## 8. Audio (P1)

48 kHz sources; music Ogg Vorbis q6 with loop points; SFX 24-bit mono WAV.

| # | Cue | Spec | Path | Status |
| --- | --- | --- | --- | --- |
| 8.1 | District theme | 96–108 BPM, 60–90 s loop | `audio/music/district.ogg` | todo |
| 8.2 | Shop theme (P2) | 80–96 BPM, 45–60 s loop | `audio/music/shop.ogg` | todo |
| 8.3 | Duel theme, base | 128–140 BPM, 60–90 s loop + intro | `audio/music/duel_base.ogg`, `duel_intro.ogg` | todo |
| 8.4 | Duel theme, intensity stem | Same length and tempo, mixed in below 2000 LP | `audio/music/duel_intense.ogg` | todo |
| 8.5 | Final duel variation (P2) | Arrangement variant of 8.3 | `audio/music/duel_final.ogg` | todo |
| 8.6 | Win / lose stingers | 3–5 s / 2–4 s | `audio/music/win.ogg`, `lose.ogg` | todo |
| 8.7 | Ending theme | 30–60 s | `audio/music/ending.ogg` | todo |
| 8.8 | Ambience | District loop, park loop, interior room tone | `audio/ambience/` | todo |
| 8.9 | Footsteps | 3 variants × stone, grass, wood | `audio/sfx/step_<surface>_<n>.wav` | todo |
| 8.10 | Duel SFX | draw, summon, set, flip, attack, hit, lp_tick, chain_link, deploy, fold, card_insert, dissolve | `audio/sfx/duel_<name>.wav` | todo |
| 8.11 | UI SFX | move, confirm, cancel, invalid, purchase, open, close | `audio/sfx/ui_<name>.wav` | todo |
| 8.12 | World SFX | door, exclamation, gate open, fountain loop | `audio/sfx/world_<name>.wav` | todo |

---

## Counts and priority summary

| Group | P0 | P1 | P2 |
| --- | --- | --- | --- |
| Smoke test and direction | 12 | 0 | 0 |
| Characters and animations | 32 | 3 | 0 |
| Duel disk | 3 | 0 | 0 |
| Cards | 15 | 72 placeholders | 72 final art |
| Environment kit | ~40 pieces greybox | dressed | |
| VFX | 0 | 9 | 0 |
| UI | 3 | 6 | 0 |
| Audio | 0 | ~50 files | 2 tracks |

## Delivery order

1. §0 smoke test, then §1 turnarounds and disk ortho.
2. §1 card frames, icons, back; §2 body A with one outfit and the full
   animation set; §3 disk final.
3. §5 greybox kit so the district can be composed in parallel with 4.
4. §2 remaining bodies, hair, outfits; §7 duel HUD; §4 placeholder art.
5. §5 dressed kit, §6 VFX, §8 audio, remaining UI.
6. P2 items.

Every delivery lists its source file under `assets/source/` and its
licence in the pull request body, per the technical design §10.
