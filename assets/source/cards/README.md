# Card graphics — issue #30

Original SOLOSRC vector artwork, released under the repository MIT license.
No external art or font files are included. The SVGs use named Inkscape layers
(base, border, information band and type signature; badge and symbol for icons).
Open them in a vector editor to change shapes, colours or layer visibility.

Runtime PNGs live in `assets/cards/frames/` and `assets/cards/icons/`.
`frame-layout.json` mirrors the documented coordinates in systems.md §6.2;
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

The exporter verifies exact sizes, all 338,000 art-window pixels fully clear
per frame, and approximate 180-degree raster symmetry of the back (mean channel
error ≤0.2/255 to allow edge antialiasing). Its shapes are vector-symmetric.
`validation.json` records the exported inventory. Godot verification checks the
22 Texture2D resources and bindings to StandardMaterial3D; it cannot validate
Fable's pending hologram shader.

## Compositing

Frames/back are 590×860. Frame PNGs have transparent exterior corners and the
exact open art rectangle `[35,40,520,650]`; place art behind that opening.
Do not interpret a transparent frame as a complete opaque card. Compose art,
frame, icons and labels into CardView's viewport before using the hologram
material. Spell/trap have an empty lower band; monster frames have two empty
stat plates. There are no baked names, rules, stats, stars or attributes.

For preview only: stars are 24×24 at centre `(40 + 36*i, 712)` for up to 12;
attribute 44×44 centred `(520,712)`. Values from the documented ATK/DEF anchors
are top-left positions; use engine font metrics to lay out numbers in the plates.
Spell/trap badge and subtype examples use 78×78 at `(46,719)` and `(156,719)`.
These sizes are renderer proposals, not changes to the approved anchor points.

Attribute files: `attr_dark`, `attr_light`, `attr_earth`, `attr_water`,
`attr_fire`, `attr_wind`, `attr_divine` (128² PNG).
Glyph files: `st_spell`, `st_trap`, `st_equip`, `st_continuous`, `st_quick_play`,
`st_counter`, `st_field` (128² PNG). Level: `star.png` (64²).
None has baked text; glyph shapes distinguish the symbols without colour alone.

See [review handoff](../../../docs/requests/card-graphics30.md).
