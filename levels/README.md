# levels — composed level scenes (owner: gpt-astra)

`district/District.tscn`, `district/interiors/` and area sub-scenes. Levels instance scenes from `scenes/` and place markers; they contain no scripts (architecture.md §4). Run `tools/level_check.gd` before opening a pull request.

## District greybox (issue #23, seeded by claude-fable)

`District.tscn` is the 120 × 120 m district from
[district-layout.md](../docs/design/district-layout.md) as a 1 m grid greybox:
ground slabs per area, 1 m thick / 3 m tall wall boxes on the grid, solid
blocks for the starting room and the card shop, and every marker the slice
needs. X grows east, Z grows south, ground at y = 0; every `kit_*` / `prop_*`
node has its origin at its bounds-min corner so `KitSnap` keeps it on the grid.
Nothing here is final art: replace pieces freely, keep the markers' ids and
the anchors below unless the layout document changes.

| Area (`AmbientZone` / `CameraBounds`) | Slab X / Z (m) | Contents |
| --- | --- | --- |
| `plaza` | 40–76 / 66–104 | `arrival` spawn (58,98), Nico `d1` at (58,91) facing the spawn, `nico` site (58,88), fountain, benches, signs |
| `market` | 8–40 / 48–104 | Shop block (16–28 / 63–73) with `ShopDoor` at (22,73) and `shop_door` spawn, two `TalkNpc`s, planter |
| `park` | 76–112 / 40–104 | Mara `d2` at (91,68) facing west, `mara` site (94,68), river strip on the east edge |
| `arcade` | 40–100 / 8–40 | Arcade Owner `d3` at (70,30), `arcade_owner` site (70,26), sign block on the south wall |
| `edge` | 40–76 / 104–112 | Starting-room block (54–62 / 105–111) with `RoomDoor` at (58,105), construction foreman `TalkNpc` |

- Gates: `ParkGate` in the hedge gap at (76,88) opens on `defeated:d1`;
  `ArcadeGate` in the wall gap at (94,40) opens on `defeated:d2`. Mara and the
  Arcade Owner carry the same flags as `RequiredFlag`, so they neither spot nor
  accept challenges before their area opens.
- Interiors (`interiors/StartRoom.tscn` 8 × 6 m, `interiors/ShopInterior.tscn`
  10 × 8 m) have a `door` spawn just inside their exit door, an interior
  `CameraBounds`, and the room also has the New Game `arrival` spawn. Their
  doors return to `arrival` / `shop_door` in the district.
- Navigation is baked at load by `Game` and by the checklist, so the scenes
  carry no baked data; press the bake button in the editor when you need the
  navmesh visible.
