# TEFO Reference World Map Report

- Backup scene path: `Assets/Scenes/MainScene_Backup_Before_ReferenceWorldMap 1.unity`
- Map size: 128x128 tiles
- Tile size: 32x32
- PPU: 32
- Seed: 2507
- Player spawn position: (64, 55, 0)
- Main Camera position: (64, 55, -10)
- Car moved: Car_Basic -> (76, 50, 0)
- Map layer stack created: yes

## Tile Categories Found
- Curb: 16
- Dirt: 14
- Forest: 14
- Grass: 101
- Road: 156
- Road_Marking: 13
- Sand: 11
- Sidewalk: 18
- Water: 28

## Prefab Categories Found
- Beach: 14
- Buildings: 75
- Farm: 8
- Nature: 19
- Props: 26

## Missing Categories
- Prefab keyword: garage
- Prefab keyword: store
- Prefab: Town
- Tile: Collision
- Tile: Decals
- Tile: Farm

## Fallback Tiles Used
- Farm -> Grass/dirt_path_transition_to_grass_01
- Decals -> Grass/dirt_path_transition_to_grass_01
- Collision -> Grass/dirt_path_transition_to_grass_01
- Collision keywords [collision] -> dirt_path_transition_to_grass_01

## Tile Counts Per Layer
- Collision: 2261
- Curb: 944
- Decals_Back: 377
- Dirt: 27353
- Farm: 728
- Forest: 4767
- Grass: 25554
- Ground: 16384
- Road: 3276
- Road_Marking: 502
- Sand: 1246
- Sidewalk: 1158
- Water: 1075

## Props Placed By Category
- beach_hut (beach_hut_large_01): 1
- bench (beach_bench_01): 1
- bench (bench_01): 1
- bush (bush_flower_01): 21
- bush (bush_medium_01): 27
- bush (bush_small_01): 31
- chair (beach_chair_01): 21
- crate (crate_01): 14
- fence (fence_corner_bl_01): 1
- fence (fence_corner_br_01): 4
- fence (fence_corner_tl_01): 2
- fence (fence_h_01): 1
- fence (rural_fence_h_01): 2
- fence (rural_fence_v_01): 2
- fountain (fountain_small_01): 1
- grass (grass_tuft_01): 4
- grass (grass_tuft_02): 6
- grass (grass_tuft_03): 9
- lamp (lamp_post_01): 20
- mailbox (mailbox_01): 6
- palm (palm_medium_01): 7
- palm (palm_small_01): 19
- rock (rock_large_01): 29
- rock (rock_medium_01): 32
- rock (rock_small_01): 35
- sign (wooden_sign_01): 2
- trash (trash_bin_01): 10
- tree (tree_oak_large_01): 19
- tree (tree_oak_medium_01): 38
- tree (tree_oak_small_01): 29
- tree (tree_pine_large_01): 26
- tree (tree_pine_medium_01): 23
- tree (tree_pine_small_01): 30
- umbrella (beach_umbrella_bluewhite_01): 13
- umbrella (beach_umbrella_redwhite_01): 8
- well (well_01): 1

## Buildings Placed By Category
- house (lighthouse_01): 2
- house (town_house_blue_01): 1
- house (village_house_brown_01): 1
- house (village_house_medium_01): 3
- shop (town_shop_medium_01): 1
- shop (town_shop_small_01): 1
- town (traffic_cone_01): 1

## Deleted Old Map Objects
- TEFO_Map

## Warnings
- None

## Manual Polish Suggestions
- Review the beach shoreline and replace any generic water/foam fallback tiles with hand-picked shore variants if your tileset has them.
- Check lighthouse availability; if no lighthouse prefab exists, the builder falls back to landmark/tower-like building tokens.
- Inspect town building lots for sprite footprint differences and nudge individual prefabs if a large art asset extends farther than its reserved tile area.
- Add hand-authored bridge/dock prefabs around the ponds if the fallback token did not find one.
