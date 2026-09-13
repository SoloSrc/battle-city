# Original symbolic card placeholders

Owner: gpt-astra. MIT, original SOLOSRC vector shapes. No published card art,
traced imagery, external fonts or raster references are included. These are
semantic emblems for gameplay testing, not final monster/spell illustrations.

Each of the 72 card IDs in `data/cards` has an editable SVG, an opaque 512×512
PNG at `assets/cards/art/<id>.png`, and an entry in `manifest.json`. The primary
and secondary motif pairs are explicit in `build.py`; colors group attributes
and spell/trap types. Do not infer mechanics from the artwork alone.

Run from any directory, using Python 3, Node and Sharp:

```sh
python3 assets/source/cards/placeholders/build.py
node assets/source/cards/placeholders/export.cjs
godot --headless --editor --path . --import
godot --headless --path . --script assets/source/cards/placeholders/verify.gd
```

Use the repository root for the commands above. `SHARP_MODULE` may point to an
installed Sharp module if it is outside Node's normal search path. The source
builder fails if its explicitly authored motif roster differs from card data.
The exporter checks dimensions, full opacity and unique decoded pixel hashes;
`validation.json` records the results. Godot verification checks all 72 textures
and StandardMaterial3D bindings. Source files stay excluded from Godot by the
existing `assets/source/.gdignore`.

The exporter also builds `docs/art/previews/card-placeholders/index.html`, a
contact sheet and six 177×258 frame samples. Review annotations use a system
font, which is not distributed or baked into the runtime art. Contain is the
review default; the HTML offers cover as a comparison. Neither sets runtime
crop policy: systems §6.2 leaves that decision deferred. Existing approved frame
assets and geometry are unchanged. Spell/trap type badges are omitted from the
review composites, which are not intended to replace the runtime CardView.
