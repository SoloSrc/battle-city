# Proof of Concept Definition

**Status:** draft for director approval · **Issue:** #2 · **Author:** claude-fable

## What we are proving

The proof of concept tests the **team**, not the concept. The question is
whether a human director and producer, an AI lead programmer and designer,
and an AI lead artist can ship a working, fun game together through a
repository-based workflow.

The game described in [pitch.md](pitch.md) is the vehicle.

## The playable slice

A player can:

1. Start a new game, choose a name and customise an avatar.
2. Walk around **one city district** with a gamepad or keyboard, with
   collision, a tilted top-down camera and at least one interior (a card
   shop).
3. Talk to non-player characters through a dialogue box.
4. Be challenged by, or challenge, **at least three duelists** of
   increasing difficulty, each with a distinct Goat Format deck.
5. Play a **complete Goat Format duel** against an AI opponent: draw,
   main phases, battle phase, end phase, life points, summons, sets, spells,
   traps, flips, chains and the win and lose conditions.
6. Win cards and currency, buy cards at the shop, and edit a deck within
   the format's rules and banlist.
7. Beat the final duelist and see an ending screen.

## Card pool for the slice

The Goat Format pool has roughly two thousand cards. The slice ships a
**curated subset of 60 to 80 cards** chosen to support three to four
recognisable Goat decks (for example Goat Control, Chaos, and a beatdown
deck), plus the staples they share. The duel engine is built so cards are
data and the remaining pool can be added later without engine changes.

Every card in the subset must be implemented completely and correctly
under 2005 rulings.

## Explicitly out of scope

- The full Goat Format card pool.
- Multiplayer or online play.
- More than one city district, or any world outside the city.
- Story beyond a short framing and the three duelists.
- Voice acting.
- Console or mobile builds.
- Final card art. Placeholder frames with name and text are acceptable.
- Localisation.
- Save slots beyond a single autosave.

## Questions the slice must answer

| Question | Success criterion |
| --- | --- |
| Can the team ship? | The slice above is complete, runs from a clean clone, and has no known crash. |
| Is it fun? | The director and at least two external testers finish the slice and want to keep playing. |
| Does the workflow work? | Every feature and asset went through an issue, a branch and a reviewed pull request. No collaborator was blocked more than one working day waiting on another. |
| Does the duel engine hold? | All slice cards pass automated rule tests, and no illegal game state can be reached in play. |
| Is the art pipeline reusable? | One rig drives the avatar and every non-player character. Adding a new outfit or building needs no code change. |

## Definition of done

The slice is done when every item in "The playable slice" is checked,
every criterion in the table is met, and the director signs off on the
`main` branch.
