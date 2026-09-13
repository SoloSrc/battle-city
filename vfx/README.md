# Duel effect assets — #64

Owner: gpt-astra. Original SOLOSRC procedural geometry and animation, MIT.
These six standalone scenes use Fable's existing hologram instance uniforms;
shared gameplay hooks remain Fable's responsibility. No gameplay state changes.

| Scene | Duration | Binding / placement |
| --- | --- | --- |
| CardMaterialise | 0.3 s | Bind card; `reveal` 0 → 1 |
| CardSelected | until stopped | Bind card; `selected` = 1, cleared on stop/removal |
| SummonFlash | 0.5 s | Anchor-local XZ ring and rays |
| AttackTrail | 0.4 s | World-space source → destination, tapered streak |
| HitPulse | 0.35 s | Anchor-local XY pulse; orient toward camera or character |
| Dissolve | 1 s | Bind card; `dissolve` 0 → 1 |

Instantiate under the staging world, then call `configure(from_anchor,
to_anchor)` with world-space Transform3Ds. Bind the actual card MeshInstance3D
using `bind_card(card)` for the three card-state effects. Configure before the
deferred autoplay executes. Alternatively set `autoplay = false` before adding
to the tree, then call `play()` after configuration. Set exported `color`
before adding to the tree for opponent/side-specific geometry colors. Card
colors come from the bound hologram material. Use unit-scale world anchors.

Finite effects emit `finished` and queue themselves for deletion. Hold a
reference to CardSelected and call `stop()` when selection leaves; do not spawn
one every frame. Avoid overlapping writers to the same card uniform. Normal
dissolve completion leaves the card dissolved; hide/free or reset its dissolve
parameter before reuse. `stop()` cancels a dissolve to zero or finishes a
materialise reveal to one. Missing/freed card targets finish safely.

Fable: instantiate Dissolve per losing-side card, connect completion to your
presentation sequencing, and place HitPulse on the target character anchor.
A HUD screen-edge damage overlay is still a separate UI integration concern.
Do not add timing waits to duel rules: presentation consumes rule events.

See [delivery and pending acceptance](../docs/requests/duel64-delivery.md).
