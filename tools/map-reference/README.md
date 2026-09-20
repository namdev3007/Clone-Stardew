# Phân tích ảnh mẫu 16×16 và trồng lại cây

Bộ script Python này phân tích `Assets/Sprites/tileset/full trang tri-1.png`
theo lưới 16×16 rồi ghi thẳng vào manifest mà tool Editor đang dùng:

```text
Assets/Settings/Map Decorations/Map Prop Placement Manifest.asset
```

Kế hoạch gốc: `docs/MAP_TREE_PLACEMENT_16X16_HANDOFF.md`.
Tool đổ cây vào scene: `Assets/Editor/MapReferencePropImporter.cs`
(**Tools → Map → Import Trees From 16x16 Reference**).

## Chạy lại (cần Python + numpy + Pillow)

```bash
python tools/map-reference/analyze_v4.py       # dò sprite trong ảnh mẫu
python tools/map-reference/build_manifest.py   # lọc trùng, quy ra ô Unity, vẽ ảnh kiểm tra
python tools/map-reference/gen_manifest_asset.py  # ghi manifest .asset cho tool Editor
```

Rồi trong Unity bấm **"2. Trồng cây vào Level_Farm.unity (Apply)"**.

> Đừng bấm **"1. Phân tích ảnh tham chiếu"**, vì nó sẽ ghi đè manifest bằng
> bản dò của C# (ít cây hơn do không chịu được cây che nhau).

Script phụ:

- `residual.py` — tô hồng mọi vật đã nhận diện, phần còn xanh là vật bị bỏ sót.
- `verify_mapping2.py` — đối chiếu ô trong manifest với các prop cũ trong scene.
- `export_manifest.py` — xuất thêm bản JSON (`map-prop-placements.json`) để tra cứu.

## Cách dò

- So khớp từng pixel giữa ảnh mẫu và sprite trong `Assets/Sprites/props-items/...`;
  chuối trang trí lấy từ `bananatree_200.png` ở tỉ lệ 0.5.
- Cây dò theo **16 hàng dưới cùng** (thân/gốc) vì tán cây che nhau rất nhiều.
  Ngưỡng khớp: cây 80%, bụi 85%, cỏ 90%.
- Mỗi vật lấy **điểm tiếp đất giữa gốc** làm anchor rồi quy ra ô 16×16.

## Quy đổi tọa độ

Giống `SetupMapProps.cs`: `PixelsPerUnit=100`, `MapLeftWorld=-8.96`,
`MapTopWorld=6.24`, `ReferenceTopCrop=96`; ô Unity 0.16 = 16 px.

```text
cellX = floor(anchorPixelX / 16) - 56
cellY = floor((720 - anchorPixelY) / 16)
```

Kiểm chứng bằng `verify_mapping2.py`: 513/538 prop cũ trong `Level_Farm.unity`
rơi đúng ô, 17 lệch 1 ô.

## Luật loại bỏ (ghi trong `status` của từng mục)

| status | Ý nghĩa | Ghi chú |
|---|---|---|
| 0 | Hợp lệ | sẽ được trồng |
| 1 | Trên đường đi | đọc màu từ `bố cục map.png` |
| 2 | Dưới nước | |
| 3 | Trong vùng canh tác | ruộng thường, giàn dưa chuột, ruộng thanh long |
| 4 | Chỗ NPC / biển / hàng rào | |
| 5 | Ngoài vùng đất (dải 96 px trên cùng) | |

Ngoại lệ: cây hoa gạo, lu nước, ụ lúa, cây chết được giữ dù cạnh đường vì là vật
đặt tay. Vườn quanh nhà (vùng "sau khi mở ruộng thanh long") giữ lại cỏ nền
nhưng bỏ cây/bụi để còn chỗ trồng. Cỏ không gắn collider.
