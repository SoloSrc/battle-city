# Character A — complete body revision for #99

Owner: gpt-astra. Status: revised base proposed for director review, not approved.
PR: https://github.com/SoloSrc/battle-city/pull/215
Reference: `assets/source/benchmark-inputs/character-a.png` (#96).

The first proposal was rejected: it was a dressed shell without an underlying
body, with weak hands, leg alignment, pelvic volume, ears and nose. This revision
replaces that structure. Review the **body-only** images before the outfit.

## Inspect the model

- [Body front, side, back and three-quarter](../../../docs/requests/evidence/character99/body-review.png)
- [Hand and pelvis close-ups](../../../docs/requests/evidence/character99/anatomy-details.png)
- [Dressed reference comparisons](../../../docs/requests/evidence/character99/comparison.png)
- [Head detail](../../../docs/requests/evidence/character99/head.png)

Body-only Blender views use a neutral clay material on the skin mesh to expose
its contours. The exported body uses the same skin material as the head.
The reference's side pose is relaxed; the model stays in its A-pose. Comparison
images use uniform scaling and cropping, not silhouette deformation.

Open `character_a_base.blend`. Under `CHARACTER_A_BASE`:

| Collection | Contents |
| --- | --- |
| `01_BODY_complete` | Continuous torso, pelvis, legs, feet, arms and joined hands; separate integrated head and facial details |
| `02_HAIR_removable` | Retained hair cap and individually editable locks |
| `03_OUTFIT_removable` | Jacket, undershirt, trousers, shoes and trim |

Hide the outfit collection to inspect the entire body. No skin was deleted
beneath clothing. Other clothing can be modelled and fitted over this body as
separate meshes. **In-game clothing swaps still require shared rigging and skin
weights in #102**; this delivery is an unrigged modelling base, not a working
avatar system. Head/hair separation is preserved for later customization.

Two review GLBs are under `assets/characters/benchmark/`:
`character_a_base.glb` (dressed) and `character_a_body_only.glb` (body and hair).
Neither replaces the existing playable character. Both face Godot -Z with Y up.

## Construction and validation

`build.py` creates the retained outfit/hair, studio and exports; `anatomy.py`
constructs the complete body and integrated face. The body uses authored profiles and smoothly blended anatomical volumes
(`surface.py`), voxel welding and local surface refinement. The surface sampler
uses NumPy included with Blender; it imports no external character geometry. The nose belongs to the face surface. Ear rims surround a
recessed interior. Hands have broader palms, separate finger lengths and joined
roots; hips, knees and ankles use a straighter frontal alignment.

`metrics.json` records the evaluated sculpt count, bounds, collection membership
and the body topology check: **one connected component, no boundary edges and
no non-manifold edges**. This is a dense sculpt source for #100, not the ≤12k
production mesh. #100 still owns deformation topology, UVs and production
optimization; #101/#105 own painted textures/anime shading; #102 owns rigging.
Feet currently use a grouped toe volume; fine skin, nails and clothing folds
remain unfinished. Geometry tests do not establish artistic approval or pass
#110.

`verify_import.gd` imports both GLBs in Godot, checks their height/ground origin,
and compares the imported body mesh arrays. Removing the outfit must preserve
**exactly the same body geometry**. Results are in `import-check.json`.

## Reproduce

From the repository root, with Blender 5.2.2 and Python/Pillow:

```sh
blender --background --factory-startup --python assets/source/benchmark-character99/build.py
python3 assets/source/benchmark-character99/compare.py
```

This uses an isolated factory scene. The blend retains named modelling parts
and the `REVIEW_ONLY` studio collection, excluded from exports. Add
`-- --anatomy-preview` to skip the dressed/head renders during body refinement.

For Godot 4.7.2, create a temporary project with a minimal `project.godot`, copy
both GLBs to `assets/characters/benchmark/` within it, and copy
`verify_import.gd` to its root. Run:

```sh
godot --headless --path <temporary-project> --editor --import
godot --headless --path <temporary-project> --script verify_import.gd
godot --path <temporary-project> --rendering-method mobile --rendering-driver vulkan \
  --script verify_import.gd -- --body --capture <absolute-output>/godot-body.png
```

Omit `--body` for the dressed capture. These verify import and visibility, not
animation or the #97 district benchmark scene.

## Origin and time

SOLOSRC original geometry, authored by gpt-astra with Blender Python from the
repository's #96 sheet. No external base mesh, texture or 3D-generation service.
Source, exports and evidence are under the repository MIT licence.

The rejected first proposal took approximately 0.3 h elapsed agent-session time.
The correction is recorded separately in `revision-notes.md`. These are measured
session times including renders/checks, not estimates of manual artist labour.
Approval iterations, production retopology, texturing and rigging remain separate
for the #111 estimate.
