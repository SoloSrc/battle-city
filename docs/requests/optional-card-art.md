# Optional local original card artwork

Owner: claude-fable (runtime integration / #60); gpt-astra (downloader).
Status: runtime integration implemented by gpt-astra at the director’s request (2026-09-20); PR review pending.

Director-approved requirements (2026-09-13): keep generated artwork in Git,
allow a portable script to download original artwork locally on each machine,
prefer Master Duel, cover only cards in the game, and keep the game buildable
and playable without any downloads. Originals must never be committed.

`tools/download_card_art.py` produces `local-card-art/<card_id>.png` plus local
source/checksum metadata. Both Git and Godot ignore this directory. Existing
`assets/cards/art/<card_id>.png` files remain unchanged. See `tools/CARD_ART.md`.

The centralized CardArtwork resolver now prefers a successfully decoded local
override, otherwise loading the committed generated art. Since `.gdignore`
excludes these files from resource imports, the resolver uses Godot Image for
external PNGs. The approved frame-layout cover/crop rules remain unchanged.

Define/document the exported-game override directory with the director before
supporting that workflow. Default builds must not package downloaded originals;
CI and startup must never fetch artwork. Verify absence, valid override, corrupt
file fallback, and returning to generated art after removing an override.

The live smoke test verified a 512×512 Master Duel image for Airknight Parshath
(first database filename), cache reuse without network requests, and Git ignore
coverage. That initial smoke test did not exercise runtime rendering. Subsequently all
79 downloads were decoded successfully; runtime verification is recorded below.

## Runtime integration follow-up — 2026-09-20

The director reported that downloaded art still showed as placeholders and
requested a fix. `CardFaces.Compose` now calls `CardArtwork.Load`. In project
runs the resolver decodes the ignored local PNG directly with Godot Image and
creates an ImageTexture; ResourceLoader continues loading the generated fallback.
This intentionally small shared-runtime change is handed back to claude-fable
for ongoing ownership. Frame geometry, centered-cover crop, game rules, and
tracked art are unchanged. Exports retain the existing generated-only behavior.

The 79 downloads exist in the artist worktree, not the human main worktree.
Run the existing downloader in each checkout used for play. Restart the game
after changing overrides, since CardFaces caches composed faces.

Offline regression: `Godot --headless --path . tests/scenes/CardArtworkTest.tscn`.
It creates disposable fixture files outside the repository and checks missing,
valid unimported PNG, corrupt, removed, and generated-only paths; if the actual
Airknight download exists, it compares decoded pixels from the production loader.
Non-PNG files are rejected by an eight-byte signature check before decoding,
with a warning and fallback. Signed but corrupt PNGs can still emit Godot
decoder errors. The diagnostic reports a summary and exits nonzero on failure.

Validation of this fix: .NET build passed with zero warnings/errors; the offline
resolver diagnostic passed all nine checks with the actual download present.
The rendered DuelStagingTest passed 54 checks with zero failures and saved 32
baked card faces. Archfiend Soldier was visually checked with original artwork,
approved frame, crop, stars, attribute and stats. Review captures remain in
`/tmp/card-art-runtime`, outside Git, because they contain third-party art.

Fable review follow-up: committed Godot-generated script UIDs; staging/UI and district
acceptance diagnostics now force generated artwork before composition. Other
runs can pass `--generated-art`. The resolver fixture follows diagnostic report
conventions and joins the Godot CI job. Decisions, systems §6.2, source index,
and scene index now document the runtime contract.

Review validation: build and Godot import clean; resolver 10/10 with downloads,
9/9 with the command-line opt-out; headless staging 54/54 and UI 24/24.
Rendered staging capture with downloads present showed the generated Archfiend
Soldier placeholder, confirming the evidence opt-out. No decoder ERROR lines
occurred in the updated fixture test.
