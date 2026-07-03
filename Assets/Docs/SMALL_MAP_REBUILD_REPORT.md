# TEFO Small Map Rebuild Report

Generated: 2026-07-03 22:30:00
Run mode: Generate Small Neighborhood Map

## Summary

- Backup scene path: Assets/Scenes/MainScene_Backup_Before_SmallMap_Rebuild 1.unity
- Source pack root: None
- Manifest path: None
- Manifest entries loaded: 0
- PNGs ingested: 0
- Import settings updated: 0
- Tile assets created: 0
- Tile assets updated: 0
- Prop prefabs created: 0
- Prop prefabs updated: 0
- Old map objects deleted: 1
- Map layer stack created: yes
- Props placed: 20
- Player spawn position: x=24, y=8, z=0
- Car position: Car_Basic: x=4, y=12, z=0

## Tile Counts Per Layer

- Curb: 584
- Grass: 3840
- Ground: 3840
- Road: 1760
- Road_Marking: 153
- Sidewalk: 436
- Stone: 873

## Deleted Old Map Objects

- TEFO_Map

## Source PNG To Destination PNG

- None

## Reserved Empty Lots

- lot_bottom_left
- lot_bottom_right
- lot_mid_left
- lot_mid_right
- lot_top_left
- lot_top_right

## Missing Categories

- Prop: Street
- Tile: Decals
- Tile: Stone

## Fallback Tiles Used

- None

## Warnings

- None

## Next Manual Polish Steps

- Review the 48x80 `TEFO_Map` scene layout before saving over any production scene.
- Add true Dirt, Stone, Curb, Road_Marking, and Decals tiles to the small pack if the report lists fallbacks.
- Add building prefabs later if the reserved lots should become houses/shops.
- Tune collision after playtesting player and vehicle movement.

