# TEFO Editor Tools

Editor-only helper scripts for map setup live here.

- `TEFOMapLayerSetupTool.cs`: creates the TEFO Tilemap layer stack from a menu item.
- `TEFOTileImportTool.cs`: applies pixel import settings to selected PNG files
  and delegates full map PNG setup to the asset setup tool.
- `TEFOTileAssetSetupTool.cs`: scans map PNGs, applies 32/16 PPU import rules,
  creates Tile assets, creates prop/building prefabs, runs layer setup, and
  writes `Assets/Docs/MAP_IMPORT_REPORT.md`.

These scripts do not run automatically at play mode or build time. The map layer
tool only works under the `TEFO_Map` root and does not touch existing player,
vehicle, or weapon gameplay objects.

Run this first after adding generated map PNGs:

- `TEFO > Map > Assets > Setup All Map Assets`
