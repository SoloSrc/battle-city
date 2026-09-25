#!/usr/bin/env python3
"""Enrich data/cards/ from the YGOPRODeck API (issue #217; systems.md §5.3).

Usage: python3 tools/fetch_card_data.py [--root data] [--langs de,fr,it,pt]
                                        [--cache .cache/carddb] [--refresh]

For every card file the script resolves the official card by exact English
name (or by a previously stored `db_id`), then writes back:

- `db_id`   — the card passcode, so later runs re-fetch by id, not name;
- `rarity`  — the rarity of the earliest TCG printing (set release dates
              come from cardsets.php), normalized to snake_case;
- `text`    — the official English card text, replacing hand-written copy;
- `i18n`    — `{lang: {"name", "text"}}` for every requested language the
              database has a translation for.

Gameplay fields (`limit`, `tier`, `effects`, monster stats, subtypes) are
never modified: the official ATK/DEF/level/attribute/type are cross-checked
against ours and any mismatch is printed as `fetch_card_data MISMATCH`.
Raw API responses are cached under --cache; pass --refresh to re-download.
Exits 1 when a card cannot be resolved or a gameplay stat disagrees.

Card data © Konami. Fetched via the free YGOPRODeck API
(https://ygoprodeck.com/api-guide/); this project stores only the card
names, texts and rarities needed by the proof of concept, no images.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

API = "https://db.ygoprodeck.com/api/v7"
LANGS = ("de", "fr", "it", "pt")
# Base printings first; the earliest set's lowest rank is the card's rarity.
RARITY_RANK = {
    "common": 0, "short_print": 1, "super_short_print": 2, "rare": 3,
    "super_rare": 4, "ultra_rare": 5, "ultimate_rare": 6, "secret_rare": 7,
}
# Our card name -> official database name, for spellings that differ.
NAME_OVERRIDES: dict[str, str] = {}
REQUEST_GAP = 0.08  # seconds between API calls (limit is 20/s)


def fetch(cache_dir: str, key: str, url: str, refresh: bool):
    """GET `url` as JSON through a file cache keyed by `key`."""
    path = os.path.join(cache_dir, key + ".json")
    if not refresh and os.path.isfile(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    time.sleep(REQUEST_GAP)
    # Cloudflare rejects urllib's default User-Agent.
    request = urllib.request.Request(url, headers={"User-Agent": "battle-city-poc/1.0 (data pipeline; issue #217)"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            doc = json.load(response)
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        try:
            # The API answers 400 with a JSON error body for unknown names.
            doc = json.loads(body)
        except json.JSONDecodeError:
            raise SystemExit(f"fetch_card_data FAIL: HTTP {e.code} from {url}: {body[:200]}") from e
    os.makedirs(cache_dir, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False)
    return doc


def snake(value: str) -> str:
    out = "".join(c.lower() if c.isalnum() else "_" for c in value.strip())
    while "__" in out:
        out = out.replace("__", "_")
    return out.strip("_")


def pick_rarity(card: dict, set_dates: dict[str, str], source: str) -> str | None:
    printings = []
    for printing in card.get("card_sets", []):
        date = set_dates.get(printing.get("set_name", ""))
        rarity = snake(printing.get("set_rarity", ""))
        if date and rarity:
            printings.append((date, RARITY_RANK.get(rarity, 99), rarity))
    if not printings:
        print(f"fetch_card_data WARN: {source}: no dated printing; rarity unchanged")
        return None
    printings.sort()
    return printings[0][2]


def cross_check(source: str, ours: dict, official: dict) -> int:
    """Compare gameplay stats; return the number of mismatches."""
    checks = []
    monster = ours.get("monster")
    if isinstance(monster, dict):
        checks += [
            ("monster.atk", monster.get("atk"), official.get("atk")),
            ("monster.def", monster.get("def"), official.get("def")),
            ("monster.level", monster.get("level"), official.get("level")),
            ("monster.attribute", monster.get("attribute"), official.get("attribute")),
            ("monster.type", monster.get("type"), official.get("race")),
        ]
    mismatches = 0
    for field, mine, theirs in checks:
        if mine != theirs:
            print(f"fetch_card_data MISMATCH: {source}: {field} is {mine!r} here, {theirs!r} in the database")
            mismatches += 1
    return mismatches


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", default="data", help="data directory (default: data)")
    parser.add_argument("--langs", default=",".join(LANGS), help="comma-separated translation languages")
    parser.add_argument("--cache", default=".cache/carddb", help="raw API response cache directory")
    parser.add_argument("--refresh", action="store_true", help="ignore the cache and re-download")
    args = parser.parse_args()
    langs = [lang for lang in args.langs.split(",") if lang]
    directory = os.path.join(args.root, "cards")
    failures = 0

    sets = fetch(args.cache, "cardsets", f"{API}/cardsets.php", args.refresh)
    set_dates = {s["set_name"]: s["tcg_date"] for s in sets if s.get("tcg_date")}

    # Pass 1: resolve every card to its official entry, by db_id then by name.
    files: list[tuple[str, dict]] = []
    official: dict[int, dict] = {}
    for name in sorted(os.listdir(directory)):
        if not name.endswith(".json"):
            continue
        path = os.path.join(directory, name)
        with open(path, encoding="utf-8") as f:
            card = json.load(f)
        files.append((path, card))
        db_id = card.get("db_id")
        if isinstance(db_id, int):
            url = f"{API}/cardinfo.php?id={db_id}"
            key = f"id_{db_id}"
        else:
            query = NAME_OVERRIDES.get(card["name"], card["name"])
            url = f"{API}/cardinfo.php?name={urllib.parse.quote(query)}"
            key = f"name_{card['id']}"
        doc = fetch(args.cache, key, url, args.refresh)
        data = doc.get("data")
        if not data:
            print(f"fetch_card_data FAIL: {path}: {doc.get('error', 'no data')} (add a NAME_OVERRIDES entry?)")
            failures += 1
            continue
        entry = data[0]
        card["db_id"] = entry["id"]
        official[entry["id"]] = entry
        failures += cross_check(path, card, entry)
        rarity = pick_rarity(entry, set_dates, path)
        if rarity:
            card["rarity"] = rarity
        card["text"] = entry.get("desc", card.get("text", ""))

    # Pass 2: one batched request per language for every resolved card.
    ids = sorted(official)
    translations: dict[str, dict[int, dict]] = {}
    for lang in langs:
        url = f"{API}/cardinfo.php?id={','.join(map(str, ids))}&language={lang}"
        doc = fetch(args.cache, f"lang_{lang}", url, args.refresh)
        translations[lang] = {entry["id"]: entry for entry in doc.get("data", [])}

    for path, card in files:
        db_id = card.get("db_id")
        if db_id not in official:
            continue
        i18n: dict[str, dict[str, str]] = {}
        for lang in langs:
            entry = translations.get(lang, {}).get(db_id)
            if entry is None:
                print(f"fetch_card_data WARN: {path}: no '{lang}' translation in the database")
                continue
            i18n[lang] = {"name": entry["name"], "text": entry.get("desc", "")}
        card["i18n"] = i18n
        with open(path, "w", encoding="utf-8") as f:
            json.dump(card, f, ensure_ascii=False, indent=2)
            f.write("\n")

    status = "FAIL" if failures else "PASS"
    print(f"fetch_card_data summary: {status}, {len(files)} cards, {failures} failures")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
