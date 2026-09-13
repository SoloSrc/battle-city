# Director reference card frames — revision 4

Owner: gpt-astra. Receiver: director and claude-fable. Status: PR review.

The director supplied six card-type examples and attribute/level sheets in
`~/Downloads/cards` on 2026-09-13, requesting their appearance and moving all
subtype badges off card faces. This supersedes the previous card layout.

![Six styles and icon inventory](../art/previews/cards30/card-set-review.png)

The six frames now use the supplied frame pixels through SVG viewports: the
bevels, palette, cloud texture and ornate stat borders come from the references.
Artwork windows are transparent. Baked numbers and badge rows are cleared with
blank background strips from the same reference, so dynamic content can be
composited without duplicate stars, attributes or numbers. Those cleared areas
are reconstructed; the full blank export is not pixel-identical to the original
populated reference card. Output is normalized to the project's 590×860 size.
The supplied star and nine main badges preserve their original artwork.

Continuous, Equip, Quick-Play, Counter, Field and Ritual get new original bronze
medallion icons with ivory symbols, for tooltips/inspectors only. Spell and Trap
faces contain only their centered main badge. Normal subtypes need no glyph.
The card back and all 79 symbolic card artworks retain their prior pixels.
The Ritual sample demonstrates a layout only; it is not a new card definition.

## Fable handoff — director-requested contract

Read systems §6.2 and `assets/source/cards/frame-layout.json` revision 4.

- Art: [14,14,562,616], centered cover; square source becomes 616² and clips
  27 pixels on each side. No stretching or letterboxing.
- Stars: 34², 36 px spacing, y center 696. Center the row where possible; shift
  long rows left using the formula in §6.2 to keep the attribute clear.
- Attribute: 64² at center (514,696).
- ATK/DEF: centers (158,781)/(430,781), 70 px italic serif starting size.
  These replace the previous top-left coordinate semantics.
- Spell/Trap: one 72² main badge at center (295,750).
- Subtypes: use the six `st_*` icons only in tooltips/inspectors. Preserve rules
  and data; this changes presentation only. The new subtype file is `st_ritual`.

No shared engine code was changed. CardView integration and tooltip hookup are
Fable's work; the preview compositor demonstrates the requested behavior.

## Source provenance and validation

Tooltip symbols and code are original SOLOSRC work. Frames, stars and main badges are reference-derived.
The attribute and level sheets were provided by the director; their original
creator/license was not included. They are identified as reference-derived
assets, not claimed as original MIT artwork. Full card examples remain under artist source references for reproducibility; their illustrations are not exported into runtime frames. Reference filenames used for visual comparison:
`level6_normal_dark.jpg`, `level2_effect_dark.jpg`, `level8_ritual_dark.jpg`,
`level9_fusion_dark.jpg`, `spell.jpg`, `trap.jpg`; the other monster examples
were also inspected for level-row placement.

Validation: 23 frame/icon/back textures import and bind in Godot 4.7.2;
all six art windows are transparent; levels 1/6/8/9/12 preserve attribute
clearance; all 79 art pixel hashes and the approved back are unchanged.
See source `validation.json` and `layout-validation.json`, and
[evidence](evidence/card-reference-frames/). Runtime acceptance awaits CardView.
