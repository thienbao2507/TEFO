# AE86 Production32 Review

## Result

- Production sequence: 17 inclusive poses, Up -> Right -> Down, 11.25 degrees per step.
- Source frames mirrored: frame_09 through frame_15.
- Source frame_16 (Up) was not mirrored because it is the symmetric anchor.
- Source frames frame_08 through frame_00 were retained as-is and reordered Right -> Down.
- Transform policy: full 186x186 canvas retained; no crop, scale, resample, blur, recolor, or redraw.

## Orientation Audit

| Source frame | Apparent front direction | Production position | Action | Confidence | Notes |
| --- | --- | --- | --- | --- | --- |
| frame_00 | Down; hood, headlights, and front bumper are at the bottom | #16, 270.00 deg | Keep pixels as-is | high | Exact Down anchor; retained as-is. |
| frame_01 | Mostly Down, slightly Right | #15, 281.25 deg | Keep pixels as-is | high | Retained as-is. |
| frame_02 | Mostly Down, moderately Right | #14, 292.50 deg | Keep pixels as-is | high | Retained as-is. |
| frame_03 | Down-Right intermediate | #13, 303.75 deg | Keep pixels as-is | high | Retained as-is. |
| frame_04 | DownRight diagonal | #12, 315.00 deg | Keep pixels as-is | high | DownRight anchor; retained as-is. |
| frame_05 | Right-Down intermediate | #11, 326.25 deg | Keep pixels as-is | high | Retained as-is. |
| frame_06 | Right-Down, shallow intermediate | #10, 337.50 deg | Keep pixels as-is | high | Retained as-is. |
| frame_07 | Mostly Right, slightly Down | #09, 348.75 deg | Keep pixels as-is | high | First clockwise step below Right; retained as-is. |
| frame_08 | Right side profile | #08, 0.00 deg | Keep pixels as-is | high | Exact Right anchor; retained as-is. |
| frame_09 | Left side profile | #07, 11.25 deg | Mirror horizontally | medium | Mirrored to face Right. Its angular separation from the exact Right anchor is subtle. |
| frame_10 | Mostly Left, slightly Up | #06, 22.50 deg | Mirror horizontally | medium | Mirrored to mostly Right, slightly Up; source silhouette is smaller than its neighbors. |
| frame_11 | Left-Up intermediate | #05, 33.75 deg | Mirror horizontally | high | Mirrored to the Right-Up intermediate. |
| frame_12 | UpLeft diagonal | #04, 45.00 deg | Mirror horizontally | high | Mirrored to the UpRight diagonal anchor. |
| frame_13 | Up-Left intermediate | #03, 56.25 deg | Mirror horizontally | high | Mirrored to continue clockwise toward UpRight. |
| frame_14 | Up-Left, shallow intermediate | #02, 67.50 deg | Mirror horizontally | high | Mirrored to the matching Up-Right intermediate. |
| frame_15 | Mostly Up, slightly Left | #01, 78.75 deg | Mirror horizontally | high | Mirrored to become mostly Up, slightly Right. |
| frame_16 | Up; rear hatch and taillights are at the bottom | #00, 90.00 deg | Keep pixels as-is | high | Symmetric Up anchor; retained without an unnecessary mirror. |

## Angle To File Mapping

| Index | Angle | Output filename | Source | Mirrored |
| ---: | ---: | --- | --- | :---: |
| 00 | 90.00 | `ae86_090_00_up.png` | `ae86_frame_16.png` | no |
| 01 | 78.75 | `ae86_078_75.png` | `ae86_frame_15.png` | yes |
| 02 | 67.50 | `ae86_067_50.png` | `ae86_frame_14.png` | yes |
| 03 | 56.25 | `ae86_056_25.png` | `ae86_frame_13.png` | yes |
| 04 | 45.00 | `ae86_045_00_upright.png` | `ae86_frame_12.png` | yes |
| 05 | 33.75 | `ae86_033_75.png` | `ae86_frame_11.png` | yes |
| 06 | 22.50 | `ae86_022_50.png` | `ae86_frame_10.png` | yes |
| 07 | 11.25 | `ae86_011_25.png` | `ae86_frame_09.png` | yes |
| 08 | 0.00 | `ae86_000_00_right.png` | `ae86_frame_08.png` | no |
| 09 | 348.75 | `ae86_348_75.png` | `ae86_frame_07.png` | no |
| 10 | 337.50 | `ae86_337_50.png` | `ae86_frame_06.png` | no |
| 11 | 326.25 | `ae86_326_25.png` | `ae86_frame_05.png` | no |
| 12 | 315.00 | `ae86_315_00_downright.png` | `ae86_frame_04.png` | no |
| 13 | 303.75 | `ae86_303_75.png` | `ae86_frame_03.png` | no |
| 14 | 292.50 | `ae86_292_50.png` | `ae86_frame_02.png` | no |
| 15 | 281.25 | `ae86_281_25.png` | `ae86_frame_01.png` | no |
| 16 | 270.00 | `ae86_270_00_down.png` | `ae86_frame_00.png` | no |

## Validation

- Canonical production frame count: 17.
- Every production frame is RGBA-capable, 186x186, and has alpha 0 in all four corner pixels.
- Every frame has its lowest opaque pixel at baseline y=169.
- Every alpha mask and every nontransparent RGBA pixel is identical to its source or to the exact horizontal reversal of its source.
- No production frame was resized or translated.
- No exact adjacent pixel duplicates were found.

## Right Transition

- #07 / 11.25 deg uses mirrored frame_09, so its nose now points Right.
- #08 / 0.00 deg uses native frame_08, whose nose points Right.
- #09 / 348.75 deg uses native frame_07 and continues toward DownRight.
- The former frame_08 Right -> frame_09 Left discontinuity is removed.
- Manual review note: #07 and #08 are both close to side profile, so the 11.25-degree distinction may be subtle at gameplay scale.

## Adjacent Similarity Review

- #04 -> #05: similarity 0.9017; exact duplicate: no.
- #00 -> #01: similarity 0.8974; exact duplicate: no.
- #07 -> #08: similarity 0.8789; exact duplicate: no.
- #03 -> #04: similarity 0.8767; exact duplicate: no.
- #15 -> #16: similarity 0.8640; exact duplicate: no.

Similarity is a review signal, not an angle classifier. Neighboring 11.25-degree poses are expected to be visually close.

## Remaining Source Inconsistencies

- frame_10, exported as #06 / 22.50 deg, has fewer opaque pixels and a visibly smaller silhouette than its immediate neighbors. The pipeline preserved it unchanged.
- The #07 / #08 near-Right pair has weaker angular separation than the rest of the strip. Orientation is now correct, but this pair is the first candidate for art regeneration if motion reads as a held frame.
- Up and Down anchors have similar silhouettes but different physical ends; they are not duplicate poses.
- No size correction was applied because the brief forbids independent scaling.

## Integration Readiness

The set is technically ready for Unity import and a 17-source/32-direction visual mapper. Art review is still recommended for #06 and the #07/#08 transition before treating every 11.25-degree step as final-quality animation.
