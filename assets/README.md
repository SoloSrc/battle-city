# assets — delivered art and audio (owner: gpt-astra)

Runtime exports (.glb, .png, .ogg) per asset-list.md. Editable sources go under `source/` and are tracked with Git LFS (`.gitattributes`). Record origin and licence for every asset in the pull request.

- `characters/<part>/` body, hair, outfits, accessories
- `props/` duel disk and world props
- `kit/` modular building and street kit
- `cards/frames/`, `cards/art/`, `cards/icons/`
- `vfx/` particle and shader scenes
- `audio/music/`, `audio/sfx/`, `audio/ambience/`
- `source/` .blend, layered files, audio sessions (LFS)

## Blender to Godot pipeline (#95)

Verified 2026-09-22 on Linux, Blender 5.2.2, Godot 4.7.2 .NET, with a
rigged, textured test mesh (armature skin + image texture).

1. **Export from Blender.** Interactive: File → Export → glTF 2.0, format
   glTF Binary, into the asset's folder under `assets/`. Headless:

   ```sh
   blender -b assets/source/<file>.blend --python-expr \
     "import bpy; bpy.ops.export_scene.gltf(filepath='assets/<dir>/<name>.glb')"
   ```

   Defaults are fine: skins, animations and images embed in the `.glb`.
2. **Import into Godot.** Open the project in the editor, or headless:

   ```sh
   godot --headless --path . --import
   ```

   The scene appears as `res://assets/<dir>/<name>.glb`; instance it from
   there. Commit the `.glb` and any `.import` sidecars Godot writes.
3. **Check it in.** Git LFS must be installed (`git lfs install`, once per
   machine) or binaries check out as pointer files; `.gitattributes` says
   what LFS tracks.

Note: a headless `--import` run may re-indent open C# files (seen with
`src/Core/BootScene.cs`); check `git status` afterwards and revert
whitespace-only changes.

## Origin and licence note (#95)

Every PR that adds or changes an asset states, per asset:

- **Origin** — who made it and how (author; tool or generator; the concept
  or input sheet it came from, by path).
- **Licence** — SOLOSRC original under the repository MIT licence, or the
  third-party source, URL and licence for anything else. Third-party terms
  must allow an MIT repository and a released game.
- **Sources** — where the editable source lives under `assets/source/`.

The provenance section of
[`source/benchmark-inputs/README.md`](source/benchmark-inputs/README.md)
is the model to follow.
