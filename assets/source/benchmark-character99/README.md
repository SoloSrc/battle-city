# Character A base — #99

Owner: gpt-astra. Status: base proposed for director review, not approved.
Branch: `astra/99-character-base`. Source reference:
`assets/source/benchmark-inputs/character-a.png` (approved #96 sheet).

## What to review

[Reference/render comparison](../../../docs/requests/evidence/character99/comparison.png):
front, side and three-quarter, plus separate back and head renders in the same
folder. This is the first base proposal, with local modelling corrections
already incorporated. Select or request changes to the silhouette, body
proportions, face, hair mass and clothing volumes before #100.

The sheet's front view anchors the geometry. Side and three-quarter views
are guidance: their arm poses and perspective do not match a single
orthographic A-pose. The comparison uses fixed uniform image scales and crops,
without stretching the model to fit the artwork. The side capture looks from
Blender +X, matching the reference's left-facing profile. The head detail uses
a slightly turned view. Studio images use neutral Principled materials and
soft lights, not the future anime shader.

## Files and modelling decisions

- `character_a_base.blend`: editable mesh sections, subdivision modifiers,
  curve trim, separate hair locks and clothing, with a studio/camera collection.
- `build.py`: the exact hand-authored Blender Python construction and export.
  No existing character mesh, 3D generator or external base model was used.
- `metrics.json`: evaluated geometry count and bounds from the final build.
- `../../characters/benchmark/character_a_base.glb`: unrigged review export.
  It does not replace `char_a_body.glb` or the playable character.
- `compare.py`: factual reference/render layout, requires Pillow.
- `verify_import.gd`: asset-specific isolated Godot import/bounds check and
  optional capture. This is not a shared gameplay or level-building tool.

The body follows the reference's adult anime proportions, cropped cobalt
jacket, ivory piping, charcoal clothes and two-tone shoes. Jacket, head, neck,
limbs/hands, eyes, hair and shoes remain separate editable parts. Trousers use
one connected mesh with a crotch saddle. Hair uses an underlying cap with
individually shaped swept locks. Hands retain individual finger volumes.

Overall evaluated height is exactly 1.70 m, soles at 0. Blender authoring is
Z-up, facing -Y. The export rotates the character 180 degrees around Blender
Z so glTF conversion yields Godot Y-up and facing -Z. The review export has
97,418 evaluated triangles across 112 meshes, including subdivision and trim.
This is a base modelling source, **not** the ≤12k production mesh required by
#100. The low-resolution editable cages are preserved in the blend file.

## Remaining work, after approval

- #100: retopology to ≤12k, facial/deformation loops, weld limb/hand junctions,
  resolve hidden/intersecting shell surfaces, UVs and final clothing folds.
- #101 / #105: painted textures and shader masks/face shadow map. Current flat
  colour materials are inspection aids; no baked lighting or image textures.
- #102: shared humanoid skeleton, skinning, deformation and animation checks.

The reference's angular cloth folds and illustrated hair shading are not
finished here. Shoulder attachment seams and the independent facial pieces
remain visible at close range. Approval of this base does not establish final
concept fidelity or pass the #110 benchmark gate.

## Reproduce

From the repository root, using Blender 5.2.2:

```sh
blender --background --factory-startup --python assets/source/benchmark-character99/build.py
python3 assets/source/benchmark-character99/compare.py
```

The build writes the blend, GLB, metrics and five Blender views. It starts its
own factory scene; do not run it inside somebody else's open Blender session.
The blend contains only original model geometry plus clearly named
`REVIEW_ONLY` studio objects, which are excluded from export.

For an isolated Godot 4.7.2 check, create a temporary project containing a
minimal `project.godot`, copy the GLB to the **same relative path**
`assets/characters/benchmark/character_a_base.glb`, and copy `verify_import.gd`
to its root. Then:

```sh
godot --headless --path <temporary-project> --editor --import
godot --headless --path <temporary-project> --script verify_import.gd
godot --path <temporary-project> --rendering-method mobile --rendering-driver vulkan \
  --script verify_import.gd -- --capture <absolute-output>/godot-front.png
```

The check instantiates the imported scene, counts meshes/materials/triangles,
and asserts height and ground contact within 2 mm. The Godot capture is only
an import/orientation check, not the full #97 district look-development review.

## Origin and licence

SOLOSRC original, authored by gpt-astra using Blender Python surface modelling,
from the repository's generated #96 reference sheet. No paid service,
third-party model, texture or character asset. Source, exported geometry and
renders are under the repository MIT licence. The comparison reproduces the
same SOLOSRC reference, also under the repository MIT licence.

## Time record

2026-09-23 America/Recife (2026-09-24 UTC), approximately 0.3 hours of elapsed
session work for this base proposal, including requirements, mesh construction,
iterative visual refinement, renders, import checks and packaging. This is
wall-clock agent-session time, not an estimate of manual artist labour. Approval
iterations, #100 cleanup, texturing and rigging are not included. Carry the
measured proposal time into #111 only with these limits.
