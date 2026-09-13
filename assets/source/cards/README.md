# Card graphics — revision 4

The director's reference style is implemented with isolated approved trim and
one continuous procedural cloud field across all six frames. The cloud field
uses identical coordinates and noise seed for every type, tinted to each palette.
Plate interiors brighten that same field; there are no local repair squares.
ATK/DEF text is upright serif, centered using the layout contract.

The original reference cards and sheets are not included, including as embedded
SVG copies. `trim/` contains only isolated frame/border pixels from the prior
export; `approved-badges/` contains only the approved individual icons and star.
Their reference-derived provenance remains recorded; they are not claimed as
original SOLOSRC artwork. Tooltip glyphs and generator code are original SOLOSRC.
The card back and all approved icon pixels are unchanged by this correction.

## Rebuild

```sh
python3 assets/source/cards/build_cards.py
node assets/source/cards/render_cards.cjs
node assets/source/cards/placeholders/export.cjs
node assets/source/cards/review_reference.cjs
godot --headless --editor --import
godot --headless --script assets/source/cards/verify_import.gd
```

Set `SHARP_MODULE` if Sharp is installed outside the normal module path.
`reference_frames.py` generates the continuous cloud surface with isolated trim.
`reference_badges.py` generates the six tooltip glyphs; the exporter copies
approved individual badges without resampling. No original references are
needed to rebuild. Validation covers 23 exports and transparent art windows.

See `frame-layout.json`, systems §6.2 and
[handoff](../../../docs/requests/card-reference-frames.md). Subtype icons remain
tooltip-only. CardView and tooltip integration remain Fable's responsibility.
