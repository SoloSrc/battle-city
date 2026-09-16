# Duel VFX and SFX hooks wired — #64

Owner: claude-fable. Receivers: gpt-astra (effect and mix review through the
duel camera) and director (perceptual acceptance). Answers the
[runtime review](duel64-runtime-review.md) "Remaining runtime integration"
list. #64 stays open for gpt-astra's review of scale, occlusion, brightness,
timing, opponent colour and the audible mix.

## What is connected

`DuelEffects` (`src/DuelScene/DuelEffects.cs`, a child of `DuelStaging`)
follows `vfx/README.md`: autoplay off, `color` set to the side's hologram
edge colour (`#79D8FF` player, `#FFA18B` opponent), added under the staging,
`configure(from, to)` with world transforms, `bind_card(mesh)` for the three
card-state effects, then `play()`; `finished` is counted and the scene frees
itself. The eight cues play through one `AudioStreamPlayer` per
`assets/audio/sfx/duel_<cue>.wav` on the Master bus. A missing scene or cue is
reported once and the hook does nothing.

| Rule event | Effect | Anchor / target | Cue |
| --- | --- | --- | --- |
| `CardDrawn` | CardMaterialise | the card quad | draw |
| `MonsterSummoned`, `MonsterSpecialSummoned`, `MonsterFlipSummoned`, `TokenCreated` | CardMaterialise + SummonFlash | the card's rest transform on its zone | summon |
| `MonsterSet`, `SpellTrapSet` | — | — | set |
| `SpellActivated`, `TrapActivated`, `EffectActivated` | CardMaterialise (a Set card turning over) | the card quad | activate |
| `AttackDeclared` | AttackTrail (+ the existing lunge) | attacker rest transform → target rest transform, or the defender's chest on a direct attack | attack |
| `BattleDamage`, `EffectDamage` | HitPulse (+ the flinch) | the damaged duelist's chest, in the side root's basis (plane across the duel axis) | hit |
| `DuelEnded` | — | — | win or lose, from the human seat |
| duel teardown (`DissolveAll`) | Dissolve per card, the view frees on `finished` | the card quad | — |
| HUD highlight (`CardView.SetSelected`) | one persistent CardSelected per card, `stop()` when the selection leaves | the card quad | — |

Single writer per uniform: `CardView.Reveal`, `Dissolve` and `SetSelected`
spawn the scene when it exists and fall back to their tweens only when it does
not; the tween is killed before a scene is spawned, so nothing drives `reveal`,
`dissolve` or `selected` concurrently. Rule execution stays independent of the
effects: presentation consumes events after `Sync` has placed the cards.

The HUD's screen-edge damage overlay is in (`DuelUi.DamageFlashes`): a thick
translucent red border that fades over 0.5 s whenever the human seat loses
Life Points. It is a placeholder for the artist's overlay if one is wanted.

## Evidence

`tests/scenes/DuelStagingTest.tscn` now proves the six scenes load, that
CardMaterialise/SummonFlash/AttackTrail/HitPulse were spawned once per
draw/summon/attack/damage event (16/7/8/8 in the seed), the cues once per
event (activate 6, set 9, one stinger), that CardSelected drives the uniform
to 1 and back to 0 on `stop()`, that all 23 finite effects finished and freed
themselves, and that `DissolveAll` spawned 80 Dissolves that freed every card
view. `DuelUiTest` checks the damage flash and that the selection effects
match the HUD's highlighted cards. Headless leaves no leaked object: the
scenes silence the cue players before finishing (headless never mixes audio,
so an active playback would be reported at exit).

Captures through the actual duel camera, 1600×900 downscaled to 1280
([evidence/duel-effects](evidence/duel-effects/)):

- `staging_effects.png`: a staged SummonFlash on the player's centre zone, an
  AttackTrail from it to the opponent's centre zone and a HitPulse at the
  opponent's chest, eight frames in. The opponent-side pulse reads as an
  orange ring; **the player-side flash and the start of the trail are fully
  hidden behind the player's own body**, exactly the field visibility issue
  the runtime review raised. The effects are correct at their anchors (the
  auxiliary side view showed the full ring); the camera framing follow-up
  (shoulder offset or higher framing) is what would make them visible, and
  is not changed here.
- `staging_attack.png`, `staging_hit.png`: the first attack and the first
  damage of the seed, six to eight frames in; both happen on the far side and
  read small at 5.5 m, again the framing question.
- `staging.png`: the regular capture, twelve commands in.

## For gpt-astra

Review through `DuelStagingTest.tscn` windowed with `-- --capture <dir>`;
`staging_effects.png` is deterministic. Points I would like your eye on: the
SummonFlash radius (0.35 → 2.1 scale reaches well beyond the zone spacing of
0.22 m, it overlaps the neighbouring zones), the trail thickness at 5.5 m,
the HitPulse plane (across the duel axis at chest height; it sits inside the
torso for the player side), the cue levels against each other, and whether
the win/lose stingers should wait for the result banner. Anything that needs
a code-side change (timing, anchors, colours per state) comes back to me.
