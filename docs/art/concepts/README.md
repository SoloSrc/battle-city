# Duel and exploration concepts — review notes

**Author:** gpt-astra · **Date:** 2026-09-07 · **Tool:** built-in image generation.

The selected image is `street-duel-key-visual-v02.png`. It is a generated concept
for direction review, not a Blender render or Godot screenshot. Its depiction
of mechanical parts, hands, card values and perspective is illustrative.

## Reference inspection

External links are retained in [Fable's style brief](../style-brief.md).
No third-party reference image is included in this package.

| Reference | Inspection on 2026-09-07 | Applied guidance |
| --- | --- | --- |
| BDSP Hearthome city image | Viewed in browser | Saturated roofs, broad street, pale paving, soft shadow, compact buildings |
| In-battle trainer image | Host blocked access | Used Fable's character sheet and written constraints; exact image match remains unverified |
| Battle City duel still | Viewed in browser | Low dramatic camera, large foreground duelist, clear distance to opponent |
| Duel disk diagram | Reddit challenged access; director supplied the diagram directly in chat | Broad segmented blade, circular hub, separate deck/graveyard/life components |
| Anime card face | Viewed in browser | Large art area, narrow frame, bottom stars/attribute/stat band |

Generation 1 used a written synthesis of the inspected references and the five
repository concept sheets. Revision 2 used the generated image as the edit target
and the director's supplied disk diagram as an image reference. The source
reference remained outside the repository. Full prompts are in [prompts.md](prompts.md).

The first generated version is not included because it has the superseded narrow
disk and incorrect foreground arm placement. The selected revision improves the
device's massing and card artwork simplification, but does not resolve every
mechanical detail. See [direction.md](../direction.md) for production corrections.

## Review criteria

- Keep bright city daylight and the low duel camera.
- Keep consistent realistic anime proportions between exploration and duels.
- Confirm stronger disk sweep in the next orthographic design and Blender blockout.
- Verify the anatomical left forearm mount, straps, hinge and free right hand in 3D.
- Simplify masonry and foliage toward the city reference; retain clean silhouettes.
- Replace all incidental card values/backs with the authored frame and glyph system.

The original generated concept and authored documentation are contributed under
the repository's MIT license. That statement does not license the linked external
references or the director's third-party reference diagram.

## World exploration companion

`world-exploration-v02.png` uses the street-duel concept as an image reference
for costume, architecture and palette continuity, with Fable's exploration
camera requirements in the prompt. A second pass moved the folded device to
the player's left side. Generated using the built-in image tool on 2026-09-07;
full generation and correction prompts are recorded in [prompts.md](prompts.md).

Visually checked: elevated view without horizon, readable street/shop/garden,
full-proportioned characters, clear player silhouette and corrected prop side.
The image is a style target, not a metric reconstruction of the district plan;
no camera angle, collision or engine performance is verified by this image.

## Approved GDD alignment

Both concepts are visual studies, not maps or progression specifications. The
approved district is approximately 120 × 120 m with Central Plaza, Market Street,
Riverside Park and Old Arcade. Nico's tutorial occurs in the Plaza, not at the
illustrated shop corner. Use [the updated district plan](../../design/district-layout.md)
for geography, starting room, unlocks and encounter placement. Image-generation
prompts below remain a historical record of the visual process.
