# Game Pitch

**Status:** draft for director approval · **Issue:** #1 · **Author:** claude-fable

## One line

A Pokémon-style city adventure where every encounter is a duel of
2005-era Yu-Gi-Oh.

## The fantasy

You arrive in a big, bright city as a new duelist. You pick a name, build
your look, and set out to explore: streets, shops, parks and landmarks, all
seen from the tilted top-down camera of *Pokémon Brilliant Diamond and
Shining Pearl*. Duelists are everywhere. Catch their eye and the world fades
into a duel table where you play a full match of Yu-Gi-Oh under **Goat
Format**, the community-preserved April 2005 rules and card pool. Win, earn
cards and reputation, tune your deck, and climb toward the city's
tournament.

## Core loop

1. **Explore** the city with a joystick, talk to people, find shops and
   duelists.
2. **Duel** any duelist you engage, playing a complete Goat Format match.
3. **Grow** by earning cards and currency, editing your deck, and unlocking
   new areas and stronger opponents.

## What makes it distinctive

- **Two beloved structures fused.** The Pokémon overworld and encounter
  rhythm with a real, complete trading card game in place of monster
  battles.
- **A frozen, finite card game.** Goat Format is a fixed card pool as of
  17 August 2005 with the April 2005 banlist and 2005-era rulings, curated
  at [goatformat.com](https://www.goatformat.com/). No Synchro, Xyz,
  Pendulum or Link monsters, no modern rule revisions. That makes the rules
  engine tractable and the metagame already balanced.
- **One character style.** Characters are anime-styled 3D models with
  realistic proportions everywhere: walking the streets, talking, and at the
  duel table. There is no chibi overworld variant.

## Player

The player creates their own avatar: chosen name, body type, hair, skin
tone and outfit, in the spirit of the Brilliant Diamond and Shining Pearl
trainer creator.

## References

| Aspect | Reference |
| --- | --- |
| Overworld camera, city layout, readability | *Pokémon Brilliant Diamond / Shining Pearl*, Hearthome City |
| Character model style (used everywhere) | *Brilliant Diamond / Shining Pearl* in-battle trainer models |
| Card game rules, pool and banlist | Goat Format, [goatformat.com](https://www.goatformat.com/) |
| Duel presentation | *Yu-Gi-Oh! Tag Force* and *Duel Links* table views |

## Platform and input

Desktop: Windows, macOS and Linux. Gamepad is the primary input, with
keyboard and mouse as a full fallback. Built in Godot with C#.

## Project nature and intellectual property

This is a **fan project**. Pokémon belongs to Nintendo, Creatures and Game
Freak; Yu-Gi-Oh belongs to Konami and Kazuki Takahashi's estate. We do not
ship their card art, character models or trademarks. Card art is placeholder
or original, characters and the city are original, and card names and text
are used only as needed to implement the rules. The code is MIT-licensed.

## Why we are building it

The proof of concept exists to answer one question: **can this team, a
human director and producer, an AI lead programmer and designer, and an AI
lead artist, produce a working and fun game together?** The game is the
vehicle. Success is a complete, playable, enjoyable vertical slice, not a
finished product. See [poc-scope.md](poc-scope.md).

A second, lasting outcome is an **asset library**: the character rig,
outfit pieces, city building kit, UI and audio made here are built to be
reused in the original-IP game that follows.
