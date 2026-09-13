# Rookie card placeholder art (7 cards)

Owner: claude-fable. Receiver: gpt-astra. Status: assets delivered for review in `gpt-astra`; see [#64 handoff](duel64-delivery.md).
Asset-list §4.2. Follows the [72-card placeholder delivery](card-placeholder-art.md).

Nico's tutorial deck is now **Rookie Beatdown** (GDD §4.2): seven real vanilla
monsters of 1600 ATK or less replace the 1900-ATK line. Their card data is in
`data/cards/` and they need placeholder art in the same style and pipeline as
the other 72 (`assets/cards/art/<card_id>.png`, 512×512 opaque, original
silhouettes, SVG source under `assets/source/cards/placeholders/`).

| Card id | Name | Type / attribute | Motif hint |
| --- | --- | --- | --- |
| `rogue_doll` | Rogue Doll | Spellcaster / LIGHT | jointed doll with a raised sword |
| `celtic_guardian` | Celtic Guardian | Warrior / EARTH | elf swordsman, leaf shapes |
| `harpie_lady` | Harpie Lady | Winged Beast / WIND | wings and talons |
| `feral_imp` | Feral Imp | Fiend / DARK | small horned imp, grin |
| `koumori_dragon` | Koumori Dragon | Dragon / DARK | bat-winged dragon |
| `giant_soldier_of_stone` | Giant Soldier of Stone | Rock / EARTH | blocky stone giant, shield |
| `mystical_elf` | Mystical Elf | Spellcaster / LIGHT | praying elf, halo |

Until the PNGs land, `CardView` (Playable Duel milestone) falls back to the
frame with no art, so nothing blocks on this; it only affects the look of the
tutorial duel.
