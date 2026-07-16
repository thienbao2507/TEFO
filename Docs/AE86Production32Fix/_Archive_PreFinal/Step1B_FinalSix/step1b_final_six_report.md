# AE86 Production32 Step 1B Final Six Report

## 1. Executive summary

Step 1B is **NOT_READY**. Of the six target slots, **0 PASS**, **2 REVIEW**, and **4 unresolved** remain. No local source or deterministic reconstruction met the <=3-degree angle limit while preserving front/rear structure and the active 186px identity. External-generation packages were therefore created for all six slots, and no replacement PNG was accepted.

## 2. Exact six-slot results

| Slot | Target | Current actual | Error | Result | Action |
|---:|---:|---:|---:|---|---|
| 1 | 78.75 | 74.37 | 4.38 | REVIEW | Generate a new 78.75-degree pose; current 74.37-degree frame is reference-only. |
| 6 | 22.50 |  |  | UNRESOLVED | Generate a new exact 22.50-degree source. |
| 7 | 11.25 |  |  | UNRESOLVED | Generate a new exact 11.25-degree source visibly distinct from Right. |
| 9 | 348.75 |  |  | UNRESOLVED | Generate a new exact 348.75-degree source visibly distinct from Right. |
| 14 | 292.50 | 299.20 | 6.70 | UNRESOLVED | Reject the 299.20-degree attempt and generate a new 292.50-degree pose. |
| 15 | 281.25 | 286.15 | 4.90 | REVIEW | Generate a new 281.25-degree pose; current 286.15-degree frame is reference-only. |

Slot 14's 299.20-degree Step 1 image is retained only in its original protected Step 1 location and may appear crossed out in audit imagery. It is not selected in the final 17-source candidate sequence.

## 3. Source method by target slot

- Slot `01`: Step 1 re-extraction; external package prepared. Current pose is 4.38 degrees from target and makes pair 00->01 jump 15.28 degrees.
- Slot `06`: External generation required. No local active-identity source exists at the required shallow Up bias.
- Slot `07`: External generation required. The prior shallow source collapsed into the 0-degree Right anchor.
- Slot `09`: External generation required. No local active-identity source exists at the required shallow Down bias.
- Slot `14`: Step 1 candidate rejected; external package prepared. Current pose has 6.70-degree error, above the Step 1B FAIL threshold.
- Slot `15`: Step 1 flip; external package prepared. Current pose is 4.90 degrees from target and leaves a 16.18-degree jump to Down.

The authoritative strip, all 17 extractions, the nine 1254px legacy images, active Production32, Step 1 outputs, deprecated prefabs, Docs, and Temp were rechecked. No unused exact-angle active-identity source was found. Mechanical shear, arbitrary rotation, smooth interpolation, and downscaled legacy art were rejected as final artwork strategies.

## 4. Angle audit

| Slot | Target | Actual | Error | Step 1B threshold | Front/rear result |
|---:|---:|---:|---:|---|---|
| 1 | 78.75 | 74.37 | 4.38 | REVIEW | upper-right, with the exact Up/Right bias shown by the target arrow; lower-left, exactly opposite the front |
| 6 | 22.50 |  |  | Unmeasured | upper-right, with the exact Up/Right bias shown by the target arrow; lower-left, exactly opposite the front |
| 7 | 11.25 |  |  | Unmeasured | upper-right, with the exact Up/Right bias shown by the target arrow; lower-left, exactly opposite the front |
| 9 | 348.75 |  |  | Unmeasured | lower-right, with the exact Down/Right bias shown by the target arrow; upper-left, exactly opposite the front |
| 14 | 292.50 | 299.20 | 6.70 | FAIL / rejected | lower-right, with the exact Down/Right bias shown by the target arrow; upper-left, exactly opposite the front |
| 15 | 281.25 | 286.15 | 4.90 | REVIEW | lower-right, with the exact Down/Right bias shown by the target arrow; upper-left, exactly opposite the front |

PCA figures remain audit measurements only. The 180-degree branch was resolved from black hood/pop-up headlights/yellow fog lights versus hatch window/red taillights. No placeholder is marked PASS.

## 5. Adjacent-step audit

| Pair | Expected | Actual | Similarity | Max scale mismatch | Centroid shift | Status |
|---|---:|---:|---:|---:|---:|---|
| 0 -> 1 | 11.25 | 15.28 | 0.80 | 0.27% | 1.38px | LARGE JUMP |
| 1 -> 2 | 11.25 | 10.64 | 0.86 | 0.91% | 2.14px | REVIEW SOURCE |
| 2 -> 3 | 11.25 | 8.87 | 0.88 | 0.91% | 3.86px | PASS |
| 3 -> 4 | 11.25 | 7.50 | 0.91 | 0.68% | 2.31px | PASS |
| 4 -> 5 | 11.25 | 11.22 | 0.84 | 0.22% | 5.76px | PASS |
| 5 -> 6 | 11.25 |  |  |  |  | UNRESOLVED |
| 6 -> 7 | 11.25 |  |  |  |  | UNRESOLVED |
| 7 -> 8 | 11.25 |  |  |  |  | UNRESOLVED |
| 8 -> 9 | 11.25 |  |  |  |  | UNRESOLVED |
| 9 -> 10 | 11.25 |  |  |  |  | UNRESOLVED |
| 10 -> 11 | 11.25 | 9.70 | 0.79 | 3.36% | 7.00px | SCALE >3% |
| 11 -> 12 | 11.25 | 7.78 | 0.84 | 1.87% | 4.80px | PASS |
| 12 -> 13 | 11.25 | 9.30 | 0.84 | 1.82% | 4.41px | PASS |
| 13 -> 14 | 11.25 |  |  |  |  | UNRESOLVED |
| 14 -> 15 | 11.25 |  |  |  |  | UNRESOLVED |
| 15 -> 16 | 11.25 | 16.18 | 0.77 | 0.23% | 1.69px | LARGE JUMP |

Maximum measured adjacent jump is **16.18 degrees** at slot 15->16. Pairs touching unresolved slots cannot be sequence-approved.

## 6. Scale audit

| Slot | Length | Width | Alpha area | Scale delta | Baseline | Edge touches |
|---:|---:|---:|---:|---:|---:|---:|
| 0 | 153.34 | 89.02 | 12035 | 0.00% | 169 | 0 |
| 1 | 152.91 | 91.54 | 11663 | -0.27% | 169 | 0 |
| 2 | 154.72 | 96.78 | 11788 | 0.91% | 169 | 0 |
| 3 | 154.36 | 100.35 | 11656 | 0.68% | 169 | 0 |
| 4 | 153.64 | 100.70 | 11515 | 0.22% | 169 | 0 |
| 5 | 153.23 | 116.00 | 11367 | -0.04% | 169 | 0 |
| 8 | 153.27 | 79.03 | 9165 | 0.00% | 169 | 0 |
| 10 | 148.29 | 81.03 | 8867 | -3.36% | 169 | 0 |
| 11 | 150.68 | 79.09 | 9226 | -1.87% | 169 | 0 |
| 12 | 150.85 | 79.99 | 9463 | -1.82% | 169 | 0 |
| 13 | 154.15 | 81.70 | 9907 | 0.26% | 169 | 0 |
| 15 | 154.29 | 81.08 | 10442 | 0.23% | 169 | 0 |
| 16 | 154.03 | 87.00 | 11959 | 0.00% | 169 | 0 |

Maximum absolute scale difference among selected source images is **3.36%** at inherited slot 10, which exceeds the Step 1B READY limit of 3%. It is outside the six-slot edit scope and was not changed.

## 7. Center and baseline audit

| Slot | Centroid | BBox center | Center offset | Baseline | Selected |
|---:|---|---|---|---:|---|
| 0 | (93.19, 93.87) | (93.00, 93.00) | (0.00, 0.00) | 169 | Yes |
| 1 | (93.52, 95.21) | (92.50, 93.50) | (0.59, -3.58) | 169 | Yes |
| 2 | (94.68, 97.01) | (93.00, 95.50) | (2.01, -6.68) | 169 | Yes |
| 3 | (93.15, 100.55) | (92.50, 100.50) | (0.74, -8.06) | 169 | Yes |
| 4 | (92.09, 102.60) | (92.50, 103.50) | (-0.05, -10.93) | 169 | Yes |
| 5 | (91.57, 108.34) | (93.00, 110.00) | (-0.32, -10.10) | 169 | Yes |
| 6 |  |  |  |  | No |
| 7 |  |  |  |  | No |
| 8 | (91.10, 133.18) | (93.00, 131.50) | (0.00, 0.00) | 169 | Yes |
| 9 |  |  |  |  | No |
| 10 | (93.24, 117.49) | (93.00, 118.00) | (1.48, -5.84) | 169 | Yes |
| 11 | (92.58, 110.52) | (92.50, 112.00) | (0.49, -7.89) | 169 | Yes |
| 12 | (92.77, 105.72) | (93.00, 106.00) | (0.35, -7.76) | 169 | Yes |
| 13 | (92.35, 101.33) | (93.00, 101.00) | (-0.41, -7.22) | 169 | Yes |
| 14 | (91.90, 97.82) | (93.00, 97.50) | (-1.18, -5.81) | 169 | No |
| 15 | (92.11, 94.20) | (92.50, 94.00) | (-1.30, -4.51) | 169 | Yes |
| 16 | (93.75, 93.78) | (93.00, 92.50) | (0.00, 0.00) | 169 | Yes |

All selected rasters retain baseline y=169 and zero edge contact, but continuity cannot be approved across four missing source positions. No generated frame was available for wheel/roof/hood/windshield semantic measurements in the unresolved cells.

## 8. Visual identity review

Existing Step 1 images retain the active light-gray body, black hood/lower trim, dark windows, black wheels, pop-up headlights, yellow front lighting, red taillights, and pixel outline. Slot 1 and 15 remain visually coherent but angle-inaccurate REVIEW references. Slot 14 is directionally too close to slot 13 and is rejected. No image-generation output was accepted because exact 186px identity, alpha, scale, center, and <=3-degree heading could not be guaranteed locally.

## 9. Remaining unresolved slots

Hard unresolved slots: `6 (22.50)`, `7 (11.25)`, `9 (348.75)`, and `14 (292.50)`. Slots `1 (78.75)` and `15 (281.25)` remain REVIEW and still require new PASS artwork before replacement.

## 10. Full 17-source status

Inherited Step 1 status plus Step 1B decisions: **11 PASS**, **2 REVIEW**, **4 unresolved**.

| Slot | Target | Selected status | Selected source |
|---:|---:|---|---|
| 0 | 90.00 | PASS | Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_090_00_up.png |
| 1 | 78.75 | REVIEW | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_01_78.75_candidate.png |
| 2 | 67.50 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_02_67.50_candidate.png |
| 3 | 56.25 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_03_56.25_candidate.png |
| 4 | 45.00 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_04_45.00_candidate.png |
| 5 | 33.75 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_05_33.75_candidate.png |
| 6 | 22.50 | UNRESOLVED | Placeholder / external required |
| 7 | 11.25 | UNRESOLVED | Placeholder / external required |
| 8 | 0.00 | PASS | Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_000_00_right.png |
| 9 | 348.75 | UNRESOLVED | Placeholder / external required |
| 10 | 337.50 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_10_337.50_candidate.png |
| 11 | 326.25 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_11_326.25_candidate.png |
| 12 | 315.00 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_12_315.00_candidate.png |
| 13 | 303.75 | PASS | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_13_303.75_candidate.png |
| 14 | 292.50 | UNRESOLVED | Placeholder / external required |
| 15 | 281.25 | REVIEW | Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_15_281.25_candidate.png |
| 16 | 270.00 | PASS | Assets/Art/Vehicles/AE86/Body/Extracted/Production32/ae86_270_00_down.png |

## 11. Full 32-direction status

Using the exact runtime flipX mapping: **20 PASS directions**, **4 REVIEW directions**, and **8 unresolved directions**. Each unresolved non-vertical source propagates to its mirrored runtime counterpart.

## 12. Exact files created

- `Docs/AE86Production32Fix/Step1B_FinalSix/final_candidates17_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/final_candidate_full32_preview.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/final_candidate_neighbor_review.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/final_six_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/final_six_metrics.csv`
- `Docs/AE86Production32Fix/Step1B_FinalSix/PNG/README.md`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Prompts/slot_01_78.75_prompt.txt`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Prompts/slot_06_22.50_prompt.txt`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Prompts/slot_07_11.25_prompt.txt`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Prompts/slot_09_348.75_prompt.txt`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Prompts/slot_14_292.50_prompt.txt`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Prompts/slot_15_281.25_prompt.txt`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_01_78.75_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_01_78.75_reference.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_06_22.50_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_06_22.50_reference.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_07_11.25_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_07_11.25_reference.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_09_348.75_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_09_348.75_reference.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_14_292.50_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_14_292.50_reference.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_15_281.25_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/References/slot_15_281.25_reference.png`
- `Docs/AE86Production32Fix/Step1B_FinalSix/Reports/Tools/AE86Step1BBuilder.cs`
- `Docs/AE86Production32Fix/Step1B_FinalSix/step1b_final_six_report.md`

## 13. Protected-file SHA-256 proof

| Protected group | Count | Before | After | Identical |
|---|---:|---|---|---|
| active Production32 PNG | 19 | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | YES |
| all Assets .meta | 1561 | `66465C2EE33F0A5B9F779CC33FEACA7F75A27790C6CF8A19C5D5C549E583DE41` | `66465C2EE33F0A5B9F779CC33FEACA7F75A27790C6CF8A19C5D5C549E583DE41` | YES |
| all Assets code | 56 | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | YES |
| all prefabs | 152 | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | YES |
| all scenes | 38 | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | YES |
| Step 1 candidates | 25 | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | YES |

Protected groups include active Production32 PNGs, every Assets `.meta`, every Assets `.cs`, all prefabs, all scenes, and all 25 Step 1 candidate files.

## 14. Recommendation

**NOT_READY**

Reasons: zero of six Step 1B targets are PASS; four source positions are unresolved; two remain REVIEW; unresolved pairs prevent a complete clockwise sequence; maximum measured jump is 16.18 degrees (>15); inherited maximum scale difference is 3.36% (>3); and center continuity cannot be validated across missing frames. Do not replace active Production32 yet.
