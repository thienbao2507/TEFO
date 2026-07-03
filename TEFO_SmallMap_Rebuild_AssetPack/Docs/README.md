# TEFO Small Map Rebuild Asset Pack

Pack này được tách tự động từ 5 sprite sheet vừa tạo lại cho map nhỏ kiểu phố/làng.

## Cấu trúc

- `SourceSheets/`: ảnh sheet gốc để đối chiếu.
- `RawCrops/`: các crop gốc chưa resize, nền đã cố gắng tách trong suốt.
- `UnityReady/Assets/Art/Map/...`: file PNG đã resize gần đúng theo chuẩn Unity, có thể copy thẳng vào project.
- `Docs/asset_manifest.csv`: manifest cho Codex đọc để biết file nên đưa vào folder nào.

## Import Unity gợi ý

- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 32
- Filter Mode: Point (no filter)
- Compression: None
- Generate Mip Maps: Off
- Generate Physics Shape: Off

## Lưu ý quan trọng

Ảnh được tách tự động từ sheet AI nên có thể còn một số sprite chưa đúng 100% tile grid.
Nếu cần map thật đẹp, dùng `UnityReady` để test nhanh trước, sau đó polish lại từng PNG trong LibreSprite/Aseprite.

Tổng số file đã tách: 280 PNG.
