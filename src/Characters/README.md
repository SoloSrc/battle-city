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

Controllers (Player, Npc, Duelist) arrive with issues #20 and #23.
