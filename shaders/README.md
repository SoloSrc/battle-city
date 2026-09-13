# shaders (owner: claude-fable)

Forward+ only (project setting `rendering_method=forward_plus`). Three
shaders and four materials, issue #27. gpt-astra tunes the numbers on the
`.tres` files in `materials/`; the `.gdshader` defaults are the first-pass
values and a fallback when a material does not override them.

| File | What it is | Used by |
| --- | --- | --- |
| `toon.gdshader` | Cel shading: 3-band ramp, tinted shadow, light-side rim, small cel highlight | every `toon_*` material |
| `outline.gdshader` | Inverted-hull contour, second pass of the toon material | `materials/toon.tres` `next_pass` |
| `hologram.gdshader` | Floating card: readable face, emissive edge, scanlines, flicker, hover bob, materialise / dissolve | duel card anchors |
| `debug_grid.gdshader` | World-space metre grid for the diagnostic scenes | `tests/scenes/*` |
| `materials/toon.tres` | The shared toon material (`next_pass` = `outline.tres`) | `ToonMaterials.Apply` |
| `materials/outline.tres` | The shared outline pass | via `toon.tres` |
| `materials/hologram_player.tres` | Hologram with the player edge colour `#79D8FF` | `HologramCards.Create(Player, …)` |
| `materials/hologram_opponent.tres` | Hologram with the opponent edge colour `#FFA18B` | `HologramCards.Create(Opponent, …)` |

## How the toon material reaches meshes

Exported glTF materials named `toon_*` stay standard PBR in the imported
scene (Godot's importer never sees the shader). At load,
`src/Rendering/ToonMaterials.cs` walks the instantiated body, prop or level and
replaces every `toon_*` surface with a copy of `materials/toon.tres` carrying
that material's base colour in `albedo` and its texture in `albedo_texture`.
Every other parameter and the outline pass are shared: change `toon.tres` and
every character, prop and kit piece changes. `Character` applies it to the
body, `DuelDisk` to the prop, `Game` to each level as it loads, and the
diagnostic scenes to what they place. Materials named otherwise are left as
imported for review. This replaces the import-script plan in architecture.md
§6.4; nothing in `assets/` needs to change.

Skin tone and accent colour: the copy made from `toon_skin` carries the skin
colour in `albedo`, the copy from `toon_accent` the accent colour. The avatar
creator will set those two `albedo` values (`CharacterAppearance`, later
issue); no per-colour textures.

## toon.gdshader parameters

| Uniform | Default | Meaning |
| --- | --- | --- |
| `albedo` | white | Base colour, multiplied with the texture. Set per material from the glTF colour. |
| `albedo_texture` | white | Optional albedo texture (the disk's `prop_duel_disk_albedo.png`). |
| `bands` | 3 | Light bands from shadow to lit: 2 = flat two-tone, 3 = the brief, up to 5. |
| `band_softness` | 0.03 | Blend width between bands; 0 is a hard edge. |
| `lit_threshold` | 0.55 | n·l at which the fully lit band starts; lower = more of the surface lit. |
| `shade_floor` | 0.38 | Brightness of the darkest band relative to the lit one. |
| `shade_tint` | `#B3BDE6` | Colour kept in shadow (direction.md "retain colour in shadow"). Cool by default. |
| `rim_strength` | 0.35 | Thin lift along the silhouette on the lit side; 0 disables. |
| `rim_width` | 0.22 | Rim band width (fraction of the view-facing range). |
| `rim_color` | warm white | Rim colour. |
| `specular_strength` | 0.05 | Cel highlight; keep low, the brief rejects glossy plastic. |
| `specular_size` | 0.03 | Highlight size. |

Lighting note: the lit band equals a Lambert surface under the same sun, so
existing scene light energies (1.2 in the diagnostics) still read right.
Ambient comes from the environment as usual. Shadows from the sun fall into
the darkest band through `ATTENUATION`.

## outline.gdshader parameters

| Uniform | Default | Meaning |
| --- | --- | --- |
| `outline_color` | `#293344` | direction.md "outline / dark fabric". |
| `outline_width` | 0.015 m | Thickness at `reference_distance`; ≈ 2 px at 1080p from the 12 m overworld camera. |
| `distance_scale` | 0.8 | 0 = constant metres (thinner far away), 1 = constant pixels (grows with distance). |
| `reference_distance` | 12 m | Distance at which `outline_width` applies unscaled. |

Known limit of the inverted hull: hard-edged meshes with split normals
(the kit cubes) show small gaps at the corners. Smooth-shaded characters and
props are continuous. If a kit piece needs a clean contour, export it with
smooth normals or a bevel.

## hologram.gdshader parameters

The card is a 0.20 × 0.29 m quad (590:860, `HologramCards.Width/Height`)
facing +Z. The front shows `face_texture` (the composed card face rendered
by `CardView`, later issue), the back shows `back_texture` mirrored. Per-side
materials only differ in `edge_color` and `back_texture`; per-card state is
set with instance shader parameters, so the material count stays at two plus
one shallow copy per distinct face texture.

| Uniform | Default | Meaning |
| --- | --- | --- |
| `face_texture` | white | Composed card face. |
| `back_texture` | `assets/cards/frames/card_back.png` | Card back, shown on the reverse. |
| `face_opacity` | 0.88 | Opacity of the print; the edge glow is always fully opaque. |
| `tint_strength` | 0.12 | How much the side colour washes over the face; 0 = untinted print. |
| `edge_color` | player `#79D8FF`, opponent `#FFA18B` | Side colour for the edge, the reveal line and the dissolve rim. |
| `edge_width` | 0.035 | Edge band as a fraction of the card width. |
| `edge_glow` | 1.8 | Edge brightness multiplier; above 1 it blooms when the environment has glow enabled. |
| `selected_boost` | 1.8 | Edge brightness multiplier while `selected` = 1 (asset-list §6.3). |
| `scanline_density` | 120 | Lines per card height. |
| `scanline_strength` | 0.16 | Darkening of the line troughs; 0 removes scanlines. |
| `scanline_speed` | 0.35 | Drift speed (positive = down). |
| `flicker_strength` | 0.04 | Slow brightness shimmer amplitude. |
| `flicker_speed` | 11 | Shimmer rate. |
| `hover_amplitude` | 0.012 m | Vertical bob. |
| `hover_speed` | 0.9 | Bob cycles per second. |

Instance parameters (`MeshInstance3D.SetInstanceShaderParameter`, helpers on `HologramCards`):

| Instance uniform | Default | Meaning |
| --- | --- | --- |
| `hover_phase` | 0 | 0–1 turn offset so neighbouring cards do not bob in step. |
| `selected` | 0 | 1 while the cursor is on the card. |
| `reveal` | 1 | Materialise wipe from the bottom: 0 hidden, 1 whole card, glowing line at the edge (asset-list §6.2, tween 0.3 s). |
| `dissolve` | 0 | End dissolve: noise threshold with a glowing rim, 1 = gone (asset-list §6.7, tween 1 s). |

Rendering: `unshaded`, `cull_disabled`, `blend_mix` with `depth_prepass_alpha`
so overlapping cards sort acceptably; alpha scissor at 0.05 keeps holes in the
dissolve crisp. `world_vertex_coords` so the bob is always world-up whatever
the card's rotation (attack upright, defence rotated 90° about Z, face-down
rotated 180° about Y).

## Where to look

- `tests/scenes/SmokeTest.tscn`: the character, cube and disk in the toon
  material and three sample cards (player attack, selected; opponent defence;
  face-down). `godot --path . res://tests/scenes/SmokeTest.tscn -- --inspect`
  starts on the close camera; `menu` toggles cameras.
- `tests/scenes/CameraFraming.tscn`: the kit cubes in toon and both duelists
  with the ten-anchor grid of systems.md §6.1 in front of them, from the
  overworld camera.
- The district: every `toon_*` kit piece and prop takes the material when the
  level loads through `Game`.

Review against `docs/art/style-brief.md` and `docs/art/direction.md`
("two or three shade bands and thin contours", "retain colour in shadow",
"narrow emissive edges; readable opaque faces"). Propose parameter changes as
edits to the `.tres` files.
