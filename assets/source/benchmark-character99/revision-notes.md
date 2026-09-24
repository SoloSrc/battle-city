# Director correction — #99 / PR #215

This revision replaces the rejected clothed shell with a complete underlying
body. It is a correction commit on the existing PR, preserving the first
proposal in review history.

| Reported problem | Change | Evidence |
| --- | --- | --- |
| Thin, disconnected fingers | Wider shaped palms, distinct finger lengths, tapered phalanges and joined roots/wrists | `hand-palm.png`, `hand-back.png` |
| Knees bent inward | Straighter hip/knee/ankle axes and matching shoe placement; reduced exaggerated knee profile | `body-front.png`, `body-side.png` |
| Missing gluteal volume | Complete blended pelvic/thigh surface with paired posterior masses and a rounded profile | `body-back.png`, `pelvis-back.png`, `pelvis-side.png` |
| Bean-like attached ears | Recessed pinnae and helix rims fused into the head surface | `head.png` |
| Attached, unusual nose | Bridge, tip and alae formed in the facial surface | `head.png` |
| No reusable body under clothes | Complete closed skin mesh, named independent body/hair/outfit collections and a body-only GLB | `body-review.png`, `import-check.json` |

The torso/limbs/hands/feet mesh must pass the connected-component and manifold
checks in `build.py`. The Godot check imports both exports and compares their
body mesh arrays. The complete body must survive outfit removal unchanged.

Hair geometry is retained. Clothing remains a fit/reference outfit. The skin
mesh is intentionally a dense modelling source; production topology, UVs,
textures and rigging remain #100–#102. Feet use grouped toe volumes, and fine
skin/nail detail is unfinished. A complete body does not by itself implement
animated clothing swaps; those meshes need the same skeleton and compatible
weights later.

All evidence paths are relative to `docs/requests/evidence/character99/`.
Body-only clay views expose the underlying geometry directly. No image synthesis
or retouching was used to conceal mesh defects; the contact sheets only lay out
actual renders.

Timing: correction work began 2026-09-24 at approximately 02:06 UTC. Elapsed
agent-session work through the final renders/checks was approximately 0.6 h,
including local modelling iterations and verification. This is separate from
the rejected first proposal's 0.3 h and is not a manual-artist labour estimate.
Further director review iterations are not included.
