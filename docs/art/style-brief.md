# Style Brief

**Status:** approved by the director (merged #10); input to the art direction · **Author:** claude-fable

This brief translates the director's references into original, annotated
concept sheets that the lead artist can build from. The reference images
are linked, not copied: they are third-party copyrighted works and must
never be committed to this repository. Everything in `style/` is our own
drawing and is MIT-licensed with the rest of the project.

## What we are after, in one paragraph

A bright, clean, anime-styled 3D city seen from a tilted top-down camera,
populated by realistically proportioned characters who duel in the street
with duel disks on their arms. The visual language is 2000s TV anime
rendered in 3D: cel shading, saturated flat colour, thin outlines, simple
readable shapes. Nothing is chibi. Nothing is photoreal.

## Concept sheets

| Sheet | What it fixes | Feeds |
| --- | --- | --- |
| [01 Overworld camera and scale](style/01-overworld-camera.svg) | Camera pitch, distance, framing, unit scale, building style | Environment kit, camera code |
| [02 Character proportions and rig](style/02-character-proportions.svg) | Head-unit proportions, face style, shared rig, swappable parts, budgets, animation list | Avatar system, every NPC |
| [03 Duel staging in the overworld](style/03-duel-staging.svg) | Where duelists stand, zone layout, duel camera, transition, holographic card look | Duel scene, VFX, camera code |
| [04 Duel disk](style/04-duel-disk.svg) | Original disk design with the functional parts of the anime's device | Prop, animation, card anchors |
| [05 Card face layout](style/05-card-layout.svg) | Anime-style card face with no text, frame colours, icons, delivery specs | Card renderer, frame and glyph art |

Numbers on the sheets are starting values. The programmer tunes camera and
scale in engine; the artist proposes changes to proportions, colour and
shape through pull requests against these sheets.

## External references (do not commit)

| Aspect | Reference | Take from it | Do not take |
| --- | --- | --- | --- |
| Overworld | Pokémon Brilliant Diamond / Shining Pearl, Hearthome City ([image](https://static0.thegamerimages.com/wordpress/wp-content/uploads/2021/11/pokemon-brilliant-diamond-shining-pearl-hearthome-city-walkthrough.jpg)) | Camera angle, street and building readability, colour saturation, soft shadows | Any building, sign, prop or character |
| Character models | BDSP in-battle trainer models ([image](https://images-wixmp-ed30a86b8c4ca887773594c2.wixmp.com/f/0bd0ac1d-c2e2-4a1c-8ac0-b6dfc0c43e5a/dev72h9-1d010e87-719a-401d-9985-699ab583a47c.png)) | Proportions, face style, cel shading, matte materials | Outfits, logos, specific characters |
| What we are not doing | BDSP overworld chibi models ([image](https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTM0xAXPh_qUTsjZKBf93yT_3PRpev-LkQBXeJFMtYzWDm2qdvndiQipAU6&s=10)) | Nothing. Shown only to mark the style we reject | Everything |
| Duel staging | Yu-Gi-Oh! Duel Monsters anime, Battle City arc ([image](https://static.wikia.nocookie.net/yugioh/images/b/b4/Yugioh136.jpg)) | Standing duels, disk on the arm, holograms in front of the duelist, low dramatic camera | Characters, monsters, card art |
| Duel disk | Battle City duel disk diagram ([image](https://preview.redd.it/battle-city-duel-disk-explanation-and-maybe-a-small-rant-v0-ygnncd95pt4g1.png)) | Functional parts: deck holder, life counter, graveyard, five zones on the blade | The exact silhouette, the KaibaCorp mark, colourways |
| Card face | 4Kids-era anime card ([image](https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTMsVZ86ILY7QsgRaHdLx_BUiqZMTnXDE7cecho0lOwsA&s=10)) | Art-first layout, stars, attribute icon, ATK/DEF boxes, no text | The art, the exact frame ornament |
| Card face, rejected | Official TCG card ([image](https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSyduKUWkgtmlTZhZngtOTFzu2fFGSeYBHsQ9KcepuClg&s=10)) | Nothing. Shown to mark the style we reject | Everything |

## Copyright rules for this project

1. Never commit a third-party image, model, sound or font. Link to it.
2. Never trace, retopologise or re-texture a third-party asset.
3. Original designs may share a *function* with a reference (a card has
   stars and two numbers; a disk has five slots) but not its *shape*,
   ornament, mark or palette.
4. Card names and rules text from Goat Format are used in code and UI only
   as needed to implement the game.
5. If in doubt, ask the director before committing.

## What the artist should produce first

1. A painted key visual of one street corner with two duelists mid-duel, in
   the target style, from the duel camera on sheet 03.
2. A character turnaround for one body type built to sheet 02.
3. The card frame set from sheet 05 with the seven attribute icons and the
   spell and trap glyphs.
4. A duel disk model to sheet 04 in folded and deployed states.

These four pieces are enough for the programmer to build the avatar
system, the duel scene and the card renderer against real assets.
