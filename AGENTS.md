# Project instructions

Read [docs/TEAM.md](docs/TEAM.md) before starting work. It records the team,
responsibilities, communication conventions and worktree layout for SOLOSRC's
MIT-licensed Godot proof of concept.

## Roles and ownership

- **Human — game director and producer:** owns creative direction, scope,
  priorities, final decisions and review.
- **claude-fable — lead programmer, game designer and systems designer:** owns
  gameplay and engine code, architecture, reusable scenes, project settings,
  design documents and the tools and systems used to build levels.
- **gpt-astra — lead artist and level designer:** produces 3D models, textures,
  music, sound effects and VFX; designs and builds levels in Blender and Godot
  using the tools and code written by claude-fable.

Level scenes and their composition belong to gpt-astra. Shared gameplay code,
reusable components and development tools belong to claude-fable. Coordinate
changes that cross these boundaries through repository artifacts. Record
missing tool or system requirements for claude-fable. The director resolves
creative or ownership disagreements.

## Working directories

Work in the worktree assigned to your session:

| Collaborator | Worktree | Branch |
| --- | --- | --- |
| Human | `/Users/torreao/Workspace/battle-city` | `main` |
| claude-fable | `/Users/torreao/Workspace/battle-city-claude-fable` | `claude-fable` |
| gpt-astra | `/Users/torreao/Workspace/battle-city-gpt-astra` | `gpt-astra` |

Check the current branch and working-tree status before editing. Preserve
existing work and avoid editing another collaborator's worktree. Changes reach
`main` through pull requests reviewed by the director. After a merge, sync the
collaborator branch with `main` before continuing. Do not create another
worktree for a branch that is already checked out.

## Durable communication

Chat provides immediate context; Markdown or HTML files in the repository
carry decisions and handoffs between sessions.

- Use `docs/` for design documents, level plans, decisions and progress notes.
- Use `docs/requests/` for asset requests, tool requirements and cross-role
  handoffs. Include the owner, status, requirements, relevant paths and what
  the receiving collaborator needs to do.
- Use `assets/` for delivered art and audio, following the agreed project
  structure. Record asset origins and applicable licenses.
- Keep `docs/TEAM.md` and this file consistent when responsibilities change.

Separate worktrees do not automatically share uncommitted files. Identify the
branch and commit or pull request containing a handoff so the other session
can obtain the correct version.

## Blender and Godot

Before changing an editor session, verify the active project or file belongs
to the intended worktree. Coordinate use of shared Blender or Godot sessions
so one collaborator does not overwrite the other's in-progress work.

The documented Godot MCP ports are 6505 for Codex and 6506 for Claude. Verify
tool availability and connectivity in the current session; documentation alone
does not establish a live connection.

Use the agreed engine version, asset specifications and reusable gameplay
components. Playtest level changes in Godot when possible and record what was
checked, along with any missing tools or blockers. Keep editable asset sources
and runtime exports identifiable, and preserve import settings needed to
reproduce the result.
