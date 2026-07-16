# Step 1G prefab serialized-property comparison

Expected differences:

- Root name: `Car_AE86` → `Car_AE86_Step1F_Test` (test asset identity only).
- `CarAE86DirectionVisual.sourceSprites17[0..16]`: all 17 references point to isolated test sprites.

All other serialized component properties are preserved by instantiating the active prefab and changing only the array before saving. Validation confirms required components, array length/order, non-null references, and no missing scripts.
