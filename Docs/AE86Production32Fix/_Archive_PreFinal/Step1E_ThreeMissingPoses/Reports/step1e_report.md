# AE86 Production32 Step 1E Three Missing Poses Report

## 1. Executive summary

Three independently generated perspective sources were processed into deterministic 186x186 pixel-art candidates. No adjacent pose was used as transformed final artwork. Manual landmark approval is intentionally pending in this first builder pass, so the set is not yet authorized for a Unity test.

- New source slots: 06 / 22.50, 09 / 348.75, 14 / 292.50.
- Processing: edge-connected chroma flood-fill, largest vehicle component, uniform nearest-neighbor reduction, shared protected AE86 palette, center placement, baseline y=169.
- Active Production32 and every protected upstream group remain read-only.

## 2. New pose audit

| Slot | Target | Measured PCA | BBox | Alpha | Length | Scale | Center offset | Similarity prev / next | Front/rear | Identity | Status |
|---:|---:|---:|---|---:|---:|---:|---|---|---|---|---|
| 06 | 22.50 | 21.69 | 145x94 | 7620 | 151.07 | -1.45% | (-1.56, -1.18) | 0.58 / 0.62 | PASS: hood/headlights upper-right; hatch/red taillights lower-left | MANUAL REVIEW: AE86 body, hood, windows, wheels, pop-up lights, yellow front lights, and red rear lights | **REVIEW** |
| 09 | 348.75 | 342.23 | 148x82 | 7478 | 150.37 | -1.95% | (+0.95, -0.13) | 0.72 / 0.67 | PASS: hood/headlights lower-right; hatch/red taillights upper-left | MANUAL REVIEW: AE86 body, hood, windows, wheels, pop-up lights, yellow front lights, and red rear lights | **REVIEW** |
| 14 | 292.50 | 299.20 | 116x148 | 9804 | 153.35 | -0.31% | (-0.56, -7.25) | 0.83 / 0.79 | PASS: hood/headlights lower-right; hatch/red taillights upper-left | MANUAL REVIEW: AE86 body, hood, windows, wheels, pop-up lights, yellow front lights, and red rear lights | **REVIEW** |

## 3. Source and cleanup provenance

| Slot | Raw source | Raw PCA | Raw component count | Flood-filled background | Uniform scale | Shared palette | Output |
|---:|---|---:|---:|---:|---:|---:|---|
| 06 | `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_06_generated_raw.png` | 21.75 | 2 | 1243839 px | 0.15 | 176 colors | `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/PNG/slot_06_22.50_step1e.png` |
| 09 | `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_09_generated_raw.png` | 342.35 | 2 | 1250129 px | 0.15 | 176 colors | `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/PNG/slot_09_348.75_step1e.png` |
| 14 | `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_14_generated_raw.png` | 299.19 | 1 | 1323753 px | 0.20 | 176 colors | `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/PNG/slot_14_292.50_step1e.png` |

The generated sources contain genuinely redrawn roofs, hoods, windows, wheel placements, lights, bumpers, and silhouettes. Cleanup never rotates, shears, mirrors, or non-uniformly squeezes a car. The only geometry operation is one uniform nearest-neighbor reduction from the large source followed by translation to the common center/baseline.

## 4. Technical PNG audit

| Slot | Canvas | RGBA alpha | Baseline | Edge | Components | Partial alpha | Green halo | Crop | Result |
|---:|---|---|---:|---:|---:|---:|---:|---|---|
| 06 | 186x186 | binary 0/255 | 169 | 0 | 1 | 0 | 0 | PASS | **PASS** |
| 09 | 186x186 | binary 0/255 | 169 | 0 | 1 | 0 | 0 | PASS | **PASS** |
| 14 | 186x186 | binary 0/255 | 169 | 0 | 1 | 0 | 0 | PASS | **PASS** |

## 5. Signed adjacent continuity audit

Signed delta uses `((next - previous + 540) % 360) - 180`; expected progression is -11.25 degrees. PCA remains an audit signal, while the manual front/rear and perspective landmarks decide visual direction.

| Pair | Signed delta | Absolute | IoU | Scale mismatch | Centroid shift | Manual progression | Status |
|---|---:|---:|---:|---:|---:|---|---|
| 00 -> 01 | -15.28 | 15.28 | 0.80 | 0.27% | 1.38 px | PASS: protected Step 1D visual progression | PASS |
| 01 -> 02 | -10.64 | 10.64 | 0.86 | 0.91% | 2.15 px | PASS: protected Step 1D visual progression | PASS |
| 02 -> 03 | -8.87 | 8.87 | 0.88 | 0.91% | 3.85 px | PASS: protected Step 1D visual progression | PASS |
| 03 -> 04 | -7.50 | 7.50 | 0.91 | 0.68% | 2.31 px | PASS: protected Step 1D visual progression | PASS |
| 04 -> 05 | -11.21 | 11.21 | 0.84 | 0.22% | 5.77 px | PASS: protected Step 1D visual progression | PASS |
| 05 -> 06 | -14.46 | 14.46 | 0.58 | 1.45% | 13.92 px | MANUAL REVIEW: 05 -> new 06 must become shallower UpRight | REVIEW |
| 06 -> 07 | -22.44 | 22.44 | 0.62 | 1.89% | 7.13 px | MANUAL REVIEW: new 06 -> 07 must become still shallower | PCA LARGE STEP REVIEW |
| 07 -> 08 | -2.40 | 2.40 | 0.86 | 1.89% | 4.14 px | PASS: protected Step 1D visual progression | PCA COMPRESSED / MANUAL REVIEW |
| 08 -> 09 | -14.61 | 14.61 | 0.72 | 1.95% | 5.22 px | MANUAL REVIEW: Right -> new 09 gains slight Down bias | REVIEW |
| 09 -> 10 | -8.29 | 8.29 | 0.67 | 3.36% | 10.67 px | MANUAL REVIEW: new 09 -> 10 gains more Down bias | REVIEW |
| 10 -> 11 | -9.70 | 9.70 | 0.79 | 3.36% | 7.00 px | PASS: protected Step 1D visual progression | REVIEW |
| 11 -> 12 | -7.78 | 7.78 | 0.84 | 1.87% | 4.80 px | PASS: protected Step 1D visual progression | REVIEW |
| 12 -> 13 | -9.30 | 9.30 | 0.84 | 1.82% | 4.40 px | PASS: protected Step 1D visual progression | REVIEW |
| 13 -> 14 | -7.96 | 7.96 | 0.83 | 0.31% | 4.96 px | MANUAL REVIEW: 13 -> new 14 gains Down bias | REVIEW |
| 14 -> 15 | -13.05 | 13.05 | 0.79 | 0.31% | 2.22 px | MANUAL REVIEW: new 14 -> 15 becomes still more vertical | REVIEW |
| 15 -> 16 | -16.18 | 16.18 | 0.77 | 0.23% | 1.69 px | PASS: protected Step 1D visual progression | PASS |

## 6. Full32 runtime preview

`Previews/step1e_full32_preview.png` uses the exact current `ResolveSourceSlot` / `flipX` mapping without modifying runtime code. Each new source appears in its native right-side direction and in the corresponding mirrored runtime direction. Final full32 manual coherence approval is pending alongside the three new source landmark review.

## 7. Protected SHA-256 proof

| Protected group | Count | Expected pre-Step1E | Before | After | Identical |
|---|---:|---|---|---|---|
| active Production32 PNG | 19 | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | YES |
| all Assets .meta | 1562 | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | YES |
| all Assets code | 56 | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | YES |
| all prefabs | 152 | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | YES |
| all scenes | 38 | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | YES |
| Step 1 candidates | 25 | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | YES |
| Step 1C outputs | 26 | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | YES |
| Step 1D outputs | 27 | `16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D` | `16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D` | `16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D` | YES |

## 8. Files created

- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/PNG/slot_06_22.50_step1e.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/PNG/slot_09_348.75_step1e.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/PNG/slot_14_292.50_step1e.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Previews/step1e_17_contact_sheet.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Previews/step1e_full32_preview.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Previews/step1e_neighbor_review.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Previews/step1e_three_slots_review.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Prompts/slot_06_22.50_prompt.txt`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Prompts/slot_09_348.75_prompt.txt`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Prompts/slot_14_292.50_prompt.txt`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_06_generated_raw.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_06_generated_raw_rejected_shallow.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_06_generated_raw_rejected_sideview.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_06_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_09_generated_raw.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_09_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_14_generated_raw.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_14_generated_raw_rejected_broad.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/References/slot_14_neighbor_triplet.png`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Reports/step1e_metrics.csv`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Reports/step1e_report.md`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Reports/Tools/AE86Step1EBuilder.cs`
- `Docs/AE86Production32Fix/Step1E_ThreeMissingPoses/Reports/Tools/AE86Step1EBuilder.exe`

## 9. Final decision

The raster and continuity checks are complete, but all three new poses and the propagated full32 view still require the explicit manual visual approval pass represented by the generated review sheets.

NOT_READY
