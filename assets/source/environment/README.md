# Greybox environment kit — issue #32

Owner: gpt-astra. 58 original procedural assets, MIT licensed under the repository
license. Editable Blender sources use Git LFS. Runtime meshes are in `assets/kit/`
and `assets/props/`; `manifest.json` records exact exported dimensions, triangle
counts, collision expectations and placement notes. No third-party assets used.

These are metric blockouts for district composition. They do not replace the
approved art direction or claim production environment detail. Flat `toon_*`
materials identify colour roles; #27 supplies the final shared toon shader.
Blank shop/arcade signs and glass/light/water surfaces are placeholders. No sign
lettering, emissive light logic, water VFX, animation or gameplay is embedded.

## Rebuild and inspect

From the repo root with Blender 5.2.1 and Godot 4.7.2:

```sh
blender --background --factory-startup --python assets/source/environment/build_environment.py
python3 assets/source/environment/build_gallery.py
godot --headless --editor --import
godot --headless --script assets/source/environment/verify_kit.gd
godot res://levels/review/EnvironmentKit.tscn
```

Set `KIT_REPO` to a different checkout for isolated regeneration. The optional
`SMOKE28_PYTHON_DEPS` workaround for this machine's Blender/NumPy ABI mismatch
is documented in `assets/source/smoke28/README.md`. Regeneration overwrites these
kit sources/exports; preserve manual edits before rebuilding. Blender may create
`.blend1` backups; they are not part of this delivery.

## Placement contract

- Metres; kit root at bounds-min; props at floor height. All Godot kit bounds
  minima are (0,0,0). The builder authors (x,depth,height) and converts to Blender
  (x,-depth,height), yielding Godot (x,height,depth).
- Grid is 1 m, with 0.25 m structural detail. Thin trim can be finer. Most ground
  pieces are 2×2 m, edge strips 1×2 m, walls 2 m wide and 3.5 m tall. Stack two
  storeys for the 7 m building height. Roof modules cover 2×2 m.
- Ground slabs are 0.25 m thick. Place at Y=-0.25 for walking surface Y=0.
  A sidewalk raised to Y=0.25 can use slab origin Y=0; kerb/ramp origin Y=0.
  The kerb ramp rises 0.25 m over 1 m. Player traversal still needs district testing.
- `kit_wall_door` spans **4 m** so the opening itself stays **2 m clear** and
  2.5 m tall. This is a two-module portal, not a narrowed 2 m wall. Door leaves
  are separate colliding assets: remove/open them during transitions via Fable's
  components. Do not permanently close a walkable portal with a static leaf.
- Visible `-col` meshes generate collision. Water is visual-only, and needs the
  river-edge/rail and progression boundaries. Tree canopy collision is deliberately
  conservative for greybox; refine it during dressing. No navmesh is included.
- Gallery positions use integer metre coordinates. The gallery is a review scene,
  not District.tscn, and its overview camera/floor are not gameplay settings.

See [handoff](../../../docs/requests/environment-kit32.md) for outstanding checks.
