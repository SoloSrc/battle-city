# Revision 5 validation — 2026-09-13

- Regenerated six 590×860 frames, six subtype glyphs and all review compositions.
- 23 exports pass dimension/window/back-symmetry checks.
- Godot 4.7.2 headless import completed without errors; verify_import.gd reported
  23 imported textures with expected sizes and StandardMaterial3D bindings.
- All levels 1–12 pass left-margin and attribute-clearance assertions. Level12:
  star row x26..467, attribute left491 (24px gap).
- Spell/Trap plate center equals colored-panel center in both axes (295,745).
- Attribute/star center y686; attribute bottom719, decorative number plate top732.
- All 96 existing gameplay art, icon and back PNGs byte-identical to prior commit.
- 30 Spell/Trap HTML faces carry one main badge each, no subtype badges.
- Six-type exported sheet visually inspected for cloud continuity, muted palettes,
  matched bevel widths, upright large numbers and unclipped icons.
- git diff --check passed. This is asset validation, not runtime CardView testing.

Follow-up: monster stat-plate fill reduced from 48% to 20%. Regenerated all
exports/review samples; visually verified cloud detail through the Fusion
number plates. Export and layout validation passed again. Spell/Trap frame
PNGs are byte-identical to the preceding revision.

Second follow-up: director found 20% too transparent; adjusted monster fill to
35%. Shifted all six types’ decorative plate borders from yellow to orange-red
copper. Regenerated samples, visually inspected all six types, and reran export
and layout checks successfully. Spell/Trap fill remains 48%.

Final director adjustment: monster plate fill 30%; Spell/Trap remains 48%.
Exports and layout validation rerun after regeneration.
