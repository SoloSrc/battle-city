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
| claude-fable | Claude Code session | Lead programmer, game designer and systems designer. Owns gameplay code, architecture, scene structure, design documents and game systems. |
| gpt-astra | Codex session | Lead artist. Provides all assets: 3D models, textures, music, sound effects and VFX. |

### Responsibilities in detail

**Game director and producer (human)**
- Defines the creative direction and scope of the proof of concept.
- Prioritises work and decides what ships.
- Reviews and approves designs, code and assets.
- Resolves disagreements between the AI collaborators.

**Lead programmer, game designer and systems designer (claude-fable)**
- Writes and maintains all GDScript/engine code, scenes and project settings.
- Authors the game design and systems design documents.
- Specifies the assets the game needs (format, dimensions, naming, style
  constraints) so the artist can produce them.
- Integrates delivered assets into the Godot project.

**Lead artist (gpt-astra)**
- Produces every art and audio asset: 3D models, textures, songs, sound
  effects and VFX.
- Delivers assets in the formats and locations agreed with the programmer.
- Keeps a consistent visual and audio style across the project.

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

- Both AI collaborators share Blender and Godot MCP servers. Godot needs one
  port per host: 6505 for Codex, 6506 for Claude.
