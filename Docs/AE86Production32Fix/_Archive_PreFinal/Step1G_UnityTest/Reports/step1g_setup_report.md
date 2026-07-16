# AE86 Step 1G Unity Test Setup Report

- Unity editor version: 6000.0.62f1
- Test root: `Assets/Tests/AE86Step1F`
- Active prefab SHA-256 after build: `7c5b1ef31a18af492e2ce01fa6f00f92f786abc78af0f99e2b5bb126de7481c0`
- Required scene dependencies copied: none. A test-only bootstrap invokes the existing public `CarTopDownController.EnableControl()` once because gameplay normally enables the car through player interaction; no prefab tuning or serialized control value is changed.
- Compile status: PASS (builder executed after script compilation)
- Missing script status: PASS
- Null sprite/reference status: PASS
- Static full32 result: PASS — 32/32 resolve through the exact runtime slot/flip mapping; see mapping CSV and preview.
- Protected-file hash result: active prefab remained unchanged during builder execution; repository-wide pre/post audit is recorded by the setup operator.

## Selected sources, test assets, GUIDs, and import comparison

- 00: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_00_90.00_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_090_00_up.png` — `086146eecaea5db488bcdcca882ecd92` — importer matches active counterpart
- 01: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_01_78.75_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_078_75.png` — `eb586bcd070653d4c9ab63776a6d4654` — importer matches active counterpart
- 02: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_02_67.50_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_067_50.png` — `0c4d64f76997f9f4f89579463d4750f5` — importer matches active counterpart
- 03: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_03_56.25_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_056_25.png` — `6421d2f5f7279284fa6feeb856aece99` — importer matches active counterpart
- 04: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_04_45.00_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_045_00_upright.png` — `4820fd2d49acc1e499a04c1294f70e04` — importer matches active counterpart
- 05: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_05_33.75_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_033_75.png` — `69efb18f8c0768c4396e69846ad620a8` — importer matches active counterpart
- 06: `Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_06_22.50_step1f.png` → `Assets/Tests/AE86Step1F/Art/ae86_022_50.png` — `4e5048233386c5a4a814ba31231b5ae4` — importer matches active counterpart
- 07: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_011_25.png` — `2ffae7b153cb54b43be83b48ae1f3ef2` — importer matches active counterpart
- 08: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_000_00_right.png` — `6426a4fdcf172cc49918f719e6ce18b0` — importer matches active counterpart
- 09: `Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_09_348.75_step1f.png` → `Assets/Tests/AE86Step1F/Art/ae86_348_75.png` — `b8d5cb9827325394baf2d650fe2e212e` — importer matches active counterpart
- 10: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_10_337.50_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_337_50.png` — `755ccd6f68325b8468b860bd5195f659` — importer matches active counterpart
- 11: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_11_326.25_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_326_25.png` — `d9f5d2b23c5d5c34fad77fd8424025c2` — importer matches active counterpart
- 12: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_12_315.00_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_315_00_downright.png` — `f41692da25e9faf4fbe8f92538956f6e` — importer matches active counterpart
- 13: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_13_303.75_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_303_75.png` — `ba2e450784081db41b7266755b924a64` — importer matches active counterpart
- 14: `Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_14_292.50_step1f.png` → `Assets/Tests/AE86Step1F/Art/ae86_292_50.png` — `e5ad18afef7e062499a84287c4f13e79` — importer matches active counterpart
- 15: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_15_281.25_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_281_25.png` — `43a434b02327c42468b8551cd8aa5bdb` — importer matches active counterpart
- 16: `Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_16_270.00_step1d.png` → `Assets/Tests/AE86Step1F/Art/ae86_270_00_down.png` — `fe078d0411b3e3f4d85631f4439f2f80` — importer matches active counterpart

## sourceSprites17

The array contains exactly 17 non-null test-only sprites in requested order (90° clockwise through 270°). No active Production32 sprite is referenced.

## Active-versus-test prefab differences

See `step1g_prefab_diff.md`. No handling, physics, input, hierarchy, renderer, controller, visual lead angle, or mapping values were changed.

## Manual Play Mode test

1. Open `Assets/Tests/AE86Step1F/Scenes/AE86_Step1F_RuntimeTest.unity`.
2. Enter Play Mode.
3. Drive slowly in clockwise circles.
4. Drive slowly in counterclockwise circles.
5. Rotate through headings near 33.75 → 22.50 → 11.25 → 0; 0 → 348.75 → 337.50; and 303.75 → 292.50 → 281.25 → 270.
6. Observe sprite direction, flipX, scale/center continuity, identity, held/skipped frames, sudden jumps, and incorrect front/rear direction.
7. Test at very low speed, normal speed, steering while accelerating, steering while decelerating, and reversing if supported.

Do not adjust `visualSteerLeadAngle` during this test.

READY_FOR_MANUAL_PLAYMODE_TEST
