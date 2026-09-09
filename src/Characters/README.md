# src/Characters (owner: claude-fable)

`Character` (CharacterBody3D, `scenes/characters/Character.tscn`): loads a body
`.glb` and the shared clip carrier at runtime, keeps a `SkeletonProfileHumanoid`
placeholder skeleton until art arrives, mounts `DuelDisk` on `LeftLowerArm`
with the offset from `data/rig/disk_mount.json`, and drives the AnimationTree
(`Locomotion` blend space by speed plus Talk / DuelReady / DuelIdle / DrawCard /
PlayCard / TakeDamage / Win / Lose states) through `SetLocomotion` and
`PlayState`. Missing clips alias to delivered ones (`CharacterClips.Fallbacks`).

`AnimationRetarget`: copies clips from the carrier, strips `anim_`, retargets
bone tracks onto the loaded skeleton and injects call-method events from
`data/rig/animation_events.json`; every event reaches
`Character.OnAnimationEvent` and the `AnimationEvent` signal.

`PlayerController` (child of a `Character`, `scenes/characters/Player.tscn`):
gamepad-first analogue movement per gdd.md §1.2 and systems.md §4.1 (walk
2.2, run 4.5 above 60 % deflection or Shift, 0.12 s / 0.08 s accel / decel,
720 °/s turn), slope snapping, 0.3 m step-up for kerbs, and the `interact`
probe over `IInteractable` nodes in the `InteractionShape` area, with a
`PromptChanged` signal for the UI. `InputEnabled` hands control to dialogue,
menus and encounters. Npc and Duelist controllers arrive with #23.
