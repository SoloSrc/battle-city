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
in the source README. Apply the existing documented stars, attribute and stat
anchors. The contact sheet includes a twelve-star case to demonstrate clearance.
Empty stat plates and spell/trap bands are deliberate; fill them in CardView.
No font is bundled or baked into the asset textures. Preview numbers use a system
font and do not establish the final game typography.

`assets/source/cards/frame-layout.json` mirrors the documented data contract.
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
