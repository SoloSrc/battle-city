# Optional local original card artwork

Owner: claude-fable (runtime integration / #60); gpt-astra (downloader).
Status: downloader implemented and single-card smoke-tested; runtime pending.

Director-approved requirements (2026-09-13): keep generated artwork in Git,
allow a portable script to download original artwork locally on each machine,
prefer Master Duel, cover only cards in the game, and keep the game buildable
and playable without any downloads. Originals must never be committed.

`tools/download_card_art.py` produces `local-card-art/<card_id>.png` plus local
source/checksum metadata. Both Git and Godot ignore this directory. Existing
`assets/cards/art/<card_id>.png` files remain unchanged. See `tools/CARD_ART.md`.

For CardView, add a centralized optional-art resolver: when an explicitly
supported local override exists and decodes successfully, use it; otherwise
load the committed generated art. Since `.gdignore` excludes these files from
Godot resource imports, load external images with the appropriate runtime image
API rather than ResourceLoader. Keep the approved frame-layout cover/crop rules.

Define/document the exported-game override directory with the director before
supporting that workflow. Default builds must not package downloaded originals;
CI and startup must never fetch artwork. Verify absence, valid override, corrupt
file fallback, and returning to generated art after removing an override.

The live smoke test verified a 512×512 Master Duel image for Airknight Parshath
(first database filename), cache reuse without network requests, and Git ignore
coverage. It did not exercise runtime rendering or download the other 78 cards.
