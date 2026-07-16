# AE86 Production32 Step 1C Report

## 1. Executive summary

**NEED_EXTERNAL_INPUT**

The required Step 1C external-input directory is absent, so no six-angle artwork was invented or substituted. Slot 10 was corrected non-destructively and all requested audit artifacts were produced in blocked-state form. The candidate set is not complete.

Strict 17-source result: **7 PASS**, **4 REVIEW**, **0 FAIL**, and **6 NEED_EXTERNAL_INPUT**.

## 2. Exact missing external inputs

- `Docs/AE86Production32Fix/Step1C_ExternalInputs/slot_01_78.75_external.png`
- `Docs/AE86Production32Fix/Step1C_ExternalInputs/slot_06_22.50_external.png`
- `Docs/AE86Production32Fix/Step1C_ExternalInputs/slot_07_11.25_external.png`
- `Docs/AE86Production32Fix/Step1C_ExternalInputs/slot_09_348.75_external.png`
- `Docs/AE86Production32Fix/Step1C_ExternalInputs/slot_14_292.50_external.png`
- `Docs/AE86Production32Fix/Step1C_ExternalInputs/slot_15_281.25_external.png`

No placeholder is marked PASS, and no old review image was substituted for these files.

## 3. Slot 10 scale correction

The complete slot 10 foreground raster was uniformly resized with integer nearest-neighbor sampling. No rotation, shear, per-part scaling, recoloring, or redraw was used. The tire-contact baseline was re-anchored to y=169.

| Metric | Before | After |
|---|---:|---:|
| Measured heading | 333.94 | 333.74 |
| Heading change | 0.00 | 0.20 |
| Projected length | 148.29 | 153.60 |
| Scale difference | -3.36% | 0.09% |
| Bounding box | 21,67,145,103 | 18,63,150,107 |
| Baseline | 169 | 169 |
| Edge contact | 0 | 0 |
| Partial-alpha pixels | 0 | 0 |

Output: `Docs/AE86Production32Fix/Step1C_FinalCandidates/PNG/slot_10_337.50_scale_fixed_candidate.png`

## 4. All 17 source slots

Step 1C applies the strict <=3.00-degree PASS threshold to every measured source. This can expose inherited REVIEW states that were accepted under the earlier Step 1 threshold.

| Slot | Target | Measured | Error | Scale | Baseline | Method | Status |
|---:|---:|---:|---:|---:|---:|---|---|
| 0 | 90.00 | 89.65 | 0.35 | 0.00% | 169 | Active anchor (read-only) | PASS |
| 1 | 78.75 |  |  |  |  | Required external input missing | NEED_EXTERNAL_INPUT |
| 2 | 67.50 | 63.73 | 3.77 | 0.91% | 169 | Inherited Step 1 candidate (read-only) | REVIEW |
| 3 | 56.25 | 54.86 | 1.39 | 0.68% | 169 | Inherited Step 1 candidate (read-only) | PASS |
| 4 | 45.00 | 47.36 | 2.36 | 0.22% | 169 | Inherited Step 1 candidate (read-only) | PASS |
| 5 | 33.75 | 36.14 | 2.39 | -0.04% | 169 | Inherited Step 1 candidate (read-only) | PASS |
| 6 | 22.50 |  |  |  |  | Required external input missing | NEED_EXTERNAL_INPUT |
| 7 | 11.25 |  |  |  |  | Required external input missing | NEED_EXTERNAL_INPUT |
| 8 | 0.00 | 356.84 | 3.16 | 0.00% | 169 | Active anchor (read-only) | REVIEW |
| 9 | 348.75 |  |  |  |  | Required external input missing | NEED_EXTERNAL_INPUT |
| 10 | 337.50 | 333.74 | 3.76 | 0.09% | 169 | Uniform nearest-neighbor scale normalization | REVIEW |
| 11 | 326.25 | 324.24 | 2.01 | -1.87% | 169 | Inherited Step 1 candidate (read-only) | PASS |
| 12 | 315.00 | 316.46 | 1.46 | -1.82% | 169 | Inherited Step 1 candidate (read-only) | PASS |
| 13 | 303.75 | 307.16 | 3.41 | 0.26% | 169 | Inherited Step 1 candidate (read-only) | REVIEW |
| 14 | 292.50 |  |  |  |  | Required external input missing | NEED_EXTERNAL_INPUT |
| 15 | 281.25 |  |  |  |  | Required external input missing | NEED_EXTERNAL_INPUT |
| 16 | 270.00 | 269.97 | 0.03 | 0.00% | 169 | Active anchor (read-only) | PASS |

## 5. Adjacent clockwise audit

| Pair | Expected | Actual | Similarity | Scale mismatch | Centroid shift | Status |
|---|---:|---:|---:|---:|---:|---|
| 0 -> 1 | 11.25 |  |  |  |  | UNRESOLVED |
| 1 -> 2 | 11.25 |  |  |  |  | UNRESOLVED |
| 2 -> 3 | 11.25 | 8.87 | 0.88 | 0.91% | 3.85px | REVIEW SOURCE |
| 3 -> 4 | 11.25 | 7.50 | 0.91 | 0.68% | 2.31px | PASS |
| 4 -> 5 | 11.25 | 11.21 | 0.84 | 0.22% | 5.77px | PASS |
| 5 -> 6 | 11.25 |  |  |  |  | UNRESOLVED |
| 6 -> 7 | 11.25 |  |  |  |  | UNRESOLVED |
| 7 -> 8 | 11.25 |  |  |  |  | UNRESOLVED |
| 8 -> 9 | 11.25 |  |  |  |  | UNRESOLVED |
| 9 -> 10 | 11.25 |  |  |  |  | UNRESOLVED |
| 10 -> 11 | 11.25 | 9.50 | 0.83 | 1.87% | 4.97px | REVIEW SOURCE |
| 11 -> 12 | 11.25 | 7.78 | 0.84 | 1.87% | 4.80px | PASS |
| 12 -> 13 | 11.25 | 9.30 | 0.84 | 1.82% | 4.40px | REVIEW SOURCE |
| 13 -> 14 | 11.25 |  |  |  |  | UNRESOLVED |
| 14 -> 15 | 11.25 |  |  |  |  | UNRESOLVED |
| 15 -> 16 | 11.25 |  |  |  |  | UNRESOLVED |

Maximum currently measurable adjacent jump: **11.21 degrees**. A complete maximum cannot be approved while pairs touching missing external slots are unresolved.

## 6. Scale, center, alpha, and crop audit

| Slot | Length | Width | Alpha area | Partial alpha | Components | Centroid | Center offset | Edge | Crop | Artifact |
|---:|---:|---:|---:|---:|---:|---|---|---:|---|---|
| 0 | 153.34 | 89.02 | 12035 | 0 | 1 | (93.19, 93.87) | (0.00, 0.00) | 0 | PASS | PASS |
| 2 | 154.72 | 96.78 | 11788 | 0 | 1 | (94.68, 97.01) | (2.01, -6.68) | 0 | PASS | PASS |
| 3 | 154.36 | 100.35 | 11656 | 0 | 1 | (93.15, 100.55) | (0.74, -8.06) | 0 | PASS | PASS |
| 4 | 153.64 | 100.70 | 11515 | 0 | 1 | (92.09, 102.60) | (-0.05, -10.93) | 0 | PASS | PASS |
| 5 | 153.23 | 116.00 | 11367 | 0 | 1 | (91.57, 108.34) | (-0.32, -10.10) | 0 | PASS | PASS |
| 8 | 153.27 | 79.03 | 9165 | 0 | 1 | (91.10, 133.18) | (0.00, 0.00) | 0 | PASS | PASS |
| 10 | 153.60 | 84.43 | 9545 | 0 | 1 | (92.70, 115.49) | (0.94, -7.84) | 0 | PASS | PASS |
| 11 | 150.68 | 79.09 | 9226 | 0 | 1 | (92.58, 110.52) | (0.49, -7.89) | 0 | PASS | PASS |
| 12 | 150.85 | 79.99 | 9463 | 0 | 1 | (92.77, 105.72) | (0.35, -7.76) | 0 | PASS | PASS |
| 13 | 154.15 | 81.70 | 9907 | 0 | 1 | (92.35, 101.33) | (-0.41, -7.22) | 0 | PASS | PASS |
| 16 | 154.03 | 87.00 | 11959 | 0 | 1 | (93.75, 93.78) | (0.00, 0.00) | 0 | PASS | PASS |

Maximum measured absolute scale difference after the slot 10 correction is **1.87%**. Missing sources have no fabricated measurements.

## 7. Final-seven review

- Slot `01`: **NEED_EXTERNAL_INPUT**. Add slot_01_78.75_external.png to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.
- Slot `06`: **NEED_EXTERNAL_INPUT**. Add slot_06_22.50_external.png to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.
- Slot `07`: **NEED_EXTERNAL_INPUT**. Add slot_07_11.25_external.png to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.
- Slot `09`: **NEED_EXTERNAL_INPUT**. Add slot_09_348.75_external.png to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.
- Slot `10`: **REVIEW**. Heading error exceeds the Step 1C 3.00-degree PASS limit.
- Slot `14`: **NEED_EXTERNAL_INPUT**. Add slot_14_292.50_external.png to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.
- Slot `15`: **NEED_EXTERNAL_INPUT**. Add slot_15_281.25_external.png to Docs/AE86Production32Fix/Step1C_ExternalInputs/ and rerun Step 1C.

## 8. Exact 32-direction runtime mapping

The preview uses the existing game mapping without code changes: directions 0-8 use sources 8-0 unflipped, directions 9-23 use sources 1-15 with flipX, and directions 24-31 use sources 16-9 unflipped.

Runtime status: **12 PASS**, **8 REVIEW**, **0 FAIL**, **12 NEED_EXTERNAL_INPUT**.

## 9. Exact files created

- `Docs/AE86Production32Fix/Step1C_FinalCandidates/PNG/slot_10_337.50_scale_fixed_candidate.png`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_final17_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_neighbor_review.png`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_full32_preview.png`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Previews/step1c_final_seven_review.png`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Reports/step1c_metrics.csv`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Reports/step1c_report.md`
- `Docs/AE86Production32Fix/Step1C_FinalCandidates/Reports/Tools/AE86Step1CBuilder.cs`

## 10. Protected-file SHA-256 proof

| Protected group | Count | Before | After | Identical |
|---|---:|---|---|---|
| active Production32 PNG | 19 | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | YES |
| all Assets .meta | 1562 | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | YES |
| all Assets code | 56 | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | YES |
| all prefabs | 152 | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | YES |
| all scenes | 38 | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | YES |
| Step 1 candidates | 25 | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | YES |
| Step 1B outputs | 26 | `221A1FEA9DB0B81388186BEA670D3A8006701E71A2EF824631015B11FEF6FE9D` | `221A1FEA9DB0B81388186BEA670D3A8006701E71A2EF824631015B11FEF6FE9D` | YES |

Protected groups cover active Production32 PNGs, every Assets .meta and .cs file, all prefabs, all scenes, all Step 1 candidates, and all Step 1B outputs.

## 11. Final decision

Six mandatory input files are absent, so all 17 source slots do not exist and twelve runtime directions remain unresolved. Strict inherited angle reviews also remain. Active gameplay assets must not be replaced.

NOT_READY