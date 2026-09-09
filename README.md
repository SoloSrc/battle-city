# battle-city

A proof-of-concept game built in Godot by SOLOSRC: tilted top-down city
exploration in an anime style, with Yu-Gi-Oh! Goat Format duels in the
Battle City style.

- Team, roles and communication conventions: [docs/TEAM.md](docs/TEAM.md)
- Design, art and technical documents: [docs/](docs/)
- License: [MIT](LICENSE)

## Toolchain

| Tool | Version |
| --- | --- |
| Godot | 4.7.2 .NET (mono) |
| .NET SDK | 8.0 (`global.json`) |
| Blender | 4.x |

The repository root is the Godot project. See
[docs/tech/architecture.md](docs/tech/architecture.md) for the layout,
conventions and pipeline.

## Build, run and test

```bash
dotnet build
dotnet test tests/Duel.Core.Tests
godot --path .
```

`godot` is the Godot .NET binary; on macOS it is
`/Applications/Godot.app/Contents/MacOS/Godot`. Continuous integration runs
`dotnet format --verify-no-changes`, the build, the Duel.Core tests and a
headless Godot import on every pull request.
