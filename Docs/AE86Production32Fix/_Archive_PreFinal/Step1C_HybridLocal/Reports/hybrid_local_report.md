# AE86 Production32 Step 1C Hybrid Local Report

## 1. Executive summary

A complete 17-source, local-only candidate set was exported without touching active gameplay assets. It contains **11 PASS**, **3 REVIEW**, and **3 FAIL** sources after technical and manual front/rear review.

PCA is reported only as an axis signal. Manual hood/headlight versus hatch/taillight inspection overrides the PCA branch when the vehicle faces the wrong horizontal side.

## 2. Selected 17-source composition

| Slot | Target | Source | PCA | BBox | Baseline | Scale | Visual result | Status |
|---:|---:|---|---:|---|---:|---:|---|---|
| 0 | 90.00 | Active anchor `ae86_090_00_up.png` | 89.65 | 89x153 | 169 | 0.00% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 1 | 78.75 | Original local `ae86_078_75.png` | 91.72 | 85x152 | 169 | -0.70% | Slight local intermediate is real but remains compressed toward the Up anchor. | REVIEW |
| 2 | 67.50 | Step1 `slot_02_67.50_candidate.png` | 63.73 | 121x148 | 169 | 0.91% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 3 | 56.25 | Step1 `slot_03_56.25_candidate.png` | 54.86 | 130x138 | 169 | 0.68% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 4 | 45.00 | Step1 `slot_04_45.00_candidate.png` | 47.36 | 136x132 | 169 | 0.22% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 5 | 33.75 | Step1 `slot_05_33.75_candidate.png` | 36.14 | 143x119 | 169 | -0.04% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 6 | 22.50 | Local normalized `ae86_022_50.png` | 323.80 | 138x115 | 169 | -3.45% | Taillights sit lower-right and the nose points upper-left; wrong horizontal side and compressed against slot 05. | FAIL |
| 7 | 11.25 | Original local `ae86_011_25.png` | 359.18 | 142x79 | 169 | -7.11% | Distinct from Right through roof perspective, rear-hatch height, and wheel alignment while remaining shallow. Scale continuity needs gameplay review. | REVIEW |
| 8 | 0.00 | Active anchor `ae86_000_00_right.png` | 356.84 | 153x76 | 169 | 0.00% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 9 | 348.75 | Original local `ae86_348_75.png` | 26.06 | 145x103 | 169 | -3.30% | Distinct from Right, but hood/headlights point lower-left instead of the required lower-right. | FAIL |
| 10 | 337.50 | Step1 `slot_10_337.50_candidate.png` | 333.94 | 145x103 | 169 | -3.36% | Step 1 option B selected: hood/headlights point lower-right and continue smoothly toward slot 11. | PASS |
| 11 | 326.25 | Step1 `slot_11_326.25_candidate.png` | 324.24 | 140x115 | 169 | -1.87% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 12 | 315.00 | Step1 `slot_12_315.00_candidate.png` | 316.46 | 135x127 | 169 | -1.82% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 13 | 303.75 | Step1 `slot_13_303.75_candidate.png` | 307.16 | 127x137 | 169 | 0.26% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |
| 14 | 292.50 | Original local `ae86_292_50.png` | 253.85 | 98x151 | 169 | 0.29% | Original local source points lower-left and therefore does not lie between right-side slots 13 and 15. | FAIL |
| 15 | 281.25 | Original local `ae86_281_25.png` | 269.44 | 81x153 | 169 | -0.29% | Mostly Down and technically clean, but the intended small Right bias is weak. | REVIEW |
| 16 | 270.00 | Active anchor `ae86_270_00_down.png` | 269.97 | 87x154 | 169 | 0.00% | Front/rear landmarks, perspective, scale, and placement remain visually coherent with neighboring selected frames. | PASS |

## 3. Slot 06 original versus normalized

Only the complete vehicle raster was uniformly scaled with integer nearest-neighbor sampling and translated to baseline y=169. There was no rotation, shear, redraw, smoothing, or per-part resize.

| Variant | BBox | Alpha area | Length | Ratio to slots 05/07 target | Baseline | Centroid |
|---|---|---:|---:|---:|---:|---|
| Original local | 115x96 | 7369 | 123.38 | 0.83 | 169 | (93.14, 120.15) |
| Scale-normalized | 138x115 | 10594 | 147.99 | 1.00 | 169 | (93.89, 110.35) |

**Visual recommendation:** select the normalized variant for the hybrid sheet because its whole-car size sits closer to slots 05 and 07 without appearing oversized. This does not repair the source's front/rear orientation: its taillights remain lower-right and its nose remains upper-left, so slot 06 is still FAIL.

## 4. Slot 10 option A versus B

Neither option was resized or rotated for this comparison.

| Option | Source | PCA | BBox | Centroid | Manual orientation | Decision |
|---|---|---:|---|---|---|---|
| A | local `ae86_337_50.png` | 35.76 | 140x115 | (92.42, 110.52) | Hood/headlights at lower-left, wrong side | Reject |
| B | Step 1 `slot_10_337.50_candidate.png` | 333.94 | 145x103 | (93.24, 117.49) | Hood/headlights at lower-right, continuous into slot 11 | **Selected** |

## 5. Required visual sequence review

- `00 -> 01 -> 02`: slot 01 is a real local intermediate but remains very close to Up; keep it REVIEW for gameplay comparison.
- `05 -> 06 -> 07 -> 08`: slot 07 is visibly distinct from Right through roof perspective, rear-hatch height, and wheel alignment. Slot 06 is horizontally reversed and also visually compresses the 05->06 step; FAIL.
- `08 -> 09 -> 10 -> 11`: slot 09 is visibly different from Right but points down-left instead of down-right. Slot 10 option B restores the required side, producing an obvious orientation reversal at 09->10; FAIL.
- `13 -> 14 -> 15 -> 16`: local slot 14 points down-left, not between the right-side slot 13 and slot 15. Slot 15 is mostly Down but its right bias is weak; slot 14 FAIL and slot 15 REVIEW.

## 6. Adjacent PCA and continuity audit

| Pair | Expected | PCA delta | Similarity | Scale mismatch | Centroid shift | Status |
|---|---:|---:|---:|---:|---:|---|
| 0 -> 1 | 11.25 | 357.93 | 0.93 | 0.70% | 1.12px | REVERSED |
| 1 -> 2 | 11.25 | 27.99 | 0.69 | 0.91% | 3.19px | LARGE JUMP |
| 2 -> 3 | 11.25 | 8.87 | 0.88 | 0.91% | 3.85px | PASS |
| 3 -> 4 | 11.25 | 7.50 | 0.91 | 0.68% | 2.31px | PASS |
| 4 -> 5 | 11.25 | 11.21 | 0.84 | 0.22% | 5.77px | PASS |
| 5 -> 6 | 11.25 | 72.35 | 0.60 | 3.45% | 3.08px | LARGE JUMP |
| 6 -> 7 | 11.25 | 324.62 | 0.49 | 7.11% | 21.68px | REVERSED |
| 7 -> 8 | 11.25 | 2.34 | 0.89 | 7.11% | 1.99px | COLLAPSED |
| 8 -> 9 | 11.25 | 330.78 | 0.52 | 3.30% | 15.78px | REVERSED |
| 9 -> 10 | 11.25 | 52.13 | 0.44 | 3.36% | 0.48px | LARGE JUMP |
| 10 -> 11 | 11.25 | 9.70 | 0.79 | 3.36% | 7.00px | SCALE >3% |
| 11 -> 12 | 11.25 | 7.78 | 0.84 | 1.87% | 4.80px | PASS |
| 12 -> 13 | 11.25 | 9.30 | 0.84 | 1.82% | 4.40px | PASS |
| 13 -> 14 | 11.25 | 53.31 | 0.46 | 0.29% | 7.16px | LARGE JUMP |
| 14 -> 15 | 11.25 | 344.41 | 0.77 | 0.29% | 0.43px | REVERSED |
| 15 -> 16 | 11.25 | 359.47 | 0.91 | 0.29% | 0.66px | REVERSED |

Maximum measured PCA jump is **359.47 degrees**; maximum absolute anchor-interpolated scale difference is **7.11%**. These figures do not override the manual orientation failures.

## 7. Technical image audit

All 17 selected PNGs are 186x186 RGBA, baseline y=169, zero edge contact, one connected alpha component, no partial-alpha halo, and no crop. Candidate images contain no checkerboard, labels, arrows, road, or shadow.

## 8. Full 32-direction runtime preview

The exact existing flipX mapping was used without code changes. Runtime propagation gives **20 PASS**, **6 REVIEW**, and **6 FAIL** directions.

## 9. Exact files created

- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_00_90.00_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_01_78.75_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_02_67.50_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_03_56.25_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_04_45.00_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_05_33.75_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_06_22.50_original_local.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_07_11.25_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_08_0.00_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_09_348.75_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_10_337.50_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_11_326.25_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_12_315.00_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_13_303.75_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_14_292.50_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_15_281.25_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_16_270.00_hybrid.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/PNG/slot_06_22.50_scale_normalized.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_17_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_full32_preview.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_neighbor_review.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_slot06_comparison.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Previews/hybrid_local_slot09_10_11_comparison.png`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Reports/hybrid_local_metrics.csv`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Reports/hybrid_local_report.md`
- `Docs/AE86Production32Fix/Step1C_HybridLocal/Reports/Tools/AE86Step1CHybridBuilder.cs`

## 10. Protected-file SHA-256 proof

| Protected group | Count | Before | After | Identical |
|---|---:|---|---|---|
| active Production32 PNG | 19 | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | YES |
| all Assets .meta | 1562 | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | YES |
| all Assets code | 56 | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | YES |
| all prefabs | 152 | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | YES |
| all scenes | 38 | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | YES |

Active Production32 PNGs, Assets .meta/.cs files, prefabs, and scenes remained read-only. No GUID, vehicle handling value, visualSteerLeadAngle, code, prefab, or scene was changed.

## 11. Final decision

The 17 files are complete and can be inspected, but wrong-side orientations at slots 06, 09, and 14 plus REVIEW states at 01, 07, and 15 prevent approval for Unity runtime testing as a coherent 32-direction set.

NOT_READY