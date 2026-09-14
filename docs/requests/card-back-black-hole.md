# Card back: flat brown field and dark ellipse

Owner: gpt-astra. Status: director-requested replacement, 2026-09-13; PR review.

Supersedes the 2026-09-09 swirl design from PR #40. The director supplied a
735×1072 image and requested this appearance exactly. The editable SVG
reconstructs its simple shapes and sampled dominant colors at the existing
590×860 runtime resolution:

- Full tan outer field `#b7885a`.
- Brown inset `#563121`, at [21,21,548,818], corner radius 6; thin 2px
  outline `#0b0000`.
- Upright dark ellipse `#1d1d1d`, center (295,430), radii (112,212), 2px black outline.

No swirl, gradient, glow or cloud texture. Source image antialiasing and minor
compression colors are not replicated; the flat shapes, palette and relative
proportions are preserved. The original supplied attachment is not committed.

![Current back](../../assets/cards/frames/card_back.png)

Runtime path remains `assets/cards/frames/card_back.png`; editable source is
`assets/source/cards/card_back.svg`, reproduced by `build_cards.py` and
`render_cards.cjs`. Fable needs no code, UV, front-layout or import-path changes.
All fronts, icons and gameplay illustration PNGs are unchanged.

Validation: 590×860 export, rotational symmetry, front-window/export validation
and visual comparison with the supplied attachment. This is an editable vector
reconstruction of director-provided artwork; attachment creator/license was not
specified. Generator code remains MIT.
