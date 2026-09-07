# Art Direction and Level Tooling Handoff

**From:** gpt-astra · **To:** claude-fable and director

**Date:** 2026-09-07 · **Status:** ready for review

**Related:** issues #5, #6 and #7

**Location:** `gpt-astra` branch; review package submitted through a pull request to `main`.

Review [art/audio direction](../art/direction.md) and
[district proposal](../design/district-layout.md). The key visual is a concept,
not a finished model or runtime screenshot. Director's latest disk diagram
supersedes the first concept's narrow straight blade; use a broad swept blade
and substantial circular hub, with original detailing.

| Owner | Review / requirement | Result needed |
| --- | --- | --- |
| Director | Style, key visual, disk massing, district route | Approved direction or specific revisions |
| claude-fable | Sheet 01's conflicting camera framing targets | Godot comparison of ~60 px and 90–120 px avatar at 1080p |
| claude-fable | Shader/renderer and texture/triangle budgets | Confirm or revise before production exports |
| claude-fable | Shared skeleton, animation events and left-forearm attachment | Reference rig contract and attachment convention |
| claude-fable | Square art in portrait window; inconsistent border notes | Crop/UV bounds and frame composition contract |
| claude-fable | Physical five bays versus ten world card zones | Named anchors and mapping independent of visual bay spacing |
| claude-fable | Level authoring | Reusable movement, collision, encounter-marker, camera and shop-transition components |
| claude-fable | Issue #6 | Generate authoritative asset list from GDD/systems and this direction |
| gpt-astra | Following review | Turnaround, disk blockout, frames/icons, then district greybox |

Suggested smoke test: one 1 m cube, 1.7 m character, left-mounted disk, two
duelists 7 m apart and ten card anchors per side in a 16 × 12 m clear street
pocket. Check scale/orientation, wrist alignment, camera clipping, hand UI
overlap and selected-card readability before full environment production.

The supplied diagram's search system and monster projection labels are reference
features, not added PoC requirements. No 3D monster models are requested.

Review performed for this package: source requirements and concept inspected;
local Markdown links and SVG syntax checked. No engine performance, animations,
audio loops or navigation have been validated yet.
