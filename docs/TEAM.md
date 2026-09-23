# Team and Project Context

This document records who is working on this project, what each member is
responsible for, and how we communicate. It is the first thing any
collaborator, human or AI, should read when joining a session.

## Company

**SOLOSRC**, a software development company.

## Project

A proof-of-concept game built in **Godot**, released under the
**MIT License** (see [LICENSE](../LICENSE)).

## Team and roles

| Member | Runs as | Role |
| --- | --- | --- |
| Vítor Torreão (human) | Desktop / terminal | Game director and producer. Owns the vision, sets priorities, makes final calls, and approves work. |
| claude-fable | Claude Code session | Lead programmer, game designer and systems designer. Owns gameplay code, architecture, reusable scene structure, design documents and game systems. |
| gpt-astra | Codex session | Lead artist and level designer. Provides all assets and builds playable levels in Blender and Godot using the tools and code written by claude-fable. |

### Responsibilities in detail

**Game director and producer (human)**
- Defines the creative direction and scope of the proof of concept.
- Prioritises work and decides what ships.
- Reviews and approves designs, code and assets.
- Resolves disagreements between the AI collaborators.

**Lead programmer, game designer and systems designer (claude-fable)**
- Writes and maintains gameplay and engine code, reusable scenes, development
  tools and project settings.
- Provides the tools, components and systems used by gpt-astra to build levels.
- Authors the game design and systems design documents.
- Specifies the assets the game needs (format, dimensions, naming, style
  constraints) so the artist can produce them.
- Owns gameplay integration and reusable asset components; coordinates with
  gpt-astra on their placement and configuration in level scenes.

**Lead artist and level designer (gpt-astra)**
- Produces every art and audio asset: 3D models, textures, songs, sound
  effects and VFX.
- Delivers assets in the formats and locations agreed with the programmer.
- Keeps a consistent visual and audio style across the project.
- Designs, builds and iterates on level layouts in Blender and Godot.
- Owns level scenes and their composition: geometry, traversal, encounter
  placement, lighting, environmental audio and VFX, within the approved design.
- Uses the tools, reusable scenes and code written by claude-fable to assemble
  and configure playable levels, and playtests them in Godot.
- Documents requests for missing tools or system changes for claude-fable,
  rather than independently changing shared gameplay architecture.

## Communication

- Each AI collaborator runs in its own session on the same machine. The
  human relays context between sessions through chat.
- Durable communication goes through the repository as **Markdown or HTML
  artifacts** (design docs, asset requests, progress notes). Anything that
  another collaborator needs to act on should be written to a file in the
  repo, not left only in chat.
- Suggested locations:
  - `docs/` for design documents, decisions and team notes.
  - `docs/requests/` for cross-role requests (for example, asset requests
    from the programmer to the artist).
  - `assets/` for delivered art and audio.

## Tooling notes

- **Blender MCP**: the [blender-mcp](https://github.com/ahujasid/blender-mcp)
  addon (`addon.py`) installed and enabled in Blender; the MCP server runs as
  `uvx blender-mcp` and talks to the addon's socket while Blender is open.
  The generator integrations in its panel are unused (the 3D-generation arm
  was dropped, docs/decisions.md 2026-09-22).
- **Godot MCP**: the `godot_mcp_bridge` editor plugin (committed under
  `addons/`, from [AkiraZ1/godot-mcp](https://github.com/AkiraZ1/godot-mcp))
  opens a local TCP port while the editor runs; a Node MCP server
  (`server/server.mjs` from a clone of that repo, outside the repo tree)
  bridges it to the agent, configured by `GODOT_PROJECT_PATH`,
  `GODOT_MCP_PORT` and `GODOT_BIN`.
- One Godot port per collaborator on the same host: 6505 for Codex, 6506 for
  Claude. Each collaborator sets `mcp_bridge/port` in an `override.cfg` in
  their own worktree root (Git-ignored); the committed project default stays
  8756. The agent's `GODOT_MCP_PORT` must match its worktree's override.
- MCP servers are per machine and per session: verify with a live status
  check before relying on them; documentation alone is not a connection.

## Repository layout and branching

- Remote: https://github.com/SoloSrc/battle-city (default branch `main`).
- Each AI collaborator works in its own git worktree on its own branch, so
  the two sessions never share a working directory:

  | Collaborator | Worktree path | Branch |
  | --- | --- | --- |
  | human | `Workspace/battle-city` | `main` |
  | claude-fable | `Workspace/battle-city-claude-fable` | `claude-fable` |
  | gpt-astra | `Workspace/battle-city-gpt-astra` | `gpt-astra` |

- Work flows from the collaborator branches into `main` through pull
  requests reviewed by the director. Follow the workflow preferences below
  when starting new work after a squash merge.
- Do not create additional worktrees on a branch that is already checked
  out elsewhere; git will refuse.

## Director's workflow preferences

These preferences travel with the repository and apply across sessions and
machines. They supersede earlier instructions to merge `main` back into an
accumulated collaborator branch after a squash merge.

- Keep each PR focused on its current task: one PR per issue, opened as one
  commit of new work on top of `main`. During review, push fixes as separate
  commits so the reviewer sees what changed since the last look; the director
  squash-merges, so `main` gets one commit either way. After opening or
  updating a PR, report and wait for the director's "Merged".
- After a squash merge, start the next task from the latest `origin/main`
  (`git fetch origin && git checkout -b <topic> origin/main`; a topic branch
  per task is fine). Do not carry already-merged commits into the next PR by
  merging main into the old branch history.
- Never move, reset or check out the local `main` branch from a
  collaborator's worktree. The worktrees share one repository, so that
  strands the director's checkout. Read `origin/main` instead.
- Never use bare `git stash` or `git stash pop`: the worktrees share one
  stash stack, and a pop can take another collaborator's changes. Use a
  temporary commit, or a tagged stash applied by its hash.
- Before realigning a branch, fetch, confirm the previous PR was merged,
  inspect the working tree and unmerged commits, preserve unfinished work,
  and keep a local backup ref. Never reset an active PR or discard work.
- When an intentional history rewrite requires a force push, use an explicit
  force-with-lease tied to the observed remote branch head. Never overwrite
  another collaborator's branch or edit their worktree.
- Astra's tests and committed visual evidence use the generated artwork.
  Do not download original card artwork into Astra's worktree unless the
  director requests it for a specific task. Optional player downloads stay
  Git-ignored and are never committed.
- Both AI collaborators act on GitHub through the director's login, so a
  review of the other collaborator's PR is `gh pr review --comment`. Approve
  and request-changes are refused on one's own PR. State the verdict in the
  comment.
- Commit the `.cs.uid` (and other `.uid`) sidecar files Godot writes next to
  new scripts. Left untracked, they block the director's next pull.
- Evidence images go under `docs/requests/evidence/<topic>/` and are embedded
  in PR bodies by commit-pinned raw URLs.

## Setting up on a new machine

The repository carries the rules; these facts belong to the machine and must
be checked again, not assumed:

- Create the three worktrees of the table above from one clone
  (`git worktree add`), each collaborator in its own.
- Godot 4.7 .NET and the .NET 8 SDK; `dotnet build BattleCity.sln` once, then
  the Build button in the editor, before the first run.
- The headless test commands in `tests/scenes/README.md` need
  `--fixed-fps 60`; without it the duel interface test reports false
  mismatches.
- The MCP setup of the Tooling notes: the Blender addon enabled and the
  Godot bridge's Node server cloned, with one Godot port per collaborator
  set in each worktree's `override.cfg`.
- Whether Blender can export glTF. On the director's Mac (macOS 13, Blender
  5.2.1) it could not, because the bundled NumPy targets a newer macOS. On
  the Linux machine (Blender 5.2.2, 2026-09-22) headless export works.
- Downloaded card art is per checkout and ignored by git; run
  `python3 tools/download_card_art.py` again if wanted.
- Agent memory and chat history do not move. Anything a collaborator must
  know belongs in this file, `AGENTS.md` or `docs/decisions.md`.
- The plan of record is `docs/roadmap.md`.
