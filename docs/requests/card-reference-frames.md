# Generated card frames — director-approved revision 5

Owner: gpt-astra. Receiver: claude-fable and director. Status: PR #73 review;
CardView integration remains Fable's responsibility.

The director approved the generated design on 2026-09-13 and asked to commit its
artwork for future visual consistency checks. These are generated review images,
not the supplied third-party reference files. The final authority and supporting
studies are in [the reference archive](../art/references/card-design/README.md).

![Current exported six-style review](../art/previews/cards30/card-set-review.png)

## Fable handoff — agreed with the director

Adopt `assets/source/cards/frame-layout.json` revision 5 and systems §6.2.

- Retain 590×860 exports. Art window [10,10,570,610], centered cover.
- Matched 10px gray/colored bevels; lower panel [0,630,590,230].
- Soft muted continuous cloud texture across all six types. Gold plates have
  translucent interiors (35% fill for monster stats; 48% for Spell/Trap). Old cropped trim and its ghost edges are removed.
- Star size 34², step 37, center y=686; center ordinary rows and shift long rows
  left per the contract. Level 12 has 26px left margin and 24px attribute gap.
- Attribute 66² at (524,686). Stars and attribute are vertically aligned.
- Lowered ATK/DEF plates; centers (156,785)/(434,785), 80px upright serif.
- Spell/Trap: plate [34,693,522,104] and 72² badge centered at (295,745).
  Equal 63px top/bottom margins from the panel bounds to the rectangle path.
- Six subtype icons belong only in tooltips/inspectors, never card faces.

Approved icons remain byte-identical. The existing back and 79 gameplay art PNGs
remain unchanged. Generated illustration references are not new game assets or
card definitions. Runtime CardView and tooltip hookup are not changed here.

Rebuild/validation instructions are in the source README. PNG review composition
uses the shared layout contract. Verify in CardView before closing integration:
all six frame types, levels 1 and 12, equal bevel widths, attribute/star alignment,
stat legibility, centered Spell/Trap plates, and no art-window letterboxing.

## Provenance

Generated source/reference imagery is recorded in the archive README. Frame
geometry and tooltip glyph code are original SOLOSRC. Approved main badges and
stars remain reference-derived from director inputs whose original license was
not supplied; they are not claimed as original MIT artwork. Supplied full card
examples and sheets are absent from the proposed tree. No outside reference
files are required to rebuild. See validation evidence in
[evidence/card-reference-frames](evidence/card-reference-frames/).
