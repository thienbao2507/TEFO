# AE86 Production32 Step 1D Local Flip Recovery Report

## 1. Executive summary

A complete 17-source local-only set was exported without changing any active or upstream asset. It contains **14 PASS**, **0 REVIEW**, and **3 FAIL** selected sources.

The exact horizontal repairs at slots 06, 09, and 14 correct their front/rear horizontal side. They do not recover missing angular information: slot 06 collapses toward 05, slot 09 collapses toward 10, and slot 14 is pixel-identical in visible content to 15. Manual landmarks override ambiguous PCA branch selection.

## 2. Selected 17-source composition

| Slot | Target | Source | Transform | PCA | BBox | Baseline | Scale | Front/rear | Status |
|---:|---:|---|---|---:|---|---:|---:|---|---|
| 0 | 90.00 | `Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_090_00_up.png` | None (read-only copy) | 89.65 | 89x153 | 169 | 0.00% | PASS: front top, rear bottom. | PASS |
| 1 | 78.75 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_01_78.75_candidate.png` | None (read-only copy) | 74.37 | 106x152 | 169 | -0.27% | PASS: front/rear landmarks inspected for the Up-to-Right sequence. | PASS |
| 2 | 67.50 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_02_67.50_candidate.png` | None (read-only copy) | 63.73 | 121x148 | 169 | 0.91% | PASS: front/rear landmarks inspected for the Up-to-Right sequence. | PASS |
| 3 | 56.25 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_03_56.25_candidate.png` | None (read-only copy) | 54.86 | 130x138 | 169 | 0.68% | PASS: front/rear landmarks inspected for the Up-to-Right sequence. | PASS |
| 4 | 45.00 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_04_45.00_candidate.png` | None (read-only copy) | 47.36 | 136x132 | 169 | 0.22% | PASS: front/rear landmarks inspected for the Up-to-Right sequence. | PASS |
| 5 | 33.75 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_05_33.75_candidate.png` | None (read-only copy) | 36.14 | 143x119 | 169 | -0.04% | PASS: front/rear landmarks inspected for the Up-to-Right sequence. | PASS |
| 6 | 22.50 | `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_06_22.50_scale_normalized.png` | Exact horizontal pixel flip | 36.20 | 138x115 | 169 | -3.45% | PASS: nose/headlights upper-right; rear/red taillights lower-left. | FAIL |
| 7 | 11.25 | `Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_011_25.png` | Uniform nearest-neighbor scale + translation | 359.24 | 150x84 | 169 | -1.89% | PASS: front/rear landmarks inspected for the Up-to-Right sequence. | PASS |
| 8 | 0.00 | `Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_000_00_right.png` | None (read-only copy) | 356.84 | 153x76 | 169 | 0.00% | PASS: front right, rear left. | PASS |
| 9 | 348.75 | `Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_348_75.png` | Exact horizontal pixel flip | 333.94 | 145x103 | 169 | -3.30% | PASS: nose/headlights lower-right; rear/red taillights upper-left. | FAIL |
| 10 | 337.50 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_10_337.50_candidate.png` | None (read-only copy) | 333.94 | 145x103 | 169 | -3.36% | PASS: front/rear landmarks inspected for the Right-to-Down sequence. | PASS |
| 11 | 326.25 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_11_326.25_candidate.png` | None (read-only copy) | 324.24 | 140x115 | 169 | -1.87% | PASS: front/rear landmarks inspected for the Right-to-Down sequence. | PASS |
| 12 | 315.00 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_12_315.00_candidate.png` | None (read-only copy) | 316.46 | 135x127 | 169 | -1.82% | PASS: front/rear landmarks inspected for the Right-to-Down sequence. | PASS |
| 13 | 303.75 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_13_303.75_candidate.png` | None (read-only copy) | 307.16 | 127x137 | 169 | 0.26% | PASS: front/rear landmarks inspected for the Right-to-Down sequence. | PASS |
| 14 | 292.50 | `Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_292_50.png` | Exact horizontal pixel flip | 286.15 | 98x151 | 169 | 0.29% | PASS: nose/headlights lower-right; rear/red taillights upper-left. | FAIL |
| 15 | 281.25 | `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_15_281.25_candidate.png` | None (read-only copy) | 286.15 | 98x151 | 169 | 0.23% | PASS: front/rear landmarks inspected for the Right-to-Down sequence. | PASS |
| 16 | 270.00 | `Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_270_00_down.png` | None (read-only copy) | 269.97 | 87x154 | 169 | 0.00% | PASS: front bottom, rear top. | PASS |

## 3. Horizontal repair audit

Each repair is one full-canvas horizontal pixel flip. There is no rotation, rescale, shear, redraw, smoothing, or antialiasing.

| Slot | Required repaired landmarks | Similarity to previous | Similarity to next | Result |
|---:|---|---:|---:|---|
| 06 | Nose upper-right; rear/red taillights lower-left | 0.93 | 0.52 | Front/rear PASS; pose collapses toward 05 |
| 09 | Nose lower-right; rear/red taillights upper-left | 0.55 | 0.98 | Front/rear PASS; near-duplicate of 10 |
| 14 | Nose lower-right; rear/red taillights upper-left | 0.70 | 1.00 | Front/rear PASS; visible pixels duplicate 15 |

## 4. Slot 07 original versus normalized

| Variant | BBox | Alpha | Length | Scale difference | Centroid | Baseline | Edge | Similarity 06 | Similarity Right | Distinct from Right | Selected |
|---|---|---:|---:|---:|---|---:|---:|---:|---:|---|---|
| Original | 142x79 | 8796 | 142.37 | -5.48% | (89.82, 131.65) | 169 | 0 | 0.50 | 0.89 | YES: roof, hatch, and wheel line remain Up-biased | NO |
| Scale-normalized | 150x84 | 9907 | 150.37 | -0.17% | (89.67, 129.29) | 169 | 0 | 0.52 | 0.86 | YES: shallow upper-right perspective remains visible | **YES** |

**Selection:** normalized slot 07. Uniform nearest-neighbor whole-car scaling and translation reduce scale/center pop without changing heading; the sprite remains visibly distinct from Right.

## 5. Required visual sequence review

- `00 -> 01 -> 02`: Step1 slot 01 forms a credible mostly-Up intermediate; signed PCA steps remain negative and visually clockwise.
- `05 -> 06 -> 07 -> 08`: repaired 06 points to the correct side, but its silhouette is too close to 05 and the following step into 07 is too large. Slot 07 remains distinct from Right.
- `08 -> 09 -> 10 -> 11`: repaired 09 points lower-right, but it is a near-duplicate of slot 10, so the 09->10 step collapses.
- `13 -> 14 -> 15 -> 16`: repaired 14 points lower-right, but its visible raster is identical to slot 15, so the 14->15 step collapses completely.

## 6. Signed adjacent-angle and continuity audit

Signed deltas use `((next - previous + 540) % 360) - 180`. The expected progression is -11.25 degrees. No transition is reported as a false jump near 360 degrees; REVERSED requires both a positive signed delta and manual front/rear evidence.

| Pair | Expected signed | Signed PCA delta | Absolute delta | Similarity | Scale mismatch | Centroid shift | Status |
|---|---:|---:|---:|---:|---:|---:|---|
| 0 -> 1 | -11.25 | -15.28 | 15.28 | 0.80 | 0.27% | 1.38px | STEP REVIEW |
| 1 -> 2 | -11.25 | -10.64 | 10.64 | 0.86 | 0.91% | 2.15px | PASS |
| 2 -> 3 | -11.25 | -8.87 | 8.87 | 0.88 | 0.91% | 3.85px | PASS |
| 3 -> 4 | -11.25 | -7.50 | 7.50 | 0.91 | 0.68% | 2.31px | PASS |
| 4 -> 5 | -11.25 | -11.21 | 11.21 | 0.84 | 0.22% | 5.77px | PASS |
| 5 -> 6 | -11.25 | 0.06 | 0.06 | 0.93 | 3.45% | 2.06px | COLLAPSED |
| 6 -> 7 | -11.25 | -36.96 | 36.96 | 0.52 | 3.45% | 19.00px | LARGE JUMP |
| 7 -> 8 | -11.25 | -2.40 | 2.40 | 0.86 | 1.89% | 4.14px | PCA COMPRESSED / VISUAL DISTINCT |
| 8 -> 9 | -11.25 | -22.90 | 22.90 | 0.55 | 3.30% | 15.73px | LARGE JUMP |
| 9 -> 10 | -11.25 | 0.00 | 0.00 | 0.98 | 3.36% | 1.00px | COLLAPSED |
| 10 -> 11 | -11.25 | -9.70 | 9.70 | 0.79 | 3.36% | 7.00px | PASS |
| 11 -> 12 | -11.25 | -7.78 | 7.78 | 0.84 | 1.87% | 4.80px | PASS |
| 12 -> 13 | -11.25 | -9.30 | 9.30 | 0.84 | 1.82% | 4.40px | PASS |
| 13 -> 14 | -11.25 | -21.01 | 21.01 | 0.70 | 0.29% | 7.14px | LARGE JUMP |
| 14 -> 15 | -11.25 | 0.00 | 0.00 | 1.00 | 0.29% | 0.00px | COLLAPSED |
| 15 -> 16 | -11.25 | -16.18 | 16.18 | 0.77 | 0.23% | 1.69px | STEP REVIEW |

Maximum absolute shortest PCA delta is **36.96 degrees**; maximum absolute anchor-interpolated scale difference is **3.45%**. PCA remains an audit signal, not a substitute for front/rear inspection.

## 7. Technical image audit

All 17 selected PNGs and both slot 07 variants are 186x186 RGBA with true transparency, baseline y=169, zero edge contact, one connected vehicle component, zero partial-alpha pixels, and no crop. No output sprite contains checkerboard, text, arrows, road, or shadow.

## 8. Full 32-direction runtime preview

The exact existing `ResolveSourceSlot` flipX mapping was reproduced without script changes. Runtime propagation gives **26 PASS**, **0 REVIEW**, and **6 FAIL** directions. Mirroring preserves the repaired front/rear side, but the collapsed source transitions also propagate to their mirrored runtime counterparts.

## 9. Exact files created

- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_00_90.00_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_01_78.75_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_02_67.50_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_03_56.25_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_04_45.00_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_05_33.75_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_06_22.50_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_09_348.75_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_10_337.50_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_11_326.25_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_12_315.00_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_13_303.75_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_14_292.50_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_15_281.25_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_16_270.00_step1d.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_original.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_scale_normalized.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_17_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_full32_preview.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_neighbor_review.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_repaired_slots_review.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Previews/step1d_slot07_comparison.png`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Reports/step1d_metrics.csv`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Reports/step1d_report.md`
- `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/Reports/Tools/AE86Step1DBuilder.cs`

## 10. Protected-file SHA-256 proof

| Protected group | Count | Before | After | Identical |
|---|---:|---|---|---|
| active Production32 PNG | 19 | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | YES |
| all Assets .meta | 1562 | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | YES |
| all Assets code | 56 | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | YES |
| all prefabs | 152 | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | YES |
| all scenes | 38 | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | YES |
| Step 1 candidates | 25 | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | YES |
| Step 1C Hybrid Local outputs | 26 | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | YES |

Active Production32, Assets metadata/code/prefabs/scenes, Step 1 candidates, and Step 1C outputs remained read-only. No GUID, vehicle handling value, visualSteerLeadAngle, runtime mapping, prefab, or scene was changed.

## 11. Final decision

The repaired sprites now face the correct horizontal side, but collapsed transitions at 05->06, 09->10, and 14->15 plus large adjacent gaps prevent a coherent 32-direction runtime test set.

NOT_READY