# AE86 Final Working Workspace

`Assets/Art/Vehicles/AE86_final/` is the main working location for the current AE86 Production32 candidate set.

## Contents

- `Current17/` contains the retained 17-source candidate composition in runtime source order.
- `ManualSlot07/` contains the previous, working, and next references for the unresolved 11.25-degree pose.
- `References/` contains only the important contact sheets, runtime review, and slot 06/07/Right comparison.
- `Reports/` contains provenance, hashes, cleanup inventory, archive mapping context, and validation results.

## Current Status

Slot 07 is not final. It remains `WORKING`, `NOT_FINAL`, and `NEEDS_MANUAL_PIXEL_ART`. All other retained sprites are anchors or approved for testing, not approved for final production replacement.

The active Production32 folder remains unchanged. Unity test assets remain under `Assets/Tests/AE86Step1F/` and are not replaced by this workspace.

No file under `AE86_final` is referenced by the active `Car_AE86.prefab`. The active prefab and its `sourceSprites17` array remain unchanged.

Replacing active Production32 assets requires another explicit approval step after manual slot 07 review and Play Mode acceptance.
