# Roadmap

**Status:** approved by the director, 2026-09-20 · **Author:** claude-fable

The proof of concept is playable: a greybox district, three duelists and
complete Goat Format duels against the AI. This page records the path from
there to a full game, and where the work is tracked.

## The ten steps

Each step makes the next one cheaper. Steps 1 and 3 run in parallel and are
the two open milestones.

1. **Vertical slice.** One district at the quality of the concept art: art,
   lighting, music, one full duel with every effect, and a settled duel
   camera. Sets the quality bar and the real cost per district.
2. **Complete duel rules.** From the 79-card subset to the full Goat Format
   pool, the missing mechanics and timing cases, and AI opponents at several
   skill levels with distinct decks. The rules test suite grows with it.
3. **Game loop.** Deck editor, collection, shop and booster packs, currency
   and duel rewards. Without these, winning a duel changes nothing.
4. **Save and progression.** Save files, player profile, avatar creator,
   story flags, unlocks that gate districts and opponents.
5. **Shell.** Main menu, settings (audio, video, control rebinding), pause,
   a tutorial that teaches the duel rules, full gamepad support.
6. **Content.** The remaining districts, duelists with dialogue and decks, a
   story or tournament structure, side activities.
7. **Audio and polish.** Full soundtrack, barks, summon animations for key
   monsters, interface animation, accessibility options.
8. **Performance and platforms.** Profiling, export presets, installers,
   store integration.
9. **Playtest and balance.** Outside playtesters; tune AI difficulty, the
   economy and the tutorial from the results.
10. **The legal question, decided early.** The game uses Yu-Gi-Oh cards,
    names and art, which belong to Konami. A public release needs a licence
    or original cards and rules text. The generated-art fallback covers the
    art only. The decision shapes steps 2 and 6.

## How the art reaches the concept art

None of the present assets are production quality; they are greybox. The kit,
animations and district were written as Blender Python scripts with speed as
the goal; whether careful hand modelling in Blender can reach the concept art
has not been tested.

The director chose **hand-built art by gpt-astra in Blender** — modelling,
texturing and rigging from the approved input sheets — with an anime shading
stack by claude-fable. 3D generation from the concept art (Hunyuan3D and
Hyper3D Rodin) was considered and dropped: the services cost money the
project does not have (docs/decisions.md, 2026-09-22).

Before any production art there is a **benchmark**: one character, one shop
front and one street corner, compared side by side with the concept art. The
director's approval of the benchmark is a gate
([#110](https://github.com/SoloSrc/battle-city/issues/110)).
If characters fail it twice, characters switch to an anime base model. After
the gate, [#111](https://github.com/SoloSrc/battle-city/issues/111)
records the method that passed and re-splits the production issues from
measured hours, so the production estimates below are provisional until then.

## Milestone 1: [Vertical slice](https://github.com/SoloSrc/battle-city/milestone/4)

Step 1. Tasks are sized at about two hours. Each phase has a tracking issue
whose sub-issues are the tasks.

| Phase | Tracking issue | Tasks | Waits for the gate |
| --- | --- | --- | --- |
| Phase 0: Pipeline | [#94](https://github.com/SoloSrc/battle-city/issues/94) | #95 to #97 (3) | no |
| Phase 1: Benchmark | [#98](https://github.com/SoloSrc/battle-city/issues/98) | #99 to #111 (13) | ends with it |
| Phase 2: Production, characters and duel disk | [#112](https://github.com/SoloSrc/battle-city/issues/112) | #113 to #129 (17) | art tasks yes; [#129](https://github.com/SoloSrc/battle-city/issues/129) (disk arm) no |
| Phase 2: Production, environment | [#130](https://github.com/SoloSrc/battle-city/issues/130) | #131 to #143 (13) | yes |
| Phase 2: Production, audio | [#144](https://github.com/SoloSrc/battle-city/issues/144) | #145 to #151 (7) | no |
| Phase 2: Production, duel presentation and interface | [#152](https://github.com/SoloSrc/battle-city/issues/152) | #153 to #159, [#67](https://github.com/SoloSrc/battle-city/issues/67), [#88](https://github.com/SoloSrc/battle-city/issues/88), [#89](https://github.com/SoloSrc/battle-city/issues/89) (10) | no |
| Phase 3: Acceptance | [#160](https://github.com/SoloSrc/battle-city/issues/160) | #161 to #163 (3) | after production |

Start here: [#95](https://github.com/SoloSrc/battle-city/issues/95)
makes the asset path work end to end (Blender to glb to Godot) and documents
it; [#97](https://github.com/SoloSrc/battle-city/issues/97) gives the
benchmark its side-by-side captures.

## Milestone 2: [Game loop](https://github.com/SoloSrc/battle-city/milestone/5)

Step 3. The data layer exists already (collection, booster draw, deck rules,
shop data, save); this milestone is the screens and rules on top of it. None
of it waits for the art gate.

| Phase | Tracking issue | Tasks |
| --- | --- | --- |
| Phase 0: Design | [#164](https://github.com/SoloSrc/battle-city/issues/164) | #165 to #166 (2) |
| Phase 1: Interface foundation | [#167](https://github.com/SoloSrc/battle-city/issues/167) | #168 to #174 (7) |
| Phase 2: Deck editor | [#175](https://github.com/SoloSrc/battle-city/issues/175) | #176 to #180 (5) |
| Phase 2: Collection | [#181](https://github.com/SoloSrc/battle-city/issues/181) | #182 to #183 (2) |
| Phase 2: Shop and packs | [#184](https://github.com/SoloSrc/battle-city/issues/184) | #185 to #191 (7) |
| Phase 2: Rewards and economy | [#192](https://github.com/SoloSrc/battle-city/issues/192) | #193 to #196 (4) |
| Phase 3: Art, sound and acceptance | [#197](https://github.com/SoloSrc/battle-city/issues/197) | #198 to #202 (5) |

[#165](https://github.com/SoloSrc/battle-city/issues/165)
comes first: it decides sell-back, deck slots, the pack line-up and the coin
curve, which the design documents do not have today.

## Board

All of the issues are on the [project board](https://github.com/users/SoloSrc/projects/4)
with their milestone and parent issue. Steps 2 and 4 to 10 get milestones
when the director opens them.
