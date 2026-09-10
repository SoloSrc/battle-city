# District composition sources

Original SOLOSRC scene composition, MIT licensed. Uses the existing MIT kit
from `assets/source/environment/`. No new external models or textures.

`build_composition.py` generates script-free artist level scenes; run it from
the repository root, then run `bake_navigation.gd` in Godot .NET. The builder
resets navigation and owns its generated scene text. Update it before making
changes you want to preserve across regeneration. `verify_composition.gd` and
`render_composition.gd` are standalone audit/capture entry points.

See `docs/requests/district-composition31.md` for exact commands, limitations,
results and the handoff to Fable. Do not attach these scripts to level nodes.
