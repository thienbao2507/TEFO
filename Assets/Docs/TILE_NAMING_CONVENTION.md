# TEFO Tile Naming Convention

Use this convention for all TEFO map source art files.

## Required Rules

- Use `tile_` prefix for Tilemap terrain.
- Use `prop_` prefix for placed props.
- Use `decal_` prefix for overlay details.
- Use lowercase file names.
- Use underscores between words.
- Do not use spaces.
- Do not use Vietnamese accents.
- Use PNG only for art assets.

## Import PPU

- Generated 32x32 map PNGs use `MAP_TILE_PPU = 32`.
- Existing top-level legacy tiles in `Assets/Map/Tiles/*.png` can remain at
  `LEGACY_TILE_PPU = 16` when they were already imported that way.
- Use `TEFO > Map > Assets > Setup All Map Assets` to apply the current import
  rules and regenerate `Assets/Docs/MAP_IMPORT_REPORT.md`.

## Examples

Good:

- `tile_grass_center.png`
- `tile_road_corner_outer_tl.png`
- `prop_tree_small.png`
- `decal_oil_stain.png`

Avoid:

- `Grass Center.png`
- `Tile-Grass-Center.png`
- `prop_cay_nho.png`
- `decal_oil_stain.jpg`
