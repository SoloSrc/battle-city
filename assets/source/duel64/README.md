# Duel placeholder effects and sounds

Owner: gpt-astra. All original SOLOSRC work, MIT; no external samples or music.
The editable effect source is `vfx/DuelEffect.gd` and its six scene resources.
`build_sfx.py` is the editable deterministic synthesis source for eight cues.

From the repository root:

```sh
python3 assets/source/duel64/build_sfx.py
godot --headless --editor --path . --import
godot --headless --path . --script assets/source/duel64/review.gd
godot --path . --disable-render-loop --script assets/source/duel64/review.gd -- --render
```

Build the card placeholder exports first; the reviewer uses the generated
`docs/art/previews/duel64/rogue-doll-face.png` composite. It is a review fixture,
not runtime CardView. Render mode writes 24 frames to `/private/tmp/duel64-frames`
for a 12 fps contact-sheet loop. The checked-in GIF uses those engine frames.

Runtime WAVs are 48 kHz, 24-bit mono, peak normalized to -9 dBFS with onset and
release ramps. The manifest records duration, RMS level and provenance. The
listening reel is 16-bit mono for convenient preview, with 0.55 s silence
between draw, summon, set, attack, hit, activate, win, lose. These are functional
placeholder cues: final timbre, perceived balance and mixing need listening
review in the actual duel. Win/lose are temporary synthesized stingers, not
final soundtrack deliveries.

`check_geometry.gd` verifies summon-zone bounds and HitPulse ray plane throughout
animation. `capture_mix.gd` runs the real DuelStaging diagnostic and records its
Master bus to `/tmp/duel64-runtime-mix.wav` for review; run with a window, not
headless. It exits on the diagnostic summary (or after a 90-second watchdog).
See the [integrated review](../../../docs/requests/duel64-effects-review.md).
