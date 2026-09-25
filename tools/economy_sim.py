#!/usr/bin/env python3
"""Economy simulation tool (issue #195).

Simulates a player who duels, opens packs, buys and sells under the
economy rules of docs/design/systems.md §8.2 (rarity-based prices and
pack weights, issue #165) and reports how many duels it takes to finish
each reference deck of data/decks/.

The §8.2 design constants live at the top of this file so the tuning
pass (#196) edits one place. Pack pools and duelist reward packs are
read from the data files when present (`pool` on a booster, `pack` on a
reward, issue #189); until then every reward pack is the Street Pack.

Player model (documented in the report): starts with the starter deck
and 500 coins, wins every duel (d1, d2, d3 first, then rematches d3),
opens every reward pack, sells copies no kept deck needs, buys needed
singles from open stock, buys the display case when it shows a needed
card, and spends coins above a reserve (the priciest needed
case-only card) on extra Street Packs while pack-drawable cards are
still missing. Deterministic per seed.

Usage:
  python3 tools/economy_sim.py --root data [--seeds 500] [--check] [--markdown]
"""

from __future__ import annotations

import argparse
import json
import random
import statistics
import sys
from pathlib import Path

# --- §8.2 design constants (single place for #196 to tune) -------------

BASE_PRICE = {
    "common": 100,
    "rare": 250,
    "super_rare": 500,
    "ultra_rare": 1000,
    "secret_rare": 2500,
    "prismatic_secret_rare": 2500,
}
# Rarities priced as another's base share its channels below.
PRICE_CLASS = {
    "short_print": "common",
    "super_short_print": "common",
    "ultimate_rare": "ultra_rare",
    "prismatic_secret_rare": "secret_rare",
}
STOCK_RARITIES = {"common", "rare", "super_rare"}  # and limit >= 2
PACK_WEIGHTS = {"common": 55, "rare": 30, "super_rare": 12, "ultra_rare": 3}
DISPLAY_CASE_MULTIPLIER = 3
SELL_FRACTION = 0.25
STARTING_COINS = 500
STARTER_DECK = "starter"
MAX_DUELS = 500

# §8.2 / GDD §5.4 targets for --check. The starter already covers the
# beatdown list, so the "competitive second deck" the target means is
# Warrior Toolbox, the first deck the player must actually build.
TARGET_SECOND_DECK = "warrior_toolbox"
TARGET_SECOND_DECK_MIN = 20
TARGET_SECOND_DECK_MAX = 30


def price_class(rarity: str) -> str:
    return PRICE_CLASS.get(rarity, rarity)


def base_price(rarity: str) -> int:
    return BASE_PRICE[price_class(rarity)]


def sell_price(rarity: str) -> int:
    return int(SELL_FRACTION * base_price(rarity))


# --- data loading -------------------------------------------------------


class Card:
    def __init__(self, data: dict):
        self.id = data["id"]
        self.rarity = data["rarity"]
        self.limit = data["limit"]


def load_data(root: Path):
    cards = {}
    for path in sorted((root / "cards").glob("*.json")):
        card = Card(json.loads(path.read_text()))
        cards[card.id] = card
    decks = {}
    for path in sorted((root / "decks").glob("*.json")):
        deck = json.loads(path.read_text())
        needs = dict(deck.get("main", {}))
        for cid, n in deck.get("fusion", {}).items():
            needs[cid] = needs.get(cid, 0) + n
        decks[deck["id"]] = {"name": deck["name"], "needs": needs}
    duelists = json.loads((root / "duelists.json").read_text())["duelists"]
    shop = json.loads((root / "shop.json").read_text())
    boosters = {b["id"]: b for b in shop["boosters"]}
    return cards, decks, duelists, boosters


def stock_cards(cards: dict) -> dict:
    """Open stock per §8.2: common/rare/super with limit >= 2, at base price."""
    return {
        c.id: base_price(c.rarity)
        for c in cards.values()
        if price_class(c.rarity) in STOCK_RARITIES and c.limit >= 2
    }


def case_pool(cards: dict, stock: dict) -> list:
    """Display case pool: every card without a stock entry."""
    return sorted(cid for cid in cards if cid not in stock)


def pack_pool(cards: dict, booster: dict) -> dict:
    """rarity price-class -> list of drawable card ids for this booster."""
    ids = booster.get("pool") or list(cards)
    by_rarity: dict[str, list] = {}
    for cid in ids:
        card = cards[cid]
        if card.limit <= 0:
            continue
        cls = price_class(card.rarity)
        if cls in PACK_WEIGHTS:
            by_rarity.setdefault(cls, []).append(card.id)
    for pool in by_rarity.values():
        pool.sort()
    return by_rarity


# --- simulation ---------------------------------------------------------


def open_pack(booster: dict, by_rarity: dict, rng: random.Random, cards: dict):
    """Draw booster["count"] cards per §8.2: rarity by weight (restricted to
    rarities the pool contains, renormalised), uniform card of that rarity,
    at most limited_max Limited cards per pack."""
    rarities = [r for r in PACK_WEIGHTS if r in by_rarity]
    weights = [PACK_WEIGHTS[r] for r in rarities]
    limited_max = booster.get("limited_max", 1)
    drawn, limited = [], 0
    for _ in range(booster.get("count", 5)):
        rarity = rng.choices(rarities, weights=weights)[0]
        pool = by_rarity[rarity]
        if limited >= limited_max:
            pool = [cid for cid in pool if cards[cid].limit > 1]
            if not pool:
                pool = by_rarity[rarity]
        cid = rng.choice(pool)
        if cards[cid].limit == 1:
            limited += 1
        drawn.append(cid)
    return drawn


class Player:
    def __init__(self, cards, decks, target: str):
        self.cards = cards
        self.coins = STARTING_COINS
        self.owned: dict[str, int] = {}
        starter = decks[STARTER_DECK]["needs"]
        for cid, n in starter.items():
            self.owned[cid] = self.owned.get(cid, 0) + n
        target_needs = decks[target]["needs"]
        # Copies to keep: the starter stays a saved deck, the target is built.
        self.keep = dict(starter)
        for cid, n in target_needs.items():
            self.keep[cid] = max(self.keep.get(cid, 0), n)
        self.target_needs = target_needs

    def missing(self) -> dict:
        return {
            cid: n - self.owned.get(cid, 0)
            for cid, n in self.target_needs.items()
            if self.owned.get(cid, 0) < n
        }

    def gain(self, cid: str):
        self.owned[cid] = self.owned.get(cid, 0) + 1

    def sell_surplus(self, stock: dict):
        for cid, have in self.owned.items():
            keep = self.keep.get(cid, 0)
            if have > keep:
                self.coins += (have - keep) * sell_price(self.cards[cid].rarity)
                self.owned[cid] = keep


def simulate(cards, decks, duelists, boosters, target: str, seed: int):
    """One playthrough; returns duels to complete the target deck."""
    rng = random.Random(seed)
    player = Player(cards, decks, target)
    stock = stock_cards(cards)
    case = case_pool(cards, stock)
    pools = {bid: pack_pool(cards, b) for bid, b in boosters.items()}
    street = boosters["street_pack"]
    first_wins = list(duelists)
    grind = duelists[-1]  # rematch the last duelist: best rewards

    # Which missing cards can come out of a pack at all.
    def pack_drawable(cid):
        return price_class(cards[cid].rarity) in PACK_WEIGHTS

    for duel in range(1, MAX_DUELS + 1):
        duelist = first_wins.pop(0) if first_wins else grind
        reward = duelist["reward_first"] if duel <= len(duelists) else duelist["reward_rematch"]
        player.coins += reward["coins"]
        if reward.get("card"):
            player.gain(reward["card"])
        pack_id = reward.get("pack", "street_pack")
        if pack_id not in boosters:
            pack_id = "street_pack"
        for _ in range(reward.get("boosters", 0)):
            for cid in open_pack(boosters[pack_id], pools[pack_id], rng, cards):
                player.gain(cid)

        # Display case rerolls after every duel.
        shown = rng.choice(case)

        # Shopping phase.
        player.sell_surplus(stock)
        missing = player.missing()
        if not missing:
            return duel

        case_price = DISPLAY_CASE_MULTIPLIER * base_price(cards[shown].rarity)
        if shown in missing and player.coins >= case_price:
            player.coins -= case_price
            player.gain(shown)
            missing = player.missing()
            if not missing:
                return duel

        for cid in sorted(missing, key=lambda c: stock.get(c, 1 << 30)):
            if cid not in stock:
                continue
            while player.owned.get(cid, 0) < player.target_needs[cid] and player.coins >= stock[cid]:
                player.coins -= stock[cid]
                player.gain(cid)
        missing = player.missing()
        if not missing:
            return duel

        # Reserve enough to buy the priciest missing card off the case,
        # spend the rest on extra packs while packs can still help.
        case_only = [cid for cid in missing if cid not in stock]
        reserve = max(
            (DISPLAY_CASE_MULTIPLIER * base_price(cards[cid].rarity) for cid in case_only),
            default=0,
        )
        if any(pack_drawable(cid) for cid in missing):
            while player.coins - street["price"] >= reserve:
                player.coins -= street["price"]
                for cid in open_pack(street, pools["street_pack"], rng, cards):
                    player.gain(cid)
                player.sell_surplus(stock)
                if not player.missing():
                    return duel

    return MAX_DUELS + 1


def pack_expected_sell_value(cards, booster, by_rarity) -> float:
    """Expected sell-back of one pack (ignores limited_max, negligible)."""
    rarities = [r for r in PACK_WEIGHTS if r in by_rarity]
    total = sum(PACK_WEIGHTS[r] for r in rarities)
    per_card = sum(PACK_WEIGHTS[r] / total * sell_price(r) for r in rarities)
    return booster.get("count", 5) * per_card


# --- report -------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default="data", type=Path)
    parser.add_argument("--seeds", default=500, type=int, help="playthroughs per deck")
    parser.add_argument("--decks", nargs="*", help="deck ids (default: all but the starter)")
    parser.add_argument("--check", action="store_true", help="exit 1 when a §8.2 target fails")
    parser.add_argument("--markdown", action="store_true", help="print the design-doc table")
    args = parser.parse_args()

    cards, decks, duelists, boosters = load_data(args.root)
    targets = args.decks or [d for d in decks if d != STARTER_DECK]
    for deck_id in targets:
        if deck_id not in decks:
            sys.exit(f"economy_sim: unknown deck '{deck_id}'")

    stock = stock_cards(cards)
    street = boosters["street_pack"]
    street_pool = pack_pool(cards, street)

    rows = []
    for deck_id in targets:
        results = [
            simulate(cards, decks, duelists, boosters, deck_id, seed)
            for seed in range(args.seeds)
        ]
        results.sort()
        missing0 = Player(cards, decks, deck_id).missing()
        rows.append({
            "deck": decks[deck_id]["name"],
            "id": deck_id,
            "cards": sum(missing0.values()),
            "case_only": sum(
                n for cid, n in missing0.items()
                if cid not in stock and price_class(cards[cid].rarity) not in PACK_WEIGHTS
            ),
            "p10": results[int(0.10 * (len(results) - 1))],
            "median": int(statistics.median(results)),
            "p90": results[int(0.90 * (len(results) - 1))],
            "capped": sum(1 for r in results if r > MAX_DUELS),
        })

    ev = pack_expected_sell_value(cards, street, street_pool)
    failures = []
    if ev >= street["price"]:
        failures.append(
            f"pack expected sell-back {ev:.0f} >= pack price {street['price']} (arbitrage pays)")
    second = next((r for r in rows if r["id"] == TARGET_SECOND_DECK), None)
    if second and not (TARGET_SECOND_DECK_MIN <= second["median"] <= TARGET_SECOND_DECK_MAX):
        failures.append(
            f"second deck ({second['id']}) median {second['median']} duels outside "
            f"{TARGET_SECOND_DECK_MIN}-{TARGET_SECOND_DECK_MAX}")
    for row in rows:
        if row["capped"]:
            failures.append(f"{row['id']}: {row['capped']} runs never finished in {MAX_DUELS} duels")

    if args.markdown:
        print("| Deck | Copies missing | Case-only copies | Duels (median) | p10 | p90 |")
        print("| --- | --- | --- | --- | --- | --- |")
        for row in rows:
            print(f"| {row['deck']} | {row['cards']} | {row['case_only']} | "
                  f"{row['median']} | {row['p10']} | {row['p90']} |")
        print()
    else:
        for row in rows:
            print(f"{row['id']}: {row['cards']} copies missing ({row['case_only']} case-only), "
                  f"median {row['median']} duels (p10 {row['p10']}, p90 {row['p90']})")
    print(f"street pack: expected sell-back {ev:.0f} of {street['price']} coins")
    for failure in failures:
        print(f"economy_sim TARGET MISS: {failure}")
    verdict = "FAIL" if failures else "PASS"
    print(f"economy_sim summary: {verdict}, {len(targets)} decks x {args.seeds} seeds, "
          f"{len(failures)} target misses")
    return 1 if (failures and args.check) else 0


if __name__ == "__main__":
    sys.exit(main())
