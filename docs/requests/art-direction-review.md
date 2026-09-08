# Art Direction and Level Tooling Handoff

**From:** gpt-astra · **To:** claude-fable and director

**Date:** 2026-09-07 · **Status:** reviewed; answers in [systems.md](../design/systems.md) §11 and [architecture.md](../tech/architecture.md)

**Related:** issues #3, #5, #6 and #7

**Baseline:** director-approved GDD at main `35c2012`; documents updated to match.

**Location:** `gpt-astra` branch; review package submitted through a pull request to `main`.

Review [art/audio direction](../art/direction.md) and
[district proposal](../design/district-layout.md). The key visual is a concept,
not a finished model or runtime screenshot. Director's latest disk diagram
supersedes the first concept's narrow straight blade; use a broad swept blade
and substantial circular hub, with original detailing.

| Owner | Review / requirement | Result needed |
| --- | --- | --- |
| Director | Style, key visual, disk massing, district route | Approved direction or specific revisions |
| claude-fable | Sheet 01's conflicting camera framing targets | Keep GDD camera baseline; compare ~60 px and 90–120 px only to diagnose framing |
| claude-fable | Shader/renderer and texture/triangle budgets | Confirm or revise before production exports |
| claude-fable | Shared skeleton, animation events and left-forearm attachment | Reference rig contract and attachment convention |
| claude-fable | Square art in portrait window; inconsistent border notes | Crop/UV bounds and frame composition contract |
| claude-fable | Physical five bays versus ten world card zones | Named anchors and mapping independent of visual bay spacing |
| claude-fable | Level authoring | Reusable movement, LOS/approach, progression gates, area camera bounds, room/shop transitions and save/return components |
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

## GDD alignment completed

The district proposal/diagram now use 120 × 120 m, Central Plaza spawn and Nico,
Market Street with two NPCs and shop, Riverside Park with Mara, Old Arcade with
the final opponent, and closed district edges. They specify a starting room,
pause-menu deck editor, 8 m / 60° detection cones, gated progression, manual
rematches and post-ending free play. Detailed coordinates remain proposals.

Art direction now records the approved avatar option counts, top-edge chain HUD
clearance, per-area camera bounds, required low-LP intensity layer, ending theme
and complete GDD cue categories. Shop music/final-opponent variation are optional.
The concept images retain their visual role and are not authoritative level maps.

Fable's remaining technical decisions include trigger rearming after a loss,
valid street staging after NPC approach, rig/anchor interfaces, camera projection
and music-layer mixing. This alignment does not alter GDD mechanics or declare
any untested engine behaviour complete.
