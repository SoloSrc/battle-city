# Interface art kit — #157

Owner: gpt-astra. Status: delivered for director review; runtime integration is
claude-fable's work in #158. Original panel and icon artwork: SOLOSRC, MIT.

![Godot component review](../../docs/requests/evidence/interface157/components-godot.png)

## Contents and usage

45 transparent PNGs, editable SVG masters in
`assets/source/ui/interface157/`, 16 StyleBoxTexture resources, two font weight
resources, and two 1920×1080 Godot review scenes. `manifest.json` records source
dimensions, equal four-edge slice margins and suggested minimum display sizes.
All source coordinates and margins are pixels at the 1080p design resolution.

| Asset | Use | Slice margin | Suggested display |
| --- | --- | --- | --- |
| `dialogue/panel` | Dialogue background | 24 | 712×242 or wider |
| `dialogue/name_tag` | Separate speaker name | 16 | 176×56 |
| `dialogue/continue` | Continue indicator | none | 20–32 square |
| `duel/lp_player`, `lp_opponent` | Named life counters | 24 | 340×128 |
| `duel/phase_idle`, `phase_active`, `phase_unavailable` | Six independent phase segments | 12 | 112×64 each |
| `duel/chain_player`, `chain_opponent` | Numbered chain chips | 16 | 200×64 |
| `duel/prompt` | Modal and response background | 24 | 560×424 |
| `duel/button_normal`, `hover`, `pressed`, `disabled` | Button states | 16 | 264×64 or wider |
| `duel/focus` | Transparent focus overlay | 12 | Match button rect |
| `glyphs/key_blank` | Live text for remapped/unknown keys | 16 | 96×64 or wider |
| Other `glyphs/` | Keyboard, mouse and positional gamepad prompts | none | 40–64 high, preserve aspect |

Load the corresponding `.tres` as a panel/button stylebox, or use the PNG in a
NinePatchRect with all four patch margins from the manifest. Do not scale a whole
panel texture: stretch only its centre and edges. Use linear filtering, lossless
compression, no mipmaps, no repeat; the committed PNG import sidecars preserve
these settings. Do not tint the entire image to indicate state.

The review scenes use NinePatchRect and live Labels. They are art specimens,
not interactive menus. They do not change the game's Theme, input handling or
layout. Fable can apply the StyleBoxTexture resources to the existing controls.

## Typography and colour

Outfit Regular 400 and Bold 700 provide a geometric sans. At 1920×1080 use
24–28 px body/action text, 36 px prompt headings and 48 px LP figures. Specimen
annotations and short phase labels use 18–22 px; long prose should not. The
28 px dialogue sample includes generous line spacing. Budget at least 24 px
content padding, and keep a speaker tag outside the text flow.

Text: ink `#293344` on pale `#F6F8FA`. Cobalt `#3565A9` supports white text in
active/pressed states. Cyan `#79D8FF` and orange `#FFA18B` are decorative side
accents; retain player names and chain numbers. Focus adds a double outline,
and the active phase adds an underline. Disabled controls still need their
runtime disabled behavior. The art does not enforce that behavior.

Use full phase names when space permits: Draw, Standby, Main 1, Battle, Main 2,
End. Phase state, chain order, LP animation and actual game text come from the
game. Avoid baking these values into the textures. Test text expansion,
populated fields, the inspector/log and the actual duel camera during #158.

## Font installation and provenance

The approved style brief forbids committing third-party fonts. This package
therefore links Outfit and installs it locally, preserving that rule while
supplying the requested regular/bold resources. Run before opening the review:

```sh
python3 assets/source/ui/interface157/install_font.py
```

This uses Python's standard library, downloads both the unchanged font and its
license, verifies both SHA-256 checksums, and writes only `assets/ui/fonts/local/`
(Git-ignored). The font resources require that installation; a fresh checkout
without it cannot load the review font. There is no silent fallback.

Source: [Outfit Project](https://github.com/Outfitio/Outfit-Fonts), through
[Google Fonts at e44c4b0](https://github.com/google/fonts/tree/e44c4b011a820c2cbe2fd2cfa8052037d7edb571/ofl/outfit).
License: [SIL OFL 1.1, with copyright notice](https://github.com/google/fonts/blob/e44c4b011a820c2cbe2fd2cfa8052037d7edb571/ofl/outfit/OFL.txt).
The font remains OFL, not MIT. Retain `local/OFL.txt` with any game distribution
that bundles it. The installer pins the revision and checksums in source.
The specimen text and key legends are rendered/outlined documents, not a
redistributed font. No third-party artwork, controller logos or card art is used.

The FontVariation resources use numeric OpenType tag `2003265652` for weight.
In the tested Godot 4.7.2 build, the string key `wght` loaded but rendered the
default 100 weight; the numeric key rendered the intended 400/700 weights.

## Input handoff

Mapping reflects `project.godot` at base `fe8c31a`:

| Action | Keyboard | Gamepad glyph |
| --- | --- | --- |
| interact | E / Enter | pad_south |
| cancel | Escape / Backspace | pad_east |
| menu | Tab | pad_start |
| run_toggle | Shift | No gamepad binding currently |
| duel_phase | Space | pad_north |
| duel_graveyard | G | pad_lb |
| duel_banished | B | pad_rb |
| duel_log | L | pad_back |
| move | WASD / arrows | pad_stick |

Face buttons show the pressed position on a four-dot cluster rather than a
platform-specific letter. Pair them with visible action text. `pad_west` is
provided for future bindings, not assigned to an existing action. Select glyphs
from the live binding/device; do not hardcode the specimen mapping. For an
unrecognised/remapped key use `key_blank` plus its live binding name. Mouse left
is available for click prompts. The keyboard move specimen shows W as an example;
the full WASD and arrow sets are supplied.

## Reproduce and inspect

To regenerate artwork, install CairoSVG 2.9.1 and fontTools in your Python
environment, install the font above, then run:

```sh
python3 assets/source/ui/interface157/build.py
```

The build script owns the exported SVGs/PNGs, manifest, StyleBoxes, font
resources and review scenes. Edit its geometry to keep regeneration consistent;
SVGs can also be edited directly for a one-off revision, but port changes back
before regenerating. PNG exports have transparent backgrounds and outlined
key legends, so runtime glyphs do not depend on font installation.

For an isolated Godot capture with no game/autoloads or .NET build required:

```sh
python3 assets/source/ui/interface157/review.py --godot /path/to/Godot
```

This copies only this kit to a temporary project, imports it, checks resource
loading, and renders both specimens through Vulkan Mobile. It saves PNG evidence
and PNG import settings back to this kit. It does not attach to a shared editor.
Requires a working display/Vulkan driver; software llvmpipe worked on this host.
For manual review after the font install/import, open
`assets/ui/review/components.tscn` or `glyphs.tscn` at 1920×1080 in Godot.

Validation and integration tasks: [delivery handoff](../../docs/requests/interface157-delivery.md).
