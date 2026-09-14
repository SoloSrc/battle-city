# Card graphics — revision 5

Director-approved generated design references are committed under
[card-design](../../../docs/art/references/card-design/README.md). The final
layout sheet is the visual authority. Runtime textures remain 590×860; the
lower panel is 230px high (26.7%), matching the approved shortened-panel preview.

`reference_frames.py` builds original deterministic 10px mitered bevels and
translucent, gold-edged stat/type plates. It tints `cloud-mottle.png`, a grayscale
texture produced by OpenAI image generation from the approved cloud appearance.
Monster plate fill opacity is 30%, preserving visible cloud detail beneath the
upright numbers; Spell/Trap plate fill stays at 48%. No old cropped trim remains. All six frames share the same continuous texture.

The generated source and reference sheets are SOLOSRC project art made with AI
assistance; source code is MIT. Approved individual main badges and stars remain
reference-derived, not claimed as original SOLOSRC imagery. Full supplied cards
and sheets are not included. Six tooltip glyphs are original SOLOSRC code/art.
The existing back and all 79 gameplay artworks are preserved.

## Rebuild

```sh
python3 assets/source/cards/build_cards.py
node assets/source/cards/render_cards.cjs
node assets/source/cards/placeholders/export.cjs
node assets/source/cards/review_reference.cjs
godot --headless --editor --import
godot --headless --script assets/source/cards/verify_import.gd
```

Set `SHARP_MODULE` to the installed Sharp module when needed. No image generation
or external reference files are needed for rebuilds. `compose_face.cjs` shares
layout revision 5 between PNG review exports. The HTML preview follows it too.
Validation covers 23 exports, transparent windows, levels 1–12, attribute gaps,
and exact Spell/Trap centering. CardView/tooltip implementation is Fable's work.
See `frame-layout.json`, systems §6.2 and the handoff.
