# Shared-rig animation source

Owner: gpt-astra. Original SOLOSRC motion, MIT; no external motion capture or
animation files. Director review pending. These are motion blockouts on the
existing smoke body, not final modular character art.

- `character_anims.blend`: editable 30 fps actions and NLA tracks, shared 23-bone
  rig; duel_idle is unmuted for opening the file. Other tracks can be soloed.
- `build_animations.py`: reproducible original pose and motion authoring.
- `manifest.json`: clip lengths, loops, provenance and event-data location.
- `configure_import.gd`: applies loop flags to the runtime GLB import preset.
- `verify.gd`: checks runtime retargeting, motion, fixed Root, durations, loops,
  finite sampled poses and injected method-event timing.
- `render.gd`: renders the actual Character scene into a 14-clip contact sheet.

## Rebuild

From the repository root, with Blender 5.2 and Godot 4.7 .NET:

```sh
blender --background --python assets/source/characters/animations/build_animations.py
godot --headless --editor --path . --import
godot --headless --path . --script assets/source/characters/animations/configure_import.gd
godot --headless --editor --path . --import
dotnet build BattleCity.csproj --no-restore
godot --headless --path . --script assets/source/characters/animations/verify.gd
godot --path . --rendering-method gl_compatibility --disable-render-loop --script assets/source/characters/animations/render.gd
```

On the current macOS 13 machine, set `SMOKE28_PYTHON_DEPS` to an external NumPy
2.2.6 installation compatible with Blender Python 3.13, as described in the
smoke source README. The builder reads `assets/source/smoke28/char_a_body.blend`;
it writes only this source directory and the runtime animation carrier.
Blender may produce a `.blend1` backup; do not commit that generated backup.
The `.blend` source is stored with Git LFS.

Rendering writes 60 PNGs to `/private/tmp/character-motion-frames`, at 15 fps.
The committed GIF uses these frames with repeating 70/60/70 ms delays. Each
one-shot holds its endpoint for half a second before repeating for review;
this does not change its non-looping runtime setting.

The GLB carries a duplicate skinned mesh for the importer. Fable's Character
loads its animation library, not that mesh. Do not replace the body with the
carrier. Root stays fixed; controller movement and world yaw remain code-owned.
Godot method events are injected from `data/rig/animation_events.json`, since
GLTF cannot encode Godot method tracks.
