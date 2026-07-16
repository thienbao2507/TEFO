# AE86 Pre-Final Archive

This archive preserves the complete historical STEP 1 through STEP 1J output set that existed before AE86 file consolidation.

## Purpose

- Historical candidates, metrics, reports, tools, prompts, and previews were moved here without permanent deletion.
- The authoritative manual working location is now `Assets/Art/Vehicles/AE86_final/`.
- Runtime test assets remain unchanged at `Assets/Tests/AE86Step1F/`.
- Active Production32 and `Car_AE86.prefab` remain unchanged.

## Restore Procedure

Each archived Step directory can be restored by moving it from `_Archive_PreFinal/` back to its `original_path` recorded in `archive_path_mapping.csv`. The mapping records file count, byte size, and a path-relative SHA-256 group digest for integrity checking.

The `_WorkspaceImportTool/` directory contains the temporary Unity importer and batch log used to assign new workspace GUIDs and Production32-equivalent import settings. It is kept outside `Assets` so it does not add an ongoing Editor script to the clean workspace.

No archived generated candidate should be treated as final artwork. Slot 07 remains unresolved and must be completed manually in `Assets/Art/Vehicles/AE86_final/ManualSlot07/`.
