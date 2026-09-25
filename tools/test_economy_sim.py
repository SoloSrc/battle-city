"""Offline tests for the economy simulation tool (issue #195)."""
import random
import unittest
from pathlib import Path

import economy_sim as sim

ROOT = Path(__file__).resolve().parent.parent / "data"


class FakeCard:
    def __init__(self, cid, rarity, limit):
        self.id, self.rarity, self.limit = cid, rarity, limit


class PriceTests(unittest.TestCase):
    def test_price_ladder(self):
        self.assertEqual(sim.base_price("common"), 100)
        self.assertEqual(sim.base_price("ultra_rare"), 1000)
        self.assertEqual(sim.base_price("prismatic_secret_rare"),
                         sim.base_price("secret_rare"))

    def test_sell_back_is_floored_quarter(self):
        self.assertEqual(sim.sell_price("rare"), 62)
        self.assertEqual(sim.sell_price("secret_rare"), 625)


class StockTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cards, cls.decks, cls.duelists, cls.boosters = sim.load_data(ROOT)

    def test_stock_is_open_rarities_with_limit_two_plus(self):
        stock = sim.stock_cards(self.cards)
        self.assertTrue(stock)
        for cid in stock:
            card = self.cards[cid]
            self.assertIn(sim.price_class(card.rarity), sim.STOCK_RARITIES)
            self.assertGreaterEqual(card.limit, 2)

    def test_case_pool_is_the_complement_of_stock(self):
        stock = sim.stock_cards(self.cards)
        case = sim.case_pool(self.cards, stock)
        self.assertEqual(set(case) | set(stock), set(self.cards))
        self.assertFalse(set(case) & set(stock))

    def test_secrets_never_in_packs(self):
        pool = sim.pack_pool(self.cards, self.boosters["street_pack"])
        self.assertNotIn("secret_rare", pool)
        drawable = {cid for ids in pool.values() for cid in ids}
        for cid, card in self.cards.items():
            if sim.price_class(card.rarity) == "secret_rare":
                self.assertNotIn(cid, drawable)


class PackTests(unittest.TestCase):
    def test_limited_max_holds(self):
        cards = {
            "lim_a": FakeCard("lim_a", "common", 1),
            "lim_b": FakeCard("lim_b", "common", 1),
            "open_c": FakeCard("open_c", "common", 3),
        }
        booster = {"count": 5, "limited_max": 1}
        pool = sim.pack_pool(cards, {"pool": list(cards), **booster})
        for seed in range(50):
            drawn = sim.open_pack(booster, pool, random.Random(seed), cards)
            self.assertEqual(len(drawn), 5)
            limited = sum(1 for cid in drawn if cards[cid].limit == 1)
            self.assertLessEqual(limited, 1)

    def test_weights_renormalise_to_pool_rarities(self):
        cards = {"only": FakeCard("only", "ultra_rare", 3)}
        booster = {"count": 3, "limited_max": 1}
        pool = sim.pack_pool(cards, {"pool": ["only"], **booster})
        drawn = sim.open_pack(booster, pool, random.Random(0), cards)
        self.assertEqual(drawn, ["only", "only", "only"])


class SimulationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = sim.load_data(ROOT)

    def test_deterministic_per_seed(self):
        cards, decks, duelists, boosters = self.data
        a = sim.simulate(cards, decks, duelists, boosters, "warrior_toolbox", 7)
        b = sim.simulate(cards, decks, duelists, boosters, "warrior_toolbox", 7)
        self.assertEqual(a, b)

    def test_stock_only_deck_finishes_fast(self):
        cards, decks, duelists, boosters = self.data
        result = sim.simulate(cards, decks, duelists, boosters, "rookie_beatdown", 0)
        self.assertLessEqual(result, 10)

    def test_pack_sell_back_stays_below_pack_price(self):
        cards, decks, duelists, boosters = self.data
        street = boosters["street_pack"]
        pool = sim.pack_pool(cards, street)
        self.assertLess(sim.pack_expected_sell_value(cards, street, pool),
                        street["price"])


if __name__ == "__main__":
    unittest.main()
