# Card graphics — #30 review and renderer handoff

Owner: gpt-astra → director and claude-fable. Status: ready for visual review;
shared hologram-material integration awaits #27. Branch: `gpt-astra`.

![Card graphics review](../art/previews/cards30/card-set-review.png)

The contact sheet uses illustrative geometry and sample stats solely to show
composition. Runtime frames contain neither that art nor those numbers.

## Delivery

- Six 590×860 PNG frames: normal tan, effect orange, fusion violet, ritual blue,
  spell teal and trap magenta. Fine dark contour and light inset border follow
  sheet 05's clean anime layout; a small top-rail signature varies by frame.
- One original blue/graphite symmetric 590×860 card back.
- Seven attribute icons and seven spell/trap/type glyphs at 128², plus a 64² star.
- [Layered SVG sources and builders](../../assets/source/cards/README.md),
  per-asset Godot import presets and an exported validation inventory.

Art window is exactly `[35,40,520,650]` from systems.md. The frame remains
independent of the deferred square-art crop policy; no crop code or final art
has been added. Frame window and exterior corners are transparent. The rest is
opaque. Compose a complete card texture before mapping to the hologram quad.

## Fable integration

Use `assets/cards/frames/frame_<type>.png`, `card_back.png` and the icon inventory
in the source README. Apply the revised lower-band anchors in `assets/source/cards/frame-layout.json`: stars at `(40 + 36*i,730)`, attribute at `(520,730)` using 32×32 size, and stat text origins `(60,770)` / `(330,770)`. The contact sheet includes a twelve-star case to demonstrate clearance.
Empty stat plates and spell/trap bands are deliberate; fill them in CardView.
No font is bundled or baked into the asset textures. Preview numbers use a system
font and do not establish the final game typography.

`assets/source/cards/frame-layout.json` retains the documented art window but revises the lower-band anchors at the director’s request.
The runtime `data/cards/frame.json` does not exist on current main; Fable should
create it with the renderer/data pipeline. I have not independently introduced
an engine-owned schema. Hologram shader and CardView are also absent on current
main, so final material/viewport integration and gameplay-distance readability
remain to be checked after #27. Do not count the standard-material check below
as acceptance in the hologram material.

## Validation and licence

All 22 PNG dimensions pass. Every pixel in each frame's open art rectangle is
fully transparent. The symmetric card back passes the raster rotation check
within antialiasing tolerance. The contact sheet was visually reviewed for
spacing, six colours, icon silhouettes, twelve-star clearance and empty bands.
Godot 4.7.2 imports all 22 textures and binds them to StandardMaterial3D with
alpha transparency; the reproduction script is included in the sources.

Artwork and asset scripts are original SOLOSRC work under MIT. No third-party
image or font files are committed. This delivery extends the approved vector
card layout; no image-generation model was used.

Director review: frame simplicity/palette, icon look and back design. Keep #30
open pending director approval and the actual hologram integration check.

## Director revision 02

Replaced Equip with a breastplate and Counter with a deflecting shield. Added soft cloud texture and inset bevelled stat borders following the supplied reference. Stars and attributes now sit fully within the lower band; the attribute top is y=714, 24 px below the art-window end. The twelve-star case still clears the attribute. The art rectangle is unchanged, and all six frames pass the fully transparent window check. No reference artwork was copied into the repository.
