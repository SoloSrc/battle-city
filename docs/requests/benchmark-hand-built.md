# Benchmark goes hand-built — handoff to gpt-astra

Owner: claude-fable → gpt-astra. Status: open. Date: 2026-09-22.
Phase: #98 (Vertical slice · Phase 1: Benchmark). Decision:
`docs/decisions.md` 2026-09-22.

## What changed

The director dropped the 3D-generation arm (Hunyuan3D, Hyper3D Rodin): the
services cost money the project does not have. The benchmark's three assets —
character A, the blue-awning shop front, the street-corner prop group — are
**modelled, textured and rigged by hand in Blender** by gpt-astra, from the
approved #96 input sheets in `assets/source/benchmark-inputs/`.

"Hand-built" means whatever Blender workflow suits you (interactive tools,
Python, modifiers, sculpt); the constraint is the result, judged against the
concept art in the #97 side-by-side, not the technique. This is the quality
test the greybox never was: take the time the concepts deserve.

## What stays the same

- The #96 sheets remain the reference; their guidance (proportions, scale
  targets, front view as silhouette anchor) still applies.
- Budgets and deliverables of #99–#104: character ≤ 12 k tris, deformation
  loops, separable head/hair/outfit, the shared skeleton and rest pose, the
  kit's metric grid for the shop, greybox pivots for props where one exists.
- The masks and face shadow map come from the shader spec (#105); no baked
  lighting.
- The #110 gate is unchanged: the director compares the result with the
  concepts and approves or sends it back. If characters fail twice, they
  switch to an anime base model.

## What claude-fable provides

- #95 (rescoped): the Blender → glb → Godot round trip, documented in
  `assets/README.md`. Headless glTF export is verified working on the
  current machine (Linux, Blender 5.2.2): the macOS NumPy blocker does not
  apply here.
- #97: the look-development scene and the concept/render side-by-side
  capture command.
- #105–#107: the anime character shader, outline pass and colour grade the
  benchmark is judged under.

## What gpt-astra should do

1. Wait for the director's fidelity approval of the #96 sheets (still
   pending per `assets/source/benchmark-inputs/README.md`).
2. Take #99–#104 as hand-modelling tasks; the issue bodies have been updated.
   Start with the street-corner props (#104's pieces) if you want a warm-up:
   they are the smallest and the most forgiving.
3. Record hours per asset as you go; #111 re-estimates production from them.
