# Step 1H — Slot 07 Continuity Redraw Report

## Scope and protection

Only `Docs/AE86Production32Fix/Step1H_Slot07Redraw/` was created or changed. Active Production32 art, Unity assets, metadata, scenes, prefabs, scripts, Step 1–1G outputs, runtime mapping, handling, and `visualSteerLeadAngle` were not modified.

## Inputs

- Previous: `Step1F_IdentityRedraw/PNG/slot_06_22.50_step1f.png`
- Old slot 07: `Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png`
- Right anchor: `Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png`
- Approved palette source: all Step 1F identity-redraw PNGs

The three candidates are genuine model-assisted perspective redraws. No candidate was produced by rotating, shearing, blurring, or merely scaling an existing complete sprite. Normalization afterward used nearest-neighbor placement into the interpolated footprint, binary-alpha cleanup, and nearest-color restriction to the approved Step 1F palette.

## Candidate review

### Candidate A

Candidate A is the shallowest redraw and closest to Right. It adds roof depth and some windshield perspective, but PCA remains −2.45°, too close to the collapsed side-view family and not a valid midpoint from slot 06. Rejected.

### Candidate B — provisional selected output

Candidate B is the most balanced of the three generated concepts and was copied to `PNG/slot_07_11.25_step1h.png` for the required final-output review. It is clearly distinct from Right, has genuine roof/windshield depth and wheel stagger, and passes the technical PNG checks. Its isolated PCA magnitude is 8.69°, 2.56° from the nominal 11.25° magnitude.

However, signed progression is not acceptable: slot 06 measures +23.88° while candidate B measures −8.69°, producing a −32.57° step instead of the preferred −8° to −16°. Visual inspection agrees with the signed metric: the generated car slopes toward the wrong vertical side relative to slot 06, so the apparent 06 → 07 transition flips perspective instead of interpolating it.

Candidate B also introduces an identity/style discontinuity. The body highlights are too pale, the wheels lose the approved dark filled treatment, the front lamps/bumper differ, and the outline/shading density does not remain exact. Therefore the provisional selected output is not approved for Unity retest.

### Candidate C

Candidate C has the deepest roof and strongest perspective. Its −14.57° PCA is farther into the same wrong signed orientation and it is visually too deep for a stable midpoint. It also carries the same palette-mapped-but-not-identity-exact wheel, bumper, lighting, and shading problems. Rejected.

## Continuity decision

- 06 → 07 pop reduced: **No.** The flat old slot is avoided, but replaced by a wrong-signed perspective and identity pop.
- Distinct from Right: **Yes.** Candidate B is not a duplicate or collapsed transition.
- Mirrored runtime counterpart: **Mechanically correct flip, visually not approved.** Exact runtime `flipX` produces the expected mirror, but mirrors the same identity and orientation defects.
- Scale pop: **Technical footprint passes.** Candidate B is 150×90 at baseline 169, matching the interpolated target footprint; centroid (92.18, 126.13) is stable within the requested tolerance.
- Palette/technical errors: **Technical PNG checks pass** (186×186 RGBA, binary alpha, baseline 169, zero edge contact, one retained vehicle component, approved palette quantization). **Manual identity/perspective errors remain.** Palette membership alone did not preserve the required distribution of dark wheel, trim, lamp, and outline colors.
- Full32 preview: **Not coherent at sectors 01/15.** The new sprite and its mirrored counterpart visibly jump against adjacent approved frames.

## Required follow-up

A manual pixel-art redraw should start from the approved slot 06/Right pixels and preserve their exact wheels, lamps, bumper, outline, and shading clusters. The silhouette principal direction should land roughly between +7.9° and +15.9° so the signed 06 → 07 change is −8° to −16°, while retaining the requested intermediate roof depth.

This result does not authorize active Production32 replacement.

NEED_MANUAL_PIXEL_ART
