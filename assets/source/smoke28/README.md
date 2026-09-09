# Pipeline smoke-test sources (#28)

Owner: gpt-astra. Original procedural meshes, rig, animation and atlas authored
for SOLOSRC and released under the repository MIT license. No third-party mesh,
texture or animation files are included. These are technical blockouts, not the
final character or disk art.

- `kit_test_cube_1m.blend`: metric cube, visible collision-suffixed mesh.
- `char_a_body.blend`: body A with integrated hair/outfit; shared 23-bone rig,
  A-pose rest and idle/walk NLA strips. Set armature pose position to Rest Position
  to inspect the bind pose. Animation export carries a duplicate skinned mesh;
  consume its clips, not that mesh, when mounting it onto the body asset.
- `prop_duel_disk.blend`: packed original 512² atlas, hinged blade, markers and
  deploy/fold strips. Saved in deployed pose. The fold swivels the blade along
  the forearm; no telescoping geometry is represented in this blockout.
- `disk_mount.json`: proposed body-A offset in PR #34's data schema. Body B is
  untuned. Fable should apply A to `data/rig/disk_mount.json` after review.
- `build_report.json`: counts, clip durations and proposed walk footstep times.

## Rebuild

Run from the repository root with Blender 5.2.1 (or compatible 5.2+):

```sh
blender --background --factory-startup --python assets/source/smoke28/build_assets.py
godot --headless --editor --import
godot --headless --script assets/source/smoke28/set_import.gd
godot --headless --editor --import
godot --headless --script assets/source/smoke28/verify_import.gd
```

The builder writes runtime exports and sources into this repository; set
`SMOKE28_REPO` to a disposable checkout to regenerate elsewhere. Commit the GLB
`.import` presets, including the idle/walk loop settings. Source `.blend` files
use Git LFS. `assets/source/.gdignore` prevents Godot importing them a second time.
The atlas in `assets/props/prop_duel_disk_albedo.png` is the authored export;
Godot extracts the embedded GLB image with a model-name prefix on first import.

On this macOS 13 machine Blender's bundled NumPy 2.3.4 could not load its BLAS
symbols. A separate NumPy 2.2.6 installation for Blender's Python 3.13 resolved
it. Point `SMOKE28_PYTHON_DEPS` to that installation if needed; the builder inserts
it into its Python path. Blender itself was not modified. This workaround is
machine-specific, not a normal project dependency.

See [handoff](../../../docs/requests/pipeline-smoke28.md) for validation and
integration details.
