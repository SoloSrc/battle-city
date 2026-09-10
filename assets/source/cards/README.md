# Card graphics — issue #30

Original SOLOSRC vector artwork, released under the repository MIT license.
No external art or font files are included. The SVGs use named Inkscape layers
(base, border, information band and type signature; badge and symbol for icons).
Open them in a vector editor to change shapes, colours or layer visibility.

Runtime PNGs live in `assets/cards/frames/` and `assets/cards/icons/`.
`frame-layout.json` retains the documented art rectangle and proposes revised lower-band coordinates;
it is a handoff reference, not an engine schema addition or art crop decision.

## Rebuild

From the repository root, with Python 3, Node and the `sharp` package available:

```sh
python3 assets/source/cards/build_cards.py
node assets/source/cards/render_cards.cjs
godot --headless --editor --import
godot --headless --script assets/source/cards/verify_import.gd
```

Set `CARDS_REPO` to build into another checkout. Set `SHARP_MODULE` to an absolute
installed module path if Sharp is not on Node's search path. There are no font
or raster references in the asset SVGs. The contact sheet uses a system font
only for review annotations; it is not a game texture.

The exporter verifies exact sizes, all 291,200 art-window pixels fully clear
per frame, and approximate 180-degree raster symmetry of the back (mean channel
error ≤0.75/255 to allow edge antialiasing). Its shapes are vector-symmetric.
`validation.json` records the exported inventory. Godot verification checks the
22 Texture2D resources and bindings to StandardMaterial3D; it cannot validate
Fable's pending hologram shader.

## Compositing — director-approved revision 03

Use the exact dimensions, icon centres and text origins in `frame-layout.json`,
now documented in systems.md §6.2. The art rectangle is `[35,40,520,560]`, and
the data panel is `[28,608,534,224]`. Frames retain transparent windows/exterior
corners, with cloud texture and inset bevelled number plates. The exporter
checks all 291,200 art-window pixels per frame.

Stars render at 32×32 centred `(68 + 34*i,656)`; attribute at 48×48 centred
`(514,656)`. This leaves 24 px padding at the left/right edges and 32 px between
the twelfth star and attribute. ATK/DEF text-box origins are `(68,726)` and
`(338,726)` with a 56 px starting font size, adjusted using engine font metrics.
Spell/trap use 104×104 badges at `(60,665)` and `(198,665)`.

Compose art, frame, icons and text into CardView before mapping to a world quad.
No stats, stars, attributes, name or rules text are baked into frames. The art
crop policy remains deferred. Equip uses a breastplate; Counter a shield
redirecting a strike. The director's supplied reference image is not committed.

See [director-approved handoff](../../../docs/requests/card-layout-director-approved.md).

## Card back — director-approved preview

Approved in chat on 2026-09-09 for PR #40. The flat dark-brown field and broad
cocoa/tan currents surround a softly integrated black elliptical opening. The
local radial transition at the centre is intentional; it is not cloud shading.
The warm-brown border and lack of event-horizon rings are retained. See
[handoff](../../../docs/requests/card-back-black-hole.md).
