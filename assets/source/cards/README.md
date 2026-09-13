# Card graphics — reference revision 4

The director's 2026-09-13 `~/Downloads/cards` examples supersede revision 3.
Six SVG frame assemblies preserve supplied bevels, lower-panel textures and
stat borders at 590×860. `reference_frames.py` clears art windows and replaces
baked badge/number areas with blank strips from the same reference. The approved
card back remains unchanged. Original illustrations are present in source
references only; exported frame windows are fully transparent.

`reference_badges.py` creates SVG viewports of the supplied attribute and level
sheets, preserving their glyphs and glossy artwork. `references/attributes.png`
and `references/levels.png` are user-provided reference assets. The full card references likewise retain their supplied artwork. Their original
creator/license was not supplied; do not label these reference-derived frames or badges
as original SOLOSRC MIT artwork. No reference monster illustration is used as runtime card art.
The assembly code and six new tooltip glyphs are original SOLOSRC.

## Rebuild

```sh
python3 assets/source/cards/build_cards.py
node assets/source/cards/render_cards.cjs
node assets/source/cards/placeholders/export.cjs
node assets/source/cards/review_reference.cjs
godot --headless --editor --import
godot --headless --script assets/source/cards/verify_import.gd
```

Set `SHARP_MODULE` to the installed Sharp module if needed. SVGs contain
embedded badge-sheet data so the standard SVG exporter remains self-contained.
Python `reference_badges.py` is the editable source for these wrappers and the
new tooltip symbols. The render validator checks all 346,192 transparent
art-window pixels per frame, dimensions and unchanged back symmetry.

`frame-layout.json` revision 4 and systems §6.2 define the contract: 562×616
cover art, centered level rows with right clearance, larger reference badges,
centered stat text and a single centered Spell/Trap type badge. Subtype icons
are tooltip-only. Runtime CardView adoption remains Fable's work in #60.
See [handoff](../../../docs/requests/card-reference-frames.md).
