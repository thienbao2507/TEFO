# TEFO Map Layer Guide

Use these Tilemap layers and sorting orders for the TEFO 2D map. Lower sorting
orders render behind higher sorting orders.

| Layer | Sorting Order | Purpose |
| --- | ---: | --- |
| Ground | -100 | Base fill under all terrain. |
| Grass | -90 | Forest, fields, and rural grass. |
| Forest | -85 | Dark forest variants and dense ground transitions. |
| Dirt | -80 | Paths, yards, trails, and exposed soil. |
| Road | -70 | Asphalt road surfaces. |
| Road_Marking | -60 | Road lines, crosswalks, parking marks, and stop lines. |
| Sidewalk | -50 | Walkable urban and village sidewalk tiles. |
| Curb | -45 | Border pieces between roads and sidewalks. |
| Sand | -40 | Beach sand and shoreline ground. |
| Water | -35 | Ocean, ponds, and water surfaces. |
| Decals_Back | -20 | Low overlay details under props and buildings. |
| Props_Back | 0 | Props that should render behind buildings or actors. |
| Buildings | 10 | Houses, shops, garages, and landmarks. |
| Vehicles | 15 | Cars and other vehicles. |
| Player | 20 | Player character. |
| Weapons | 30 | Weapon sprites and attack visuals. |
| Props_Front | 30 | Foreground props that can cover actors. |
| Collision | 100 | Paint-only collision Tilemap with collider components. |

## Notes

- Keep the Grid cell size at 1 unit. With 16 PPU art, each 16x16 tile becomes
  one Unity unit.
- Use `Assets/Editor/TEFOMapLayerSetupTool.cs` to create the layer stack from
  the Unity menu: `TEFO > Map > Create Tilemap Layer Stack`.
- The setup tool only creates missing map Tilemap objects under `TEFO_Map` and
  applies sorting orders there. It does not delete existing scene objects or
  touch existing player, vehicle, or weapon gameplay objects.
- The expected scene hierarchy is `TEFO_Map/Grid/<Tilemap Layer>`.
- The `Collision` Tilemap receives `TilemapCollider2D`, `CompositeCollider2D`,
  and a static `Rigidbody2D`. Its renderer is disabled by default so collision
  painting does not appear during normal editing.
- `Player`, `Vehicles`, and `Weapons` are sorting order references for gameplay
  renderers, not Tilemaps created by the setup tool.
- Put visual source PNGs under `Assets/Art/Map`.
- Put generated Unity Tile assets under `Assets/Tiles`.
- Put Tile Palette assets under `Assets/TilePalettes`.
