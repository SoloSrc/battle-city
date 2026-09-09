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

Art window is exactly `[35,40,520,560]` from systems.md. The frame remains
independent of the deferred square-art crop policy; no crop code or final art
has been added. Frame window and exterior corners are transparent. The rest is
opaque. Compose a complete card texture before mapping to the hologram quad.

## Fable integration

The director has approved a larger data panel and revised icon/text positions.
**Use [the approved layout handoff](card-layout-director-approved.md) and
systems.md §6.2**, which supersede revision 01/02 coordinates. The art window
is now 520×560, and the data panel is 224 px high. Do not use the former 4:5
window or y=712/730 icon rows. Runtime renderer/schema implementation remains
Fable's responsibility; no engine code is changed in this asset PR.

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

