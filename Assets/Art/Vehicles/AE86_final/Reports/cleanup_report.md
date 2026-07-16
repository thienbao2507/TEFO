# AE86 File Consolidation Cleanup Report

## Scope

The current retained AE86 Production32 candidate composition was consolidated into:

`Assets/Art/Vehicles/AE86_final/`

No active Production32 sprite, prefab, scene, gameplay script, runtime mapping, controller setting, or existing Unity test asset was intentionally changed.

## Main Workspace

- `Current17/`: exactly 17 source sprites in slots 00 through 16.
- `ManualSlot07/`: exactly three identity references for slot 06, working slot 07, and Right.
- `References/`: five selected review images only.
- `Reports/`: manifest, workspace README, cleanup inventory, Unity import audit, protected hash baseline, and this report.

Slot 07 remains `WORKING_SLOT07`, `NOT_FINAL`, and `NEEDS_MANUAL_PIXEL_ART`. `Current17` is not marked production-ready.

No exact `slot07_manual_edit_guide.png` existed. No Step 1I or Step 1J generated candidate was substituted for that missing guide.

## Copy And Provenance Validation

- Current17 copied PNG count: `17`.
- ManualSlot07 copied PNG count: `3`.
- Selected reference PNG count: `5`.
- Required source/reference copy hash mismatches before archive: `0`.
- Manifest entries: `17`.
- Inventory entries: `11` historical Step folders.
- Every Step containing a retained file reached `COPIED_AND_HASH_VERIFIED` before archive began.

See `AE86_final_manifest.csv` and `cleanup_inventory.csv` for per-file and per-Step detail.

## Unity Import Validation

- Unity version: `6000.0.62f1`.
- Batch import result: `AE86_FINAL_IMPORT_READY`, process exit code `0`.
- Current17 sprite assets validated: `17/17`.
- ManualSlot07 sprite assets validated: `3/3`.
- Sprite dimensions and PNG mode: `186x186`, RGBA color type 6.
- Sprite references: all non-null.
- Workspace sprite GUIDs: all unique.
- GUID matches against active Production32 PNGs: `0`.
- Import settings: matched corresponding active Production32 importers, including Sprite/Single, PPU, mesh, pivot, Point filtering, no mipmaps, compression, max size, alpha, wrap, sRGB, NPOT, readability, and platform settings.

The five larger review PNGs preserve their original review-sheet dimensions and are not counted as 186x186 source sprites. See `unity_import_audit.csv` for the complete 20-sprite audit.

## Archive Result

- Archive root: `Docs/AE86Production32Fix/_Archive_PreFinal/`.
- Historical Step folders moved: `11`.
- Historical Step folders permanently deleted: `0`.
- Original Step folders remaining outside archive: `0`.
- Archived Step folders available for restoration: `11`.
- Archive mapping rows: `15`, including the temporary workspace importer and both Unity batch logs.
- Every Step move was verified by file count, total byte size, and path-relative SHA-256 group digest.

The original source paths remain recorded in `AE86_final_manifest.csv`. Their complete historical directories are now recoverable through `Docs/AE86Production32Fix/_Archive_PreFinal/archive_path_mapping.csv` and `archive_readme.md`.

## Protected Assets

The pre-operation baseline contained `3,048` files under `Assets`, excluding only the initially empty `AE86_final` destination. Before archive, all `3,048/3,048` pre-existing files were present and byte-identical.

Explicit protected baselines:

- Active Production32 aggregate: `F7B4D95D5C5E7FE10E94FD1C252BBBAA0E779EEB30235F4EB92F245CBB0667E8`.
- `Assets/Tests/AE86Step1F` aggregate: `2EFD96F8D7C6835601A0827A9270BA359AFA1DF62D7BA9A8AACE3BA50F322A34`.
- Active prefab: `7C5B1EF31A18AF492E2CE01FA6F00F92F786ABC78AF0F99E2B5BB126DE7481C0`.
- AE86 controller: `7E84C30BA30D1B554E8294699FFF54735707FE30D8756E0823AD90FBE3B47F88`.
- AE86 direction visual: `04D3302BA505AF40B3D7B62DDF1B12AE4D15659E2CF5717B2EB3E7B09F82B44A`.

The final protected hash comparison is recorded below after the last Unity refresh:

- Changed pre-existing Assets files: `0`.
- Missing pre-existing Assets files: `0`.
- Unexpected new Assets files outside `AE86_final` and its required root `.meta`: `0`.
- Active Production32 unchanged: `PASS`.
- Active prefab unchanged: `PASS`.
- `Assets/Tests/AE86Step1F` unchanged: `PASS`.
- Existing scripts, prefabs, scenes, and `.meta` files unchanged: `PASS`.

## Integration State

No asset under `AE86_final` is referenced by the active prefab. `sourceSprites17`, runtime mapping, `visualSteerLeadAngle`, handling, physics, colliders, and input configuration were not changed.

Final Production32 replacement remains blocked until slot 07 receives a reviewed manual pixel-art result and a separate explicit integration approval.

WORKSPACE_READY_ARCHIVE_COMPLETE
