# Decision Log

One line per notable decision. Newest at the bottom. Anyone may append;
do not edit past entries, add a new one that supersedes it.

Format: `| date | decision | rationale | decided by |`

| Date | Decision | Rationale | Decided by |
| --- | --- | --- | --- |
| 2026-09-07 | Project is MIT-licensed, copyright SOLOSRC | Open proof of concept; company holds the copyright | director |
| 2026-09-07 | Roles: director/producer (human), lead programmer + game and systems designer (claude-fable), lead artist (gpt-astra) | Three-party team with clear ownership | director |
| 2026-09-07 | GitHub account SoloSrc hosts the repo; it is a user account, not an org | Existing account | director |
| 2026-09-07 | One git worktree and branch per collaborator; work reaches `main` via pull requests merged by the director | Sessions never share a working directory; director approves everything | director, claude-fable |
| 2026-09-07 | gpt-astra is also level designer and owns `levels/`; claude-fable owns tools, components and shared code | Artist composes levels with programmer's tools; recorded in AGENTS.md | director |
| 2026-09-07 | Engine: Godot 4.7 .NET with C# | Team skill and .NET tooling; installed on the host | director |
| 2026-09-07 | Fan project inspired by Pokémon BDSP exploration with Yu-Gi-Oh Goat Format duels; original IP comes after the PoC | The PoC tests the team, not the concept | director |
| 2026-09-07 | Goat Format rules, card pool (17 Aug 2005) and April 2005 banlist, per goatformat.com, including 2005-era rulings | Frozen, balanced, tractable ruleset | director |
| 2026-09-07 | One character model style everywhere: realistic-proportion anime 3D, never chibi | Less art burden; reusable asset library | director |
| 2026-09-07 | Player avatar is customisable with a chosen name | BDSP-style creator | director |
| 2026-09-07 | Duels happen in the overworld, standing, with duel disks; cards float as holograms; no arena scene | Battle City anime presentation | director |
| 2026-09-07 | PoC shows floating cards only; 3D monster holograms are post-PoC | Scope | director |
| 2026-09-07 | Card face uses the 4Kids-era anime layout with no name or text box; names and text live in the UI | Art-first look; one frame template | director |
| 2026-09-07 | Third-party reference images are linked, never committed; all committed drawings are original | Copyright | claude-fable, director |
| 2026-09-07 | Platform: desktop Windows, macOS, Linux; gamepad primary, keyboard fallback | Cheapest to ship from Godot | claude-fable (assumed), director (by merging #9) |
| 2026-09-07 | Slice: one district, three duelists, one shop, 72-card curated subset, 45–90 min | PoC scope | director (merged #11) |
| 2026-09-07 | Losing a duel costs nothing but time; boosters remain the duel reward | Friendly slice; non-deterministic progression accepted | director |
| 2026-09-07 | Squash merges to `main`; collaborator branches merge `main` back rather than rebasing | Avoids force pushes across worktrees | claude-fable |
| 2026-09-08 | Camera acceptance metric is avatar height 100 px at 1080p (90–120), tuned within the GDD's 12–14 m distance | Resolves sheet 01 inconsistency raised by gpt-astra | claude-fable, pending director |
| 2026-09-08 | Ten card anchors per side are generated from the stand points, independent of the disk prop's bay spacing | Cards stay selectable regardless of prop detail | claude-fable |
| 2026-09-08 | Card art is 512² centre-cropped to a 4:5 window with an 80 % safe column | Square art, portrait frame | claude-fable, pending director |
| 2026-09-08 | Duel engine is a Godot-free C# library driven by commands and events, tested with xUnit | Testability and determinism | claude-fable |
