# AE86 Production32 Step 1 Candidate Report

## 1. Executive summary

This non-destructive pass recovered **11** of the 14 incorrect source slots: **8 PASS**, **3 REVIEW**, and **0 retained FAIL candidates**. Slots **6, 7, 9** remain `NEEDS_EXTERNAL_GENERATION`; no misleading bitmap was created for them. The three active anchors remain read-only.

## 2. Confirmed active architecture

The project uses 32 final headings from 17 real source sprites plus 15 runtime `SpriteRenderer.flipX` mirrors. `sourceSprites17`, controller quantization, visual mapping, prefab references, and Unity import settings were treated as verified and were not changed.

## 3. Sources discovered

See [`source_inventory.md`](source_inventory.md). The authoritative identity source is the 2172x724 `full ae86.png` strip and its 17 border-flood-filled 186x186 extractions. No additional source sheet, backup, deprecated export, or temporary image containing missing exact angles was found.

## 4. Slots recovered from existing source art

Recovered slots: `1, 2, 3, 4, 5, 10, 11, 12, 13, 14, 15`. Each uses a real extracted frame from the authoritative strip; no arbitrary rotation or redraw was used.

## 5. Slots corrected by safe re-extraction

Slots `1-5` use unmirrored extracted frames `14-10`, reassigned by measured heading. Their full vehicle raster was uniformly normalized with nearest-neighbor sampling to the interpolated anchor length, then aligned to baseline y=169. Body and wheels were scaled together.

## 6. Slots corrected by validated flip

Slots `10-15` use extracted frames `07-02` with a full horizontal pixel reversal. The front bumper/headlights and rear hatch/taillights were manually checked after the flip. Their existing projected scale was already within 5 percent, so no resize was applied.

## 7. Supported generation workflow

No generated candidate was accepted. A built-in image-generation path exists, but it cannot guarantee a native 186x186 pixel grid, exact active-car identity, baseline, and <=4-degree heading in one deterministic pass. Using it here would require downsampling/redesign risk, so Step 1 leaves those slots explicit for external generation and review.

## 8. Slots requiring external generation

- Slot `6` at `22.50 deg`: No existing frame matches 22.50 degrees at the approved identity and scale.
- Slot `7` at `11.25 deg`: The only shallow source collapses into the 0-degree Right anchor.
- Slot `9` at `348.75 deg`: No existing frame provides the required small Down bias from Right.

## 9. Angle table for all 17 source positions

| Slot | Target | Estimated | Error | Front/rear inspection | Between neighbors | Method | Status |
|---:|---:|---:|---:|---|---|---|---|
| 0 | 90.00 | 89.65 | 0.35 | Front at top; rear hatch and taillights at bottom. | Yes | Existing | PASS |
| 1 | 78.75 | 74.37 | 4.38 | Front upper-right, rear lower-left; mostly Up. | Yes | Re-extracted | REVIEW |
| 2 | 67.50 | 63.73 | 3.77 | Front upper-right, rear lower-left. | Yes | Re-extracted | PASS |
| 3 | 56.25 | 54.86 | 1.39 | Front upper-right, rear lower-left. | Yes | Re-extracted | PASS |
| 4 | 45.00 | 47.36 | 2.36 | Front upper-right, rear lower-left; diagonal anchor. | Yes | Re-extracted | PASS |
| 5 | 33.75 | 36.14 | 2.39 | Front upper-right, rear lower-left; mostly Right. | Limited: neighbor unresolved | Re-extracted | PASS |
| 6 | 22.50 | n/a | n/a | Defined in the matching prompt and angle-reference diagram. | Unresolved | External required | NEEDS_EXTERNAL_GENERATION |
| 7 | 11.25 | n/a | n/a | Defined in the matching prompt and angle-reference diagram. | Unresolved | External required | NEEDS_EXTERNAL_GENERATION |
| 8 | 0.00 | 356.84 | 3.16 | Front at right; rear hatch and taillights at left. | Limited: neighbor unresolved | Existing | PASS |
| 9 | 348.75 | n/a | n/a | Defined in the matching prompt and angle-reference diagram. | Unresolved | External required | NEEDS_EXTERNAL_GENERATION |
| 10 | 337.50 | 333.94 | 3.56 | Front lower-right, rear upper-left; mostly Right. | Limited: neighbor unresolved | Flipped | PASS |
| 11 | 326.25 | 324.24 | 2.01 | Front lower-right, rear upper-left. | Yes | Flipped | PASS |
| 12 | 315.00 | 316.46 | 1.46 | Front lower-right, rear upper-left; diagonal anchor. | Yes | Flipped | PASS |
| 13 | 303.75 | 307.16 | 3.41 | Front lower-right, rear upper-left. | Yes | Flipped | PASS |
| 14 | 292.50 | 299.20 | 6.70 | Front lower-right, rear upper-left; mostly Down. | Yes | Flipped | REVIEW |
| 15 | 281.25 | 286.15 | 4.90 | Front lower-right, rear upper-left; mostly Down. | Yes | Flipped | REVIEW |
| 16 | 270.00 | 269.97 | 0.03 | Front at bottom; rear hatch and taillights at top. | Yes | Existing | PASS |

Angle values use alpha-mask PCA for the longitudinal axis, with the 180-degree branch resolved by manual inspection of hood/headlights versus hatch/taillights. PCA was not used alone.

## 10. Adjacent-step validation

| Pair | Clockwise delta | Result |
|---|---:|---|
| 0 -> 1 | 15.28 | large jump (15.28 deg) |
| 1 -> 2 | 10.64 | OK (10.64 deg) |
| 2 -> 3 | 8.87 | OK (8.87 deg) |
| 3 -> 4 | 7.50 | OK (7.50 deg) |
| 4 -> 5 | 11.21 | OK (11.21 deg) |
| 5 -> 6 | n/a | Unresolved candidate in pair |
| 6 -> 7 | n/a | Unresolved candidate in pair |
| 7 -> 8 | n/a | Unresolved candidate in pair |
| 8 -> 9 | n/a | Unresolved candidate in pair |
| 9 -> 10 | n/a | Unresolved candidate in pair |
| 10 -> 11 | 9.70 | OK (9.70 deg) |
| 11 -> 12 | 7.78 | OK (7.78 deg) |
| 12 -> 13 | 9.30 | OK (9.30 deg) |
| 13 -> 14 | 7.96 | OK (7.96 deg) |
| 14 -> 15 | 13.05 | OK (13.05 deg) |
| 15 -> 16 | 16.18 | large jump (16.18 deg) |

No measured candidate reverses the clockwise sequence. Slot 0->1 and slot 15->16 remain visibly larger transitions, and all pairs touching slots 6, 7, or 9 remain unresolved until new art exists.

## 11. Scale comparison

| Slot | Projected length | Projected width | Alpha area | Scale delta | Baseline | Edge touches | Components |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 153.34 | 89.02 | 12035 | 0.00% | 169 | 0 | 1 |
| 1 | 152.91 | 91.54 | 11663 | -0.27% | 169 | 0 | 1 |
| 2 | 154.72 | 96.78 | 11788 | 0.91% | 169 | 0 | 1 |
| 3 | 154.36 | 100.35 | 11656 | 0.68% | 169 | 0 | 1 |
| 4 | 153.64 | 100.70 | 11515 | 0.22% | 169 | 0 | 1 |
| 5 | 153.23 | 116.00 | 11367 | -0.04% | 169 | 0 | 1 |
| 8 | 153.27 | 79.03 | 9165 | 0.00% | 169 | 0 | 1 |
| 10 | 148.29 | 81.03 | 8867 | -3.36% | 169 | 0 | 1 |
| 11 | 150.68 | 79.09 | 9226 | -1.87% | 169 | 0 | 1 |
| 12 | 150.85 | 79.99 | 9463 | -1.82% | 169 | 0 | 1 |
| 13 | 154.15 | 81.70 | 9907 | 0.26% | 169 | 0 | 1 |
| 14 | 154.54 | 81.88 | 10247 | 0.45% | 169 | 0 | 1 |
| 15 | 154.29 | 81.08 | 10442 | 0.23% | 169 | 0 | 1 |
| 16 | 154.03 | 87.00 | 11959 | 0.00% | 169 | 0 | 1 |

Projected length and width come from PCA-axis extrema rather than raw bounding-box width. Uniform transforms preserve wheel/body ratio. Manual pixel review confirmed that roof, hood, windshield, hatch, and wheels grow together for slots 1-5; no semantic part was resized independently. All real candidates remain 186x186 RGBA, baseline 169, with no edge contact.

## 12. Center and baseline comparison

| Slot | Alpha centroid | BBox center | Margins L/R/T/B | Interpolated centroid offset | Baseline |
|---:|---|---|---|---|---:|
| 0 | (93.19, 93.87) | (93.00, 93.00) | 49/48/17/16 | (0.00, 0.00) | 169 |
| 1 | (93.52, 95.21) | (92.50, 93.50) | 40/40/18/16 | (0.59, -3.58) | 169 |
| 2 | (94.68, 97.01) | (93.00, 95.50) | 33/32/22/16 | (2.01, -6.68) | 169 |
| 3 | (93.15, 100.55) | (92.50, 100.50) | 28/28/32/16 | (0.74, -8.06) | 169 |
| 4 | (92.09, 102.60) | (92.50, 103.50) | 25/25/38/16 | (-0.05, -10.93) | 169 |
| 5 | (91.57, 108.34) | (93.00, 110.00) | 22/21/51/16 | (-0.32, -10.10) | 169 |
| 8 | (91.10, 133.18) | (93.00, 131.50) | 17/16/94/16 | (0.00, 0.00) | 169 |
| 10 | (93.24, 117.49) | (93.00, 118.00) | 21/20/67/16 | (1.48, -5.84) | 169 |
| 11 | (92.58, 110.52) | (92.50, 112.00) | 23/23/55/16 | (0.49, -7.89) | 169 |
| 12 | (92.77, 105.72) | (93.00, 106.00) | 26/25/43/16 | (0.35, -7.76) | 169 |
| 13 | (92.35, 101.33) | (93.00, 101.00) | 30/29/33/16 | (-0.41, -7.22) | 169 |
| 14 | (91.90, 97.82) | (93.00, 97.50) | 35/34/26/16 | (-1.18, -5.81) | 169 |
| 15 | (92.11, 94.20) | (92.50, 94.00) | 44/44/19/16 | (-1.30, -4.51) | 169 |
| 16 | (93.75, 93.78) | (93.00, 92.50) | 50/49/16/16 | (0.00, 0.00) | 169 |

Raw boxes were not forced to identical coordinates. Candidate content is horizontally centered and tire contact remains at y=169. An optional read-only comparison copy, `PNG/slot_08_0.00_center_preview.png`, shifts the Right anchor two pixels right; it is not an approved replacement.

## 13. Contact sheets

- [`candidates17_contact_sheet.png`](candidates17_contact_sheet.png)
- [`candidate_neighbor_review.png`](candidate_neighbor_review.png)
- [`candidate_full32_preview.png`](candidate_full32_preview.png)

## 14. Candidate PNG paths

- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_01_78.75_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_02_67.50_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_03_56.25_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_04_45.00_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_05_33.75_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_10_337.50_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_11_326.25_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_12_315.00_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_13_303.75_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_14_292.50_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_15_281.25_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_08_0.00_center_preview.png` (audit-only preview)

## 15. Prompt and angle-reference paths

- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_06_22.50_prompt.txt`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_06_22.50_angle_reference.png`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_07_11.25_prompt.txt`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_07_11.25_angle_reference.png`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_09_348.75_prompt.txt`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_09_348.75_angle_reference.png`

## 16. Exact files created

- `Docs/AE86Production32Fix/Step1_Candidates/candidates17_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1_Candidates/candidate_full32_preview.png`
- `Docs/AE86Production32Fix/Step1_Candidates/candidate_metrics.csv`
- `Docs/AE86Production32Fix/Step1_Candidates/candidate_neighbor_review.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_01_78.75_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_02_67.50_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_03_56.25_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_04_45.00_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_05_33.75_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_08_0.00_center_preview.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_10_337.50_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_11_326.25_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_12_315.00_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_13_303.75_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_14_292.50_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/PNG/slot_15_281.25_candidate.png`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_06_22.50_angle_reference.png`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_06_22.50_prompt.txt`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_07_11.25_angle_reference.png`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_07_11.25_prompt.txt`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_09_348.75_angle_reference.png`
- `Docs/AE86Production32Fix/Step1_Candidates/Prompts/slot_09_348.75_prompt.txt`
- `Docs/AE86Production32Fix/Step1_Candidates/source_inventory.md`
- `Docs/AE86Production32Fix/Step1_Candidates/step1_candidate_report.md`
- `Docs/AE86Production32Fix/Step1_Candidates/Tools/AE86Step1Builder.cs`

## 17. Protected-file SHA-256 comparison

| Protected group | File count | Before | After | Identical |
|---|---:|---|---|---|
| active Production32 PNG | 19 | `65843E394D5B57B30EB8DEECF02371B5E15C4837CD686BD251C5BD42C02E4ED9` | `65843E394D5B57B30EB8DEECF02371B5E15C4837CD686BD251C5BD42C02E4ED9` | YES |
| all Assets .meta | 1561 | `946E3AAC02A60259230FF04D96FEA96B2E9BDC7DA6A0D38F45E79CD0DD51FB26` | `946E3AAC02A60259230FF04D96FEA96B2E9BDC7DA6A0D38F45E79CD0DD51FB26` | YES |
| all Assets code | 56 | `F02CB8FE4E40DD014E19EDC2BEACF11D98D9C71A69688C0EA3B97FBB884870F0` | `F02CB8FE4E40DD014E19EDC2BEACF11D98D9C71A69688C0EA3B97FBB884870F0` | YES |
| all prefabs | 152 | `FAA129846C29274984B1268D9F22154DBD408C737453BA80B3016AB106C234B1` | `FAA129846C29274984B1268D9F22154DBD408C737453BA80B3016AB106C234B1` | YES |
| all scenes | 38 | `54E8BDE92F7AF8B93A2E5F2C4BA539806F02EEF0BE162679D1D28E46473A0006` | `54E8BDE92F7AF8B93A2E5F2C4BA539806F02EEF0BE162679D1D28E46473A0006` | YES |

The protected groups cover every PNG in active Production32, every `.meta` under Assets, every Assets `.cs`, every prefab, and every Unity scene. No active PNG pixel, `.meta`, code file, prefab, scene, handling value, import setting, or serialized reference was modified by Step 1.
