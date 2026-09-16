# Evidence: duel HUD (issue #61)

Captures from `tests/scenes/DuelUiTest.tscn` run windowed with
`-- --capture docs/requests/evidence/duel-ui` (scaled to 1280 px):

- `ui_free.png` — free cursor on a hand card: phase bar with the advance
  button (top-left), Life Points (top-right), banner, inspector (right),
  hand fan (bottom), pile and log buttons, control hint.
- `ui_menu.png` — a card's action menu (Scapegoat: Activate).
- `ui_targets.png` — the attack target list.
- `ui_response.png` — the response prompt of a summon window.
- `ui_picker.png` — an engine `Choice` (Metamorphosis' tribute) as a picker
  with Confirm.

Headless the scene reports `DuelUiTest summary: 22 pass, 0 fail`.
