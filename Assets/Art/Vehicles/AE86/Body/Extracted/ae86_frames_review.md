# AE86 Sprite Sheet Extraction Review

- Source: `full ae86.png` (2172x724)
- Detected frames: **17**
- Expected gameplay direction count: **16**
- More frames than expected: **yes (1 extra)**
- Raw connected components: 17
- Normalized canvas: `186x186`
- Alignment: foreground/body center proxy + shared bottom tire baseline; no scaling
- Background removal: 8-connected border flood-fill, border median RGB `254, 254, 254`

## Near-Duplicate Review

- `frame_00` / `frame_16`: score `0.9342`
- `frame_15` / `frame_16`: score `0.8849` (adjacent)
- `frame_00` / `frame_15`: score `0.8839`
- Visual inspection: `frame_00` and `frame_16` have similar silhouettes but are opposite front/rear anchors, so the high score is not sufficient reason to delete either one.
- `frame_15` / `frame_16` are the most suspicious adjacent end pair, but they remain visibly distinct and should be reviewed at gameplay scale before removal.

## Adjacent Frame Similarity

- `frame_00` -> `frame_01`: `0.8411`
- `frame_01` -> `frame_02`: `0.7213`
- `frame_02` -> `frame_03`: `0.7595`
- `frame_03` -> `frame_04`: `0.8126`
- `frame_04` -> `frame_05`: `0.7978`
- `frame_05` -> `frame_06`: `0.8213`
- `frame_06` -> `frame_07`: `0.7811`
- `frame_07` -> `frame_08`: `0.4918`
- `frame_08` -> `frame_09`: `0.7682`
- `frame_09` -> `frame_10`: `0.5102`
- `frame_10` -> `frame_11`: `0.8048`
- `frame_11` -> `frame_12`: `0.8701`
- `frame_12` -> `frame_13`: `0.8349`
- `frame_13` -> `frame_14`: `0.7946`
- `frame_14` -> `frame_15`: `0.6734`
- `frame_15` -> `frame_16`: `0.8849`

## Pose Anchors And Direction Naming

- `frame_00`: front-facing / Down anchor candidate; nose and pop-up headlights at the lower edge
- `frame_01`: progressive front/Down-to-Right intermediate candidate
- `frame_02`: progressive front/Down-to-Right intermediate candidate
- `frame_03`: progressive front/Down-to-Right intermediate candidate
- `frame_04`: progressive front/Down-to-Right intermediate candidate
- `frame_05`: progressive front/Down-to-Right intermediate candidate
- `frame_06`: progressive front/Down-to-Right intermediate candidate
- `frame_07`: progressive front/Down-to-Right intermediate candidate
- `frame_08`: Right-facing side-profile anchor candidate; nose points right
- `frame_09`: Left-facing side-profile anchor candidate; abrupt side switch after frame_08
- `frame_10`: progressive Left-to-rear/Up intermediate candidate; mirror may supply a Right-to-Up source
- `frame_11`: progressive Left-to-rear/Up intermediate candidate; mirror may supply a Right-to-Up source
- `frame_12`: progressive Left-to-rear/Up intermediate candidate; mirror may supply a Right-to-Up source
- `frame_13`: progressive Left-to-rear/Up intermediate candidate; mirror may supply a Right-to-Up source
- `frame_14`: progressive Left-to-rear/Up intermediate candidate; mirror may supply a Right-to-Up source
- `frame_15`: progressive Left-to-rear/Up intermediate candidate; mirror may supply a Right-to-Up source
- `frame_16`: rear-facing / Up anchor candidate; taillights at the lower edge

### Proposed right-side source curation (manual confirmation required)

- `Down`: `frame_00`
- `DownDownRight`: candidate `frame_02`
- `DownRight`: candidate `frame_04`
- `RightDownRight`: candidate `frame_06`
- `Right`: `frame_08`
- `RightUpRight`: candidate mirrored `frame_11`
- `UpRight`: candidate mirrored `frame_12` or `frame_13`; compare in Unity before choosing
- `UpUpRight`: candidate mirrored `frame_14`
- `Up`: `frame_16`

The jump from `frame_08` (Right-facing side) to `frame_09` (Left-facing side) and the uneven sample spacing mean the strip is **not ready for automatic final direction naming**. Treat the mapping above as a curation proposal only.
