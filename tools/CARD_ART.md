# Optional local card artwork

Requires Python 3.10 or newer; no pip packages, credentials, Godot, or AI tools.
Run from a clone on macOS, Linux, or Windows (use `py -3` instead of `python3`
where appropriate). Paths resolve relative to the script, not the current directory.

```sh
# Offline: show the first database card (sorted by filename).
python3 tools/download_card_art.py --first --list
# Smoke test: download only that card.
python3 tools/download_card_art.py --first
# Download one specific card.
python3 tools/download_card_art.py --card archfiend_soldier
# Download all current database cards, sequentially.
python3 tools/download_card_art.py
# Optional slower pacing.
python3 tools/download_card_art.py --interval 10
```

The script resolves the expected Master Duel English artwork filename using
Yugipedia's MediaWiki API. If absent, it consults the card's artwork gallery,
preferring Master Duel, then English, then a deterministic filename order among
other game artwork PNGs. It never downloads full-card scans. Missing galleries
or unresolvable art stop the run with an actionable error; generated art stays
intact. Discovery is not yet validated across all 79 cards.

Files and provenance/checksum metadata go to `local-card-art/<card_id>.png`
and `.json`. This entire directory is Git-ignored. Its `.gdignore` prevents
Godot from importing or automatically bundling these files. Do not force-add
these downloads or copy them into tracked source assets. Third-party artwork
is not covered by this repository's MIT license.

Existing files with matching saved SHA-256 checksums are reused without network
requests. Interrupted batches resume by skipping completed cards. Individual
partial transfers restart, rather than using HTTP byte ranges. Writes use
`.part` files followed by atomic replacement. A lock prevents concurrent runs
in the same checkout; after an abrupt process termination, remove a stale
`.download.lock` only after confirming no downloader remains active. Different
machines/checkouts have independent pacing, so avoid synchronized bulk runs.

Requests are serial, at least five seconds apart, with an identifying User-Agent,
robots checks for each host, and API `maxlag=5`. Longer robots crawl delays/request
rates are honored. A missing robots file (404/410) is permitted; access denials
stop the run. HTTP 429 and transient server failures receive at most two retries
with increasing waits, honoring Retry-After. Waits longer than five minutes stop
the run so it can be retried later. API errors, challenges, unexpected redirects,
and non-PNG downloads stop rather than being bypassed. No downloads run during
builds, CI, or ordinary game startup.

This change supplies the downloader only. Runtime override loading is a separate
CardView integration; see `docs/requests/optional-card-art.md`. Until integrated,
the game continues using generated artwork even after a successful download.

## Validation

```sh
python3 -B -m unittest discover -s tools -p test_download_card_art.py
```

Live smoke test on 2026-09-13 (America/Sao_Paulo): `--first` downloaded
`AirknightParshath-MADU-EN-VG-artwork.png` at 512×512. A second run reported
`Cached: airknight_parshath`. SHA-256:
`1a4f6dcab0898945ee0b399083cc2408b6e450bc094fded3fb60c75927ba913e`.
The API worked for the standalone client even though the assistant's browsing
client was blocked. The card has no artwork gallery page; preferred-file lookup
successfully handles that case. Nine offline tests cover discovery, selection,
robots handling, host restrictions, invalid content, and cache reuse.
