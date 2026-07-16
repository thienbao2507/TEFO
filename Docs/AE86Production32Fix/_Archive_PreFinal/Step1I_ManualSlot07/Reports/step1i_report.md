# Step 1I — Manual Pixel Patch for Slot 07

## Method and protection

No image generation was used. Every candidate starts from the old Step 1D slot 07 and uses explicit integer pixel-cluster edits. The tool assigns each original opaque pixel to at most one semantic region, moves only that region by whole pixels, repairs enclosed one-pixel seams from neighboring identity pixels, and retains binary alpha. It performs no whole-image rotation, shear, resize, smoothing, interpolation, or antialiasing.

No `Assets/`, Step 1–1H output, Unity asset, metadata, GUID, scene, prefab, script, mapping, handling value, or `visualSteerLeadAngle` was modified.

## Candidate selection

**Candidate C** was selected as the diagnostic final output and saved as `PNG/slot_07_11.25_step1i.png`. It produces the strongest correct-signed slope of the three while preserving the old slot’s colors and recognizable AE86 pixel clusters.

Candidate C is not approved for Unity retest. Its PCA is only +4.51°, below the required +8° to +14° range. Selection means “best of this patch pass,” not “ready.”

## Explicitly changed regions

All changes are highlighted in `Previews/step1i_pixel_edit_regions.png`.

- Roof boundary and visible roof/glass cluster: source rectangle x=20–112, y=86–136; bounded rear-down/front-up offsets.
- Windshield and side-window upper edges: included in the roof/glass patch and moved with the same signed direction.
- Hood, front lighting, bumper and outline: x=105–180, y=105–150; progressively raised toward the nose while keeping original pixels.
- Hatch and rear-lamp boundary: x=17–46, y=112–147; shallow downward rear offset.
- Black lower trim and seam pixels: x=25–180, y=137–162; signed rear-down/front-up adjustment.
- Rear wheel: x=29–67, y=143–169; small downward cluster move.
- Front wheel: x=119–157, y=143–169; upward cluster move for shallow stagger.
- Outline repairs: only transparent one-pixel gaps enclosed horizontally or vertically after cluster movement.

Candidates A, B, and C use increasing integer displacement magnitudes. No Step 1H candidate is used as a base.

## Metrics and adjacent progression

| Item | PCA | Width×height | Centroid | Baseline | Projected length |
|---|---:|---:|---:|---:|---:|
| Slot 06 | +23.88° | 146×105 | (90.99, 116.32) | 169 | 243.24 |
| Old slot 07 | −0.76° | 150×84 | (89.67, 129.29) | 169 | 279.12 |
| Candidate A | +3.29° | 150×87 | (89.20, 128.24) | 169 | 277.33 |
| Candidate B | +4.04° | 150×88 | (89.27, 128.00) | 169 | 277.18 |
| Candidate C | +4.51° | 150×89 | (89.25, 128.05) | 169 | 277.09 |
| Right | −3.16° | 153×76 | (91.10, 133.18) | 169 | 283.49 |

For selected Candidate C:

- Slot 06 → slot 07 signed delta: **−19.38°**; required −10° to −16° — fail.
- Slot 07 → Right signed delta: **−7.67°**; required −8° to −16° — fail.
- Interpolated projected-length expectation: 263.36; error approximately **5.21%** — fail against ≤3%.
- Interpolated centroid expectation: approximately (91.04, 124.75); selected distance approximately **3.75 px** — pass against ≤4 px.
- Width 150, height 89, baseline 169 — pass.

## Manual identity review

- Identity and palette: substantially preserved because all visible colors originate from the old Step 1D sprite; no Step 1H generated artwork or new colors were introduced.
- Wheels: original dark filled wheel clusters remain, with shallow stagger applied. The front-wheel/trim junction still needs manual outline cleanup.
- Lamps and bumper: original clusters remain, but the front perspective needs a deliberate hand-redrawn corner rather than additional regional displacement.
- Outline and shading: density is mostly retained; small stair-step discontinuities remain around the hood/front bumper and roof-to-windshield transition.
- Orientation: the sign is now correct, but the slope magnitude is insufficient.
- Runtime mirror: exact `flipX` remains mechanically coherent, but mirrors the same under-angled pose and outline defects.
- Expected Play Mode pop reduction: some improvement over the flat old slot 07 is expected, but not enough for a Unity retest because the 06 → 07 delta remains too large and projected length remains outside tolerance.

## Why the other candidates were rejected

- Candidate A is the shallowest at +3.29° and remains too close to the flat old pose.
- Candidate B improves to +4.04° but still fails both the target PCA and continuity bands.
- Candidate C is closest to the requested direction, but still fails the mandatory angle and projected-length gates.

Further work must be a true hand redraw of the front quarter, roof/windshield polygon, and wheel/body silhouette—not larger regional offsets—so the pose reaches roughly +10° to +12.5° without stretching projected length or breaking identity.

This result does not authorize active Production32 replacement.

NEED_MORE_MANUAL_PIXEL_EDIT
