# Step 1J — True Manual Perspective Redraw for Slot 07

## Result

No candidate passed every selection gate. Therefore no approved `PNG/slot_07_11.25_step1j.png` was created.

Candidate C is used only as the diagnostic candidate in the sequence previews because it comes closest to the required signed progression. It is not selected or approved.

## Construction method

No image generation was used. Candidates A, B, and C were authored as separate integer-coordinate pixel geometries. Each has independently specified body silhouette, roof polygon, windshield trapezoid, hood/front-quarter polygon, side and rear windows, hatch, near wheels, far wheels, lower trim, bumper corners, lamps, seams, and outline joins. They were not made by translating regions, rotating, shearing, resizing, filtering, or increasing a shared displacement parameter.

Colors are selected exclusively from the approved Step 1F palette using 32-bit nearest-color distance. Step 1I contributes placement and identity reference only; its side-view geometry is not retained.

## Metrics

| Candidate | PCA | 06→07 delta | 07→Right delta | Width×height | Centroid | Baseline | Projected length | Error |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| A | +5.21° | −18.67° | −8.37° | 153×86 | (91.13, 127.56) | 169 | 276.78 | 5.09% |
| B | +7.04° | −16.84° | −10.20° | 153×91 | (92.05, 125.27) | 169 | 278.05 | 5.58% |
| C | +8.56° | −15.33° | −11.72° | 153×95 | (92.99, 123.40) | 169 | 278.52 | 5.76% |

Reference PCA values are slot 06 = +23.88° and Right = −3.16°. Projected-length expectation is 263.36 with allowed range 255.46–271.26.

## Candidate decisions

- **A rejected:** PCA is far below +10°, 06→07 delta is too large, and projected length exceeds tolerance. It is the shallowest and closest to Right, but remains insufficiently perspective-deep.
- **B rejected:** bbox, height, centroid, baseline, and 07→Right delta pass, but PCA is +7.04°, 06→07 delta narrowly misses the range, and projected-length error is 5.58%.
- **C rejected:** both adjacent signed deltas pass and the positive slope is strongest. However PCA is below +10°, height is 95 rather than 84–91, projected-length error is 5.76%, and centroid x is close to the 4 px limit.

## Geometry and identity review

- Roof and windshield: genuinely rebuilt for all candidates. Depth increases A→C and follows the correct positive sign, but Candidate C’s roof/body stack is too tall.
- Hood/front quarter: genuinely shortened and reconstructed with an upper surface and front-right corner. The result remains too horizontally long by projected-length measurement.
- Wheels: near and far wheels are separately redrawn with staggered centers. They remain dark and filled, but their simplified spoke/arch treatment does not exactly match the established Step 1F wheel clusters.
- Side windows: remain strongly visible and use approved dark glass colors, but the pillar and upper-edge pixel density is simpler than the authoritative sprites.
- Lamps and bumper: red rear and yellow front landmarks are present, but exact lamp and bumper pixel distribution is not yet identity-accurate.
- Outline and shading: binary, crisp, and palette-valid, but manual inspection finds outline/shading simplification compared with slot 06, old slot 07, and Right.
- Runtime mirror: the exact `flipX` counterpart is mechanically correct and preserves the signed geometry, but mirrors the same identity and proportion shortcomings.
- Expected Play Mode continuity: Candidate C would improve signed direction continuity, but its excess height/length and simplified identity would create a new visual pop. Unity retest is not justified.

## Pixel-change manifest

`step1j_pixel_change_manifest.csv` records, for every candidate, reused reference scope, all redrawn areas, changed/added/removed opaque pixel counts, edited bounding rectangle, and explicit wheel/hood/roof reconstruction flags.

## Protected assets

This step writes only under `Docs/AE86Production32Fix/Step1J_TrueSlot07Redraw/`. It does not authorize active Production32 replacement.

NEED_MORE_MANUAL_PIXEL_EDIT
