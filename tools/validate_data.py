#!/usr/bin/env python3
"""Validate the game data under data/ (architecture.md §5; schemas in systems.md §5.3, §7, §8, GDD §1.1).

Usage: python3 tools/validate_data.py [--root data]

Prints one `validate_data FAIL: <file>: <rule>` line per problem and a summary;
exits 1 on any failure. The C# loaders (Duel.Core CardLoader, BattleCity.Data)
apply the same rules at runtime; this script is the CI gate that needs no build.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

SNAKE = re.compile(r"^[a-z0-9_]+$")
FLAG = re.compile(r"^[a-z0-9_]+(:[a-z0-9_]+)?$")
HEX = re.compile(r"^#[0-9a-fA-F]{6}$")
KINDS = {"monster", "spell", "trap", "fusion"}
CATEGORIES = {"normal", "effect", "fusion", "ritual", "flip", "spirit", "toon", "union"}
ATTRIBUTES = {"DARK", "LIGHT", "EARTH", "WATER", "FIRE", "WIND", "DIVINE"}
SPELL_SUBTYPES = {"normal", "quick", "equip", "continuous", "field", "ritual"}
TRAP_SUBTYPES = {"normal", "continuous", "counter"}
# Effects implemented in Duel.Core's EffectRegistry (tiers 1–4, the whole pool): every effect id a card names must be here.
IMPLEMENTED_EFFECTS = {
    "pot_of_greed",
    # Tier 2 (issue #56).
    "graceful_charity", "morphing_jar",
    "fissure", "smashing_ground", "heavy_storm", "mystical_space_typhoon", "lightning_vortex", "dust_tornado",
    "mirror_force", "sakuretsu_armor", "widespread_ruin", "torrential_tribute", "trap_hole",
    "zaborg_the_thunder_monarch", "mobius_the_frost_monarch", "exiled_force",
    "sangan", "reinforcement_of_the_army", "the_warrior_returning_alive", "gravekeepers_spy", "magician_of_faith",
    "axe_of_despair", "axe_of_despair_recycle", "book_of_moon", "berserk_gorilla", "goblin_attack_force", "giant_orc",
    "gravekeepers_guard",
    # Tier 3 (issue #57).
    "enraged_battle_ox", "jinzo", "blade_knight", "command_knight", "marauding_captain", "asura_priest", "mystic_swordsman_lv2",
    "reaper_on_the_nightmare", "dark_balter_the_terrible",
    "don_zaloog", "dd_warrior_lady", "dd_assailant", "airknight_parshath", "kycoo_the_ghost_destroyer", "mystic_tomato", "shining_angel",
    "breaker_the_magical_warrior", "breaker_the_magical_warrior_destroy", "chaos_sorcerer", "chaos_sorcerer_banish",
    "skilled_dark_magician", "skilled_dark_magician_summon", "tribe_infecting_virus", "sinister_serpent", "tsukuyomi",
    "delinquent_duo", "premature_burial", "snatch_steal", "snatch_steal_upkeep", "nobleman_of_crossout", "enemy_controller",
    "scapegoat", "metamorphosis", "swords_of_revealing_light", "creature_swap",
    "ring_of_destruction", "call_of_the_haunted", "bottomless_trap_hole", "waboku",
    # Tier 4 (issue #58).
    "spirit_reaper", "black_luster_soldier_envoy_of_the_beginning", "black_luster_soldier_envoy_of_the_beginning_banish",
    "black_luster_soldier_envoy_of_the_beginning_double_attack", "thousand_eyes_restrict", "thousand_eyes_restrict_absorb", "cyber_jar",
}
MIN_DECK, MAX_DECK = 40, 60


class Report:
    def __init__(self) -> None:
        self.failures = 0
        self.checks = 0

    def fail(self, source: str, message: str) -> None:
        self.failures += 1
        print(f"validate_data FAIL: {source}: {message}")

    def check(self, ok: bool, source: str, message: str) -> bool:
        self.checks += 1
        if not ok:
            self.fail(source, message)
        return ok


def load_json(report: Report, path: str):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        report.fail(path, "file not found")
    except json.JSONDecodeError as e:
        report.fail(path, f"invalid JSON: {e}")
    return None


def is_int(value, minimum=None, maximum=None) -> bool:
    if not isinstance(value, int) or isinstance(value, bool):
        return False
    return (minimum is None or value >= minimum) and (maximum is None or value <= maximum)


def validate_card(report: Report, path: str, doc) -> dict | None:
    if not isinstance(doc, dict):
        report.fail(path, "card must be a JSON object")
        return None
    card_id = doc.get("id")
    expected = os.path.splitext(os.path.basename(path))[0]
    ok = report.check(isinstance(card_id, str) and bool(SNAKE.match(card_id)), path, f"'id' must be snake_case (got {card_id!r})")
    if ok:
        report.check(card_id == expected, path, f"id '{card_id}' does not match the file name '{expected}'")
    report.check(isinstance(doc.get("name"), str) and doc["name"].strip() != "", path, "'name' is required")
    kind = doc.get("kind")
    if not report.check(kind in KINDS, path, f"'kind' must be one of {sorted(KINDS)} (got {kind!r})"):
        return None
    wants_monster = kind in ("monster", "fusion")
    monster = doc.get("monster")
    report.check((monster is not None) == wants_monster, path, "'monster' must be present exactly when kind is monster or fusion")
    report.check((doc.get("spell") is not None) == (kind == "spell"), path, "'spell' must be present exactly when kind is spell")
    report.check((doc.get("trap") is not None) == (kind == "trap"), path, "'trap' must be present exactly when kind is trap")
    report.check(is_int(doc.get("limit"), 0, 3), path, "'limit' must be 0–3 (April 2005 list)")
    tier = doc.get("tier")
    report.check(is_int(tier, 1, 4), path, "'tier' must be 1–4")
    effects = doc.get("effects", [])
    if report.check(isinstance(effects, list) and all(isinstance(e, str) for e in effects), path, "'effects' must be a list of ids"):
        for effect in effects:
            report.check(bool(SNAKE.match(effect)), path, f"effect id '{effect}' must be snake_case")
            report.check(effect in IMPLEMENTED_EFFECTS, path, f"tier {tier} effect '{effect}' is not implemented in Duel.Core")
        if tier is not None and tier != 1:
            report.check(len(effects) > 0, path, "a tier 2+ card names at least one effect id (stub or implemented)")
    if isinstance(monster, dict):
        report.check(isinstance(monster.get("type"), str) and monster["type"].strip() != "", path, "'monster.type' is required")
        report.check(monster.get("attribute") in ATTRIBUTES, path, f"'monster.attribute' must be one of {sorted(ATTRIBUTES)}")
        report.check(is_int(monster.get("level"), 1, 12), path, "'monster.level' must be 1–12")
        report.check(is_int(monster.get("atk"), 0), path, "'monster.atk' must be a non-negative integer")
        report.check(is_int(monster.get("def"), 0), path, "'monster.def' must be a non-negative integer")
        category = monster.get("category")
        report.check(category in CATEGORIES, path, f"'monster.category' must be one of {sorted(CATEGORIES)}")
        if category == "normal":
            report.check(not effects, path, "a normal monster has no effects")
        if kind == "fusion":
            report.check(category == "fusion", path, "a fusion card has category 'fusion'")
            materials = doc.get("materials")
            report.check(isinstance(materials, list) and len(materials) >= 2, path, "a fusion monster lists at least two 'materials'")
    elif wants_monster:
        pass
    spell = doc.get("spell")
    if isinstance(spell, dict):
        report.check(spell.get("subtype") in SPELL_SUBTYPES, path, f"'spell.subtype' must be one of {sorted(SPELL_SUBTYPES)}")
    trap = doc.get("trap")
    if isinstance(trap, dict):
        report.check(trap.get("subtype") in TRAP_SUBTYPES, path, f"'trap.subtype' must be one of {sorted(TRAP_SUBTYPES)}")
    return doc if ok else None


def validate_cards(report: Report, root: str) -> dict:
    cards: dict = {}
    directory = os.path.join(root, "cards")
    if not os.path.isdir(directory):
        report.fail(directory, "cards directory not found")
        return cards
    for name in sorted(os.listdir(directory)):
        if not name.endswith(".json"):
            continue
        path = os.path.join(directory, name)
        doc = load_json(report, path)
        if doc is None:
            continue
        card = validate_card(report, path, doc)
        if card is not None:
            if card["id"] in cards:
                report.fail(path, f"duplicate card id '{card['id']}'")
            cards[card["id"]] = card
    report.check(len(cards) > 0, directory, "no card files")
    return cards


def check_counts(report: Report, path: str, counts, cards: dict, field: str, fusion: bool) -> int:
    if not report.check(isinstance(counts, dict), path, f"'{field}' must map card ids to copy counts"):
        return 0
    total = 0
    for card_id, count in counts.items():
        if not report.check(card_id in cards, path, f"unknown card '{card_id}'"):
            continue
        card = cards[card_id]
        if not report.check(is_int(count, 1), path, f"{card['name']}: copy count must be at least 1"):
            continue
        total += count
        if card["limit"] == 0:
            report.fail(path, f"{card['name']} is Forbidden")
        else:
            report.check(count <= card["limit"], path, f"{card['name']}: {count} copies, limit {card['limit']}")
        if fusion:
            report.check(card["kind"] == "fusion", path, f"{card['name']} is not a Fusion monster")
        else:
            report.check(card["kind"] != "fusion", path, f"{card['name']} is a Fusion monster and belongs in the Fusion Deck")
    return total


def validate_decks(report: Report, root: str, cards: dict) -> dict:
    decks: dict = {}
    directory = os.path.join(root, "decks")
    if not os.path.isdir(directory):
        report.fail(directory, "decks directory not found")
        return decks
    for name in sorted(os.listdir(directory)):
        if not name.endswith(".json"):
            continue
        path = os.path.join(directory, name)
        doc = load_json(report, path)
        if not isinstance(doc, dict):
            if doc is not None:
                report.fail(path, "deck must be a JSON object")
            continue
        deck_id = doc.get("id")
        expected = os.path.splitext(name)[0]
        if report.check(isinstance(deck_id, str) and bool(SNAKE.match(deck_id)), path, f"'id' must be snake_case (got {deck_id!r})"):
            report.check(deck_id == expected, path, f"id '{deck_id}' does not match the file name '{expected}'")
        report.check(isinstance(doc.get("name"), str) and doc["name"].strip() != "", path, "'name' is required")
        main = check_counts(report, path, doc.get("main", {}), cards, "main", fusion=False)
        check_counts(report, path, doc.get("fusion", {}), cards, "fusion", fusion=True)
        report.check(MIN_DECK <= main <= MAX_DECK, path, f"main deck has {main} cards; {MIN_DECK}–{MAX_DECK} required")
        if isinstance(deck_id, str):
            decks[deck_id] = doc
    return decks


def validate_duelists(report: Report, root: str, decks: dict) -> None:
    path = os.path.join(root, "duelists.json")
    doc = load_json(report, path)
    if doc is None:
        return
    duelists = doc.get("duelists") if isinstance(doc, dict) else None
    if not report.check(isinstance(duelists, list) and len(duelists) > 0, path, "'duelists' must list at least one duelist"):
        return
    seen = set()
    for d in duelists:
        if not isinstance(d, dict):
            report.fail(path, "each duelist must be an object")
            continue
        did = d.get("id")
        where = f"duelist '{did}'"
        if report.check(isinstance(did, str) and bool(SNAKE.match(did)), path, f"duelist 'id' must be snake_case (got {did!r})"):
            report.check(did not in seen, path, f"duplicate duelist id '{did}'")
            seen.add(did)
        for field in ("name", "area"):
            report.check(isinstance(d.get(field), str) and d[field].strip() != "", path, f"{where}: '{field}' is required")
        report.check(d.get("deck") in decks, path, f"{where}: 'deck' {d.get('deck')!r} is not a known deck")
        flag = d.get("required_flag", "")
        report.check(isinstance(flag, str) and (flag == "" or bool(FLAG.match(flag))), path, f"{where}: 'required_flag' {flag!r} is not a flag id")
        profile = d.get("profile")
        if report.check(isinstance(profile, dict), path, f"{where}: 'profile' is required"):
            for key in ("board", "cards", "life", "risk", "jitter", "bluff_set"):
                report.check(isinstance(profile.get(key), (int, float)) and not isinstance(profile.get(key), bool), path, f"{where}: 'profile.{key}' must be a number")
            if isinstance(profile.get("jitter"), (int, float)):
                report.check(profile["jitter"] >= 0, path, f"{where}: 'profile.jitter' must be ≥ 0")
            if isinstance(profile.get("bluff_set"), (int, float)):
                report.check(0 <= profile["bluff_set"] <= 1, path, f"{where}: 'profile.bluff_set' must be within 0–1")
        for field in ("reward_first", "reward_rematch"):
            reward = d.get(field)
            if report.check(isinstance(reward, dict), path, f"{where}: '{field}' is required"):
                report.check(is_int(reward.get("coins"), 0) and is_int(reward.get("boosters"), 0), path, f"{where}: '{field}' coins and boosters must be integers ≥ 0")
        report.check(isinstance(d.get("ending", False), bool), path, f"{where}: 'ending' must be a boolean")


def validate_shop(report: Report, root: str, cards: dict) -> None:
    path = os.path.join(root, "shop.json")
    doc = load_json(report, path)
    if not isinstance(doc, dict):
        if doc is not None:
            report.fail(path, "shop must be a JSON object")
        return
    seen = set()
    for entry in doc.get("stock", []) if isinstance(doc.get("stock", []), list) else []:
        card_id = entry.get("card") if isinstance(entry, dict) else None
        if not report.check(card_id in cards, path, f"stock card {card_id!r} is unknown"):
            continue
        card = cards[card_id]
        report.check(card["limit"] >= 2, path, f"{card['name']} is Limited and only comes from boosters and rewards (GDD §5.2)")
        report.check(is_int(entry.get("price"), 1), path, f"{card['name']} needs a positive price")
        report.check(card_id not in seen, path, f"{card['name']} is listed twice in the stock")
        seen.add(card_id)
    boosters = doc.get("boosters")
    if not report.check(isinstance(boosters, list) and len(boosters) > 0, path, "at least one booster is required (duel rewards are boosters)"):
        return
    for b in boosters:
        if not isinstance(b, dict):
            report.fail(path, "each booster must be an object")
            continue
        bid = b.get("id")
        where = f"booster '{bid}'"
        report.check(isinstance(bid, str) and bool(SNAKE.match(bid)), path, f"booster 'id' must be snake_case (got {bid!r})")
        report.check(isinstance(b.get("name"), str) and b["name"].strip() != "", path, f"{where}: 'name' is required")
        report.check(is_int(b.get("price"), 1) and is_int(b.get("count"), 1), path, f"{where}: needs a positive price and count")
        report.check(is_int(b.get("limited_max", 0), 0), path, f"{where}: 'limited_max' must be ≥ 0")
        weights = b.get("weights")
        if report.check(isinstance(weights, dict) and len(weights) > 0, path, f"{where}: 'weights' by tier are required"):
            total = 0
            for tier, weight in weights.items():
                if report.check(tier in ("1", "2", "3", "4") and is_int(weight, 0), path, f"{where}: weights are keyed by tier 1–4 with non-negative values (got {tier!r}: {weight!r})"):
                    total += weight
            report.check(total > 0, path, f"{where}: weights must sum to more than 0")


def validate_avatar(report: Report, root: str) -> None:
    path = os.path.join(root, "avatar.json")
    doc = load_json(report, path)
    if not isinstance(doc, dict):
        if doc is not None:
            report.fail(path, "avatar must be a JSON object")
        return
    name = doc.get("name")
    if report.check(isinstance(name, dict), path, "'name' is required"):
        default = name.get("default")
        pattern = name.get("pattern")
        report.check(isinstance(default, str) and default != "", path, "'name.default' is required")
        report.check(is_int(name.get("min_length"), 1), path, "'name.min_length' must be ≥ 1")
        report.check(is_int(name.get("max_length"), name.get("min_length") if is_int(name.get("min_length")) else 1), path, "'name.max_length' must be ≥ min_length")
        if isinstance(pattern, str) and isinstance(default, str):
            try:
                report.check(re.match(pattern, default) is not None, path, f"the default name '{default}' does not match its own pattern")
            except re.error:
                report.fail(path, "'name.pattern' is not a valid regular expression")
        else:
            report.fail(path, "'name.pattern' is required")

    def id_list(field: str) -> list:
        values = doc.get(field)
        ok = isinstance(values, list) and len(values) > 0 and all(isinstance(v, str) and v.strip() != "" for v in values)
        report.check(ok, path, f"'{field}' must be a non-empty list of ids")
        return values if ok else []

    def colour_list(field: str) -> list:
        values = id_list(field)
        for v in values:
            report.check(bool(HEX.match(v)), path, f"'{field}' entry '{v}' is not a #rrggbb colour")
        return values

    body_types = id_list("body_types")
    skin_tones = colour_list("skin_tones")
    hair_colors = colour_list("hair_colors")
    accent_colors = colour_list("accent_colors")
    outfits = id_list("outfits")
    hair_styles = doc.get("hair_styles")
    if report.check(isinstance(hair_styles, dict), path, "'hair_styles' must map body types to style lists"):
        for body in body_types:
            styles = hair_styles.get(body)
            report.check(isinstance(styles, list) and len(styles) > 0, path, f"'hair_styles' needs at least one style for body type '{body}'")
    defaults = doc.get("defaults")
    if report.check(isinstance(defaults, dict), path, "'defaults' is required"):
        body = defaults.get("body_type")
        report.check(body in body_types, path, f"default body type {body!r} is not in 'body_types'")
        styles = hair_styles.get(body, []) if isinstance(hair_styles, dict) and body in body_types else []
        for field, count in (("skin_tone", len(skin_tones)), ("hair_style", len(styles)), ("hair_color", len(hair_colors)), ("outfit", len(outfits)), ("accent_color", len(accent_colors))):
            value = defaults.get(field)
            valid = "no options" if count == 0 else f"0–{count - 1}"
            report.check(is_int(value, 0, count - 1), path, f"default '{field}' {value!r} is out of range ({valid})")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", default="data", help="data directory (default: data)")
    args = parser.parse_args()
    report = Report()
    if not os.path.isdir(args.root):
        report.fail(args.root, "data directory not found")
    else:
        cards = validate_cards(report, args.root)
        decks = validate_decks(report, args.root, cards)
        validate_duelists(report, args.root, decks)
        validate_shop(report, args.root, cards)
        validate_avatar(report, args.root)
    status = "FAIL" if report.failures else "PASS"
    print(f"validate_data summary: {status}, {report.checks} checks, {report.failures} failures ({args.root})")
    return 1 if report.failures else 0


if __name__ == "__main__":
    sys.exit(main())
