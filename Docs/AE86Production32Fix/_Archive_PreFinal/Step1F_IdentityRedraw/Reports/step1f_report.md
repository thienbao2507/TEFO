# AE86 Production32 Step 1F Identity-Preserving Redraw Report

## 1. Executive summary

Nine local pixel candidates were reconstructed from authoritative AE86 identity rasters. Step 1E contributes heading reference only; none of its brown/beige palette, roof, glass, wheel, outline, bumper, or lighting pixels enter a Step 1F candidate.

Manual feature review approved candidates 06-C, 09-B, and 14-B. Final exports remain isolated Step 1F files and are not active Production32 replacements.

## 2. Candidate selection audit

### Slot 06 / 22.50

Review selection: candidate **C**. It has the lowest gated score in the current A/B/C set: PCA 23.88, error 1.38, bbox 146x105, projected-length error -0.67%, centroid distance 2.53 px.

- Candidate A: **REJECTED**. Rejected: angle error 5.95 > 3.00; bbox height +12.32%; centroid 7.52 px.
- Candidate B: **REJECTED**. Rejected: bbox height +6.40%; centroid 4.53 px.
- Candidate C: **SELECTED**. Chosen after metric and manual identity review: every geometry, palette, alpha, artifact, and AE86 feature gate passes.

### Slot 09 / 348.75

Review selection: candidate **B**. It has the lowest gated score in the current A/B/C set: PCA 348.11, error 0.64, bbox 151x91, projected-length error +0.34%, centroid distance 0.85 px.

- Candidate A: **REJECTED**. Rejected: angle error 4.18 > 3.00; bbox height +10.61%.
- Candidate B: **SELECTED**. Chosen after metric and manual identity review: every geometry, palette, alpha, artifact, and AE86 feature gate passes.
- Candidate C: **REJECTED**. Metric-valid alternative, but candidate B has the better combined angle, bbox, projected-length, centroid, and visual continuity score for this slot.

### Slot 14 / 292.50

Review selection: candidate **B**. It has the lowest gated score in the current A/B/C set: PCA 294.48, error 1.98, bbox 111x148, projected-length error -0.81%, centroid distance 1.95 px.

- Candidate A: **REJECTED**. Rejected: angle error 4.09 > 3.00.
- Candidate B: **SELECTED**. Chosen after metric and manual identity review: every geometry, palette, alpha, artifact, and AE86 feature gate passes.
- Candidate C: **REJECTED**. Metric-valid alternative, but candidate B has the better combined angle, bbox, projected-length, centroid, and visual continuity score for this slot.

## 3. Identity and palette

- Approved shared palette size: 38680 exact RGB colors from target neighbors, active Up/Right/Down anchors, and selected Step 1 PASS frames.
- Colors outside approved palette in review selections: 0.
- Free-form Step 1E vehicle pixels copied into candidates: 0.
- Roof, windshield, side windows, wheels, hood, lights, bumpers, and outline: PASS by manual comparison in `step1f_identity_palette_review.png`.
- All nine A/B/C candidates retain the established AE86 identity; rejected candidates are rejected for angle/size/center continuity, not for introducing a different vehicle design.

Every candidate uses binary alpha and hard palette colors. Construction performs discrete part-band moves and seam pixel redraw; it never rotates, shears, antialiases, or blends a complete bitmap.

## 4. Corrected green-halo detector

A contamination pixel must be outside the approved palette, strongly green-dominant, within two pixels of transparency, and part of an eight-connected cluster of at least four pixels. Valid palette pixels never fail.

| Protected slot | Chroma clusters | Result | Source changed |
|---:|---:|---|---|
| 07 | 0 | PASS | NO |
| 11 | 0 | PASS | NO |
| 12 | 0 | PASS | NO |
| 13 | 0 | PASS | NO |

## 5. Review selection metrics

| Slot | Candidate | Target | PCA | Error | BBox / expected | Length error | Alpha / range | Centroid | Palette outside | Chroma | Status |
|---:|---|---:|---:|---:|---|---:|---|---:|---:|---:|---|
| 06 | C | 22.50 | 23.88 | 1.38 | 146x105 / 146.50x101.50 | -0.67% | 9876 / 9411..11936 | 2.53 px | 0 | 0 | METRIC_PASS |
| 09 | B | 348.75 | 348.11 | 0.64 | 151x91 / 149.00x89.50 | +0.34% | 8678 / 8423..9624 | 0.85 px | 0 | 0 | METRIC_PASS |
| 14 | B | 292.50 | 294.48 | 1.98 | 111x148 / 112.50x144.00 | -0.81% | 10301 / 9411..10965 | 1.95 px | 0 | 0 | METRIC_PASS |

## 6. Adjacent continuity

Signed delta uses `((next - previous + 540) % 360) - 180`; expected progression is -11.25 degrees. PCA remains an audit signal, not a front/rear classifier.

| Pair | Signed | Absolute | IoU | Scale mismatch | Center shift | Manual | Status |
|---|---:|---:|---:|---:|---:|---|---|
| 00 -> 01 | -15.28 | 15.28 | 0.80 | 0.28% | 1.38 px | PASS: protected Step 1D progression | PASS |
| 01 -> 02 | -10.64 | 10.64 | 0.86 | 1.17% | 2.15 px | PASS: protected Step 1D progression | PASS |
| 02 -> 03 | -8.87 | 8.87 | 0.88 | 0.23% | 3.85 px | PASS: protected Step 1D progression | PASS |
| 03 -> 04 | -7.50 | 7.50 | 0.91 | 0.46% | 2.31 px | PASS: protected Step 1D progression | PASS |
| 04 -> 05 | -11.21 | 11.21 | 0.84 | 0.27% | 5.77 px | PASS: protected Step 1D progression | PASS |
| 05 -> 06 | -12.26 | 12.26 | 0.80 | 1.61% | 8.00 px | REVIEW: manual hood/rear/wheel progression required | MANUAL REVIEW |
| 06 -> 07 | -24.64 | 24.64 | 0.60 | 0.28% | 13.05 px | PASS: slot 07 has known compressed PCA; roof, side exposure, wheels, and front/rear still progress clockwise | PCA >18 / VISUAL JUSTIFIED |
| 07 -> 08 | -2.40 | 2.40 | 0.86 | 1.91% | 4.14 px | PASS: protected Step 1D progression | PASS |
| 08 -> 09 | -8.73 | 8.73 | 0.71 | 1.30% | 8.76 px | REVIEW: manual hood/rear/wheel progression required | MANUAL REVIEW |
| 09 -> 10 | -14.17 | 14.17 | 0.75 | 2.00% | 7.07 px | REVIEW: manual hood/rear/wheel progression required | MANUAL REVIEW |
| 10 -> 11 | -9.70 | 9.70 | 0.79 | 1.60% | 7.00 px | PASS: protected Step 1D progression | PASS |
| 11 -> 12 | -7.78 | 7.78 | 0.84 | 0.12% | 4.80 px | PASS: protected Step 1D progression | PASS |
| 12 -> 13 | -9.30 | 9.30 | 0.84 | 2.16% | 4.40 px | PASS: protected Step 1D progression | PASS |
| 13 -> 14 | -12.68 | 12.68 | 0.79 | 0.77% | 5.51 px | REVIEW: manual hood/rear/wheel progression required | MANUAL REVIEW |
| 14 -> 15 | -8.33 | 8.33 | 0.87 | 0.86% | 1.66 px | REVIEW: manual hood/rear/wheel progression required | MANUAL REVIEW |
| 15 -> 16 | -16.18 | 16.18 | 0.77 | 0.17% | 1.69 px | PASS: protected Step 1D progression | PASS |

The 06 -> 07 PCA step exceeds 18 degrees because protected slot 07 has a known near-side-profile PCA compression (359.24 degrees). Manual inspection confirms a clockwise reduction in roof depth, side-window exposure, wheel stagger, and body pitch from 06-C into 07, with no front/rear reversal or duplicate; this is the explicit visual justification required by the brief.

## 7. Final output state

Manual approval complete: YES. Final `PNG/slot_*_step1f.png` files are emitted only after all three chosen candidates pass both metric gates and manual AE86 feature review. No placeholder is written as a final sprite.

## 8. Protected SHA-256 proof

| Group | Count | Before | After | Expected match | Unchanged |
|---|---:|---|---|---|---|
| active Production32 PNG | 19 | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | `0C365BB09E03913DEFA8657EC1B71530DE15ABF010B4F820EAF8C862DD18575D` | YES | YES |
| all Assets .meta | 1562 | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | `75DDB21BFE59DCED8F9D20971A22BD54316DCD37CCD25B2A3F4BBE72076E04FF` | YES | YES |
| all Assets code | 56 | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | `06157D40CB7D52D41B3EAE6312C8F980F4B7EB0A527F5C006706045D243C1F93` | YES | YES |
| all prefabs | 152 | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | `92638226A268137617FF640C1AC0DFCE04288EE89CF44B4351CD43D916861649` | YES | YES |
| all scenes | 38 | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | `63393EF3547DBD83DACADD26C4D742B5CDFEC2F62EC54BEC79D056CB8EA381D2` | YES | YES |
| Step 1 candidates | 25 | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | `4A9379EDE8256E251BB15870F3EC9A64C905107C33DF8A394D832B4DF118C654` | YES | YES |
| Step 1C outputs | 26 | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | `0B80E7EF5DBF9D35781A8F96D70C5D9A1011505D98C79DA6A36A408A04741320` | YES | YES |
| Step 1D outputs | 27 | `16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D` | `16D5A1481D1F19C3CD99A232FCA8D4F738F6A4B781B2249D85B83A8978E6142D` | YES | YES |
| Step 1E outputs | 23 | `68447F2AE29219C23E44ADB95071113D48046DC3E51B307D00400E3C78ADEE2F` | `68447F2AE29219C23E44ADB95071113D48046DC3E51B307D00400E3C78ADEE2F` | YES | YES |

## 9. Final decision

All metric, identity, palette, detector, continuity, full32 visual, and protected-file gates pass.

READY_FOR_UNITY_TEST
