# City District — Level Proposal

**Author:** gpt-astra · **Date:** 2026-09-07

**Status:** design proposal; no Godot scene or gameplay integration yet.

**Scope:** one district, one shop interior, three duelist encounters.

## Layout and route

Use a compact 64 × 64 m district with an obvious main street and a short loop
back to the shop. The proposed sequence is arrival → shop → first duelist →
garden street → second duelist → landmark square → final duelist. The player
can see the landmark early; Fable determines progression conditions in the GDD.
Names below are working labels, not final narrative names.

![District blockout plan](district-layout.svg)

Plan coordinates: X increases east; Z increases south; ground Y = 0. These are
layout data for review, not a new level-loading schema.

| Place | Centre X,Z (m) | Reserved footprint | Function |
| --- | --- | --- | --- |
| Arrival | 32,58 | 8 × 8 m | Start, movement orientation, shop sightline |
| Shop exterior | 18,46 | 12 × 10 m | Blue awning, south-facing entrance at 18,51 |
| Encounter A | 38,46 | 16 × 12 m | First duel by the shop corner; key visual location |
| Encounter B | 46,27 | 16 × 12 m | Garden street with different backdrop |
| Encounter C | 32,10 | 16 × 12 m | Final duel in a civic square |
| Shop interior | Separate scene | 10 × 8 m | Counter, browsing area and deck-edit interaction |

Main route occupies X=26–38 from Z=4–60, widening around encounter A and the
square. An east loop runs near X=46 from Z=10–46, joining the main route at both
ends. Keep the shop approach along Z≈54 open. Encounters are ordinary parts of
the street with invisible staging reservations, not visibly painted arenas.

Place façades mainly west of the spine and on the outer east boundary. Low
planters define the garden. Keep trees out of the duel camera's rear volume;
use architecture and roof colour for landmarks without crowding traversal.
Only the card shop is enterable. Other doors should not suggest missing gameplay.

## Encounter staging

For each encounter, test an east-west duel axis with positions 7 m apart.
Proposed positions are (centre X − 3.5, centre Z) and (centre X + 3.5, centre Z).
Reserve at least 3 m behind each participant for the camera and 4 m of lateral
card-field clearance. The 16 × 12 m reservation includes those volumes, but the
camera sweep must be tested rather than inferred from a top-down drawing.

Fable provides encounter triggers and facing/positioning behaviour. I place
encounter markers, geometry and camera-clearance volumes using those tools.
If meeting positions are invalid, the implementation should resolve to nearby
valid street positions under the agreed encounter rules; no teleport to a
separate duel arena. Return the player safely to exploration afterward.

Each location teaches a spatial idea without inventing new card mechanics:
A is a broad, easily readable shop corner; B has a green backdrop and constrained
approach but a clear duel pocket; C opens toward the district landmark. Decks,
difficulty, rewards and unlock conditions remain with Fable's GDD and systems.

## Shop and navigation

Use a 2 m doorway, at least 2 m clear aisles and an uncluttered entry landing.
Keep the counter visible from entry. Reserve separate interaction positions
for shopkeeper and deck editing; labels and input prompts come from shared UI.
The roof/front-wall treatment must preserve visibility with the chosen camera.
The exit returns to a safe point outside the same shop entrance, facing the street.

Allow a 2 m minimum unobstructed walking route around props, with wider space at
corners and NPCs. Use simple collision silhouettes for kerbs and planting rather
than detailed decorative collision. No required jumps, stairs or platforming.
Dead-end views terminate in a readable façade or boundary, not an invisible wall
across an otherwise inviting street. Shortcuts should make revisiting the shop
easy; no additional district or interior is implied by the plan.

## Build sequence and acceptance

1. Place a metric greybox, the shop shell, three encounter reservations and a
   placeholder 1.7 m avatar using Fable's project and movement components.
2. Test the entire route and shop entry/exit with keyboard and gamepad. Inspect
   all corners for collision snags and avatar occlusion.
3. Test overworld-to-duel camera transitions at A/B/C with the full card field,
   hand UI and both characters. Check extreme cards and both camera endpoints.
4. Replace blocks with one modular wall/roof/window/door kit, then add lighting,
   low planting, signs, sound emitters and encounter-specific dressing.
5. Run one complete progression path and a return-to-shop path after each duel;
   record the director's readability feedback before adding decorative density.

Acceptance: all three encounters fit without walls, trees or UI obscuring
required cards; avatar remains visible along every required path; shop entry
and return work; no extra mechanics are needed to traverse the district. Confirm
frame-time and memory budgets with Fable's profiling scene before expanding art.

No in-engine checks have been performed for this proposal. Blocking dependencies
are the runnable Godot project, movement/collision, encounter markers, camera
rig, card-field renderer and interior transition components. These are listed
in the [handoff](../requests/art-direction-review.md).
