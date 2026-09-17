# Bàn giao: phân tích map theo ô 16×16 và trồng lại cây bằng GameObject

> Mục đích: tài liệu này dùng để giao cho một bot/lập trình viên khác trồng lại cây và vật trang trí đúng theo ảnh map tham chiếu.
>
> Yêu cầu gốc đã được xác nhận: **đọc từng ô 16×16 pixel, nhận biết vị trí chân/gốc cây, rồi đặt từng cây thành GameObject riêng trong scene**. Không vẽ cây vào Tilemap và không rải ngẫu nhiên toàn map.

## 1. Kết quả cần đạt

- Cây và vật trang trí xuất hiện gần giống vị trí trong ảnh tham chiếu.
- Mỗi cây là một GameObject độc lập để có thể:
  - thêm `BoxCollider2D` ở phần thân/gốc;
  - Y-sort với Player;
  - chọn, di chuyển hoặc xóa riêng trong Editor;
  - gắn logic chặt cây về sau.
- Đối chiếu map theo lưới **16×16 pixel**. Tọa độ của cây là ô chứa **điểm tiếp đất ở giữa gốc cây**, không phải ô chứa góc trên-trái của tán cây.
- Kết quả phải được lưu trực tiếp trong `Level_Farm.unity`, nhìn thấy ở Edit Mode và giữ nguyên sau khi đóng/mở Unity.
- Không sửa, khoét hoặc xóa tile nền trong `Map Layout Preview.prefab`.

## 2. File làm nguồn chuẩn

### Ảnh/map tham chiếu

- `Assets/Sprites/tileset/full trang tri-1.png`
- `Assets/Sprites/tileset/full trang tri map.png`
- Map nền hiện dùng: `Assets/Tiles/Map Layout/Map Layout Preview.prefab`

Ưu tiên `full trang tri-1.png` nếu hai ảnh khác nhau. Chỉ dùng ảnh còn lại để đối chiếu hình dáng hoặc vị trí khó đọc.

### Sprite GameObject được phép đặt

- `Assets/Sprites/props-items/trang trí map-tách riêng/`
- `Assets/Sprites/props-items/ụ lúa-tách riêng/`
- `Assets/Sprites/props-items/cây-chết.png`
- `Assets/Sprites/props-items/lu nước.png`
- `Assets/Sprites/props-items/cây hoa gạo-Sheet.png`
- Chuối trang trí: `Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200.png`

Không cắt lại ảnh tham chiếu để dùng làm một Sprite lớn phủ lên map. Phải dùng các Sprite vật thể đã tách riêng.

### Scene và dữ liệu vùng

- Scene gameplay map: `Assets/Scenes/Levels/OutDoors/Level_Farm.unity`
- Scene chính: `Assets/MainScenes/Core 1.unity`
- Dữ liệu vùng: `Assets/Settings/Map Regions/Map Region Collection.asset`

## 3. Công cụ hiện có trong project

### `MapCellAndPropTool`

File: `Assets/Editor/MapCellAndPropTool.cs`

Mở bằng:

```text
Tools → Map → Cell Debug & Prop Tool
```

Công dụng:

- xem cell dưới con trỏ;
- copy tọa độ cell/world;
- chọn vùng bằng hai click và đặt tên vùng;
- hiển thị một vùng hoặc tất cả vùng;
- đặt/xóa prop thủ công;
- thêm collider;
- lưu vùng vào `Map Region Collection.asset`;
- rải prop theo vùng.

Lưu ý: chức năng rải theo mật độ chỉ phù hợp cho vùng trang trí tự do. **Không dùng scatter ngẫu nhiên để tái tạo toàn bộ ảnh tham chiếu**.

### `SetupMapProps`

File: `Assets/Editor/SetupMapProps.cs`

Menu:

```text
Tools → Map → Place Props From Reference Layout
```

Script này đã có logic:

- đọc `full trang tri-1.png`;
- dò Sprite vật thể trong ảnh phẳng;
- chuyển vị trí pixel sang world;
- tạo GameObject dưới `Map Props (Generated)`;
- neo Sprite theo đáy giữa;
- đặt Sorting Layer `Dynamic`;
- thêm collider tại gốc;
- loại bớt vật thể nằm trên đường đi.

Các hằng số căn chỉnh hiện có:

```csharp
PixelsPerUnit = 100f;
MapLeftWorld = -8.96f;
MapTopWorld = 6.24f;
ReferenceTopCrop = 96;
```

Không được tự đổi các số này nếu chưa hiệu chỉnh lại bằng landmark.

### Script bổ trợ

- `Assets/Editor/BuildSpecialCropMapPreview.cs`: tạo nội dung theo vùng, collider vùng, giàn/cột đặc biệt. Có thể xóa rồi dựng lại các root do chính nó quản lý.
- `Assets/Editor/RemovePathVegetation.cs`: tham khảo quy tắc loại cây khỏi đường.
- `Assets/Scripts/World/MapRegionGeneratedProp.cs`: lưu tên vùng và cell nguồn trên GameObject sinh tự động.

## 4. Quy trình bắt buộc khi bot khác trồng lại

### Bước 1 — Sao lưu và không phá dữ liệu nền

Trước khi sinh lại cây:

1. Kiểm tra Git diff của:
   - `Level_Farm.unity`;
   - `Map Layout Preview.prefab`;
   - `Map Region Collection.asset`.
2. Không dùng `git reset --hard`, không restore cả scene nếu scene còn thay đổi thủ công của người dùng.
3. Chỉ được xóa root cây do tool quản lý, ví dụ `Map Props (Generated)`. Không xóa các GameObject ngoài root này.

### Bước 2 — Hiệu chỉnh hệ tọa độ bằng ít nhất ba landmark

Ảnh tham chiếu và Tilemap Unity có thể lệch crop hoặc offset. Trước khi đặt hàng loạt, chọn ít nhất ba điểm rõ ràng nằm xa nhau, ví dụ:

- góc nhà;
- góc ao;
- giao lộ đường đi;
- một gốc cây lớn dễ nhận biết.

Với mỗi landmark:

1. Ghi tọa độ pixel trong ảnh tham chiếu.
2. Chia theo lưới 16×16 để lấy cell ảnh.
3. Dùng `Cell Debug & Prop Tool` lấy cell Unity tương ứng.
4. Xác nhận cả ba điểm cho cùng một phép tịnh tiến, không bị flip hoặc xoay.

Nếu ba điểm không khớp, dừng lại và sửa mapping; không bù sai số riêng cho từng cây.

### Bước 3 — Phân tích từng ô 16×16

Quét ảnh từ trên xuống dưới, trái sang phải theo cell:

```text
sourceCellX = floor(pixelX / 16)
sourceCellY = floor(pixelY / 16)
```

Đối với vật thể lớn chiếm nhiều cell:

- không tạo một cây cho mỗi cell tán lá;
- tìm thân cây đi xuống vị trí tiếp đất;
- chọn đúng **một anchor cell** tại giữa chân/gốc;
- ghi loại Sprite và tỉ lệ tương ứng.

Ví dụ dữ liệu trung gian cần tạo:

```text
sourceCell=(42,18), targetCell=(-11,7), sprite=cay-lon-1, scale=1.0
sourceCell=(45,19), targetCell=(-8,6),  sprite=cay-nho-1, scale=1.0
```

Nên lưu danh sách chính xác này thành manifest (ScriptableObject, JSON hoặc CSV). Không chỉ giữ trong biến tạm của EditorWindow.

Manifest tối thiểu phải có:

```text
id
sourceCell
targetCell
spriteAssetGuid hoặc sprite reference
scale
rotation/flip nếu có
addCollider
regionName
```

### Bước 4 — Phân loại mặt đất trước khi đặt

Không đặt cây nếu anchor nằm trên:

- đường đi;
- nước;
- mái/sàn nhà;
- khu giàn Dưa chuột hoặc Thanh long;
- ô bảng khóa/tương tác;
- hàng rào/cổng;
- vùng canh tác cần để trống;
- vị trí NPC hoặc điểm spawn Player.

Phải kiểm tra **anchor ở gốc cây**, không kiểm tra tâm của toàn Sprite. Tán cây được phép phủ lên cỏ hoặc đường ở phía sau về mặt hình ảnh, nhưng gốc không được nằm trên đường.

### Bước 5 — Đặt GameObject theo đáy giữa Sprite

Dùng cùng công thức với tool hiện có:

```csharp
Vector3 anchor = grid.GetCellCenterWorld(targetCell);
Bounds bounds = sprite.bounds;
Vector3 bottomCenterOffset = new Vector3(bounds.center.x, bounds.min.y, 0f);
prop.transform.position = anchor - bottomCenterOffset * scale;
```

Yêu cầu:

- `localScale = (scale, scale, 1)`;
- SpriteRenderer dùng Sorting Layer `Dynamic`;
- sorting order dựa trên điểm gốc:

```csharp
renderer.sortingOrder = Mathf.RoundToInt(-anchor.y * 100f);
```

Nhờ vậy Player đứng phía trên gốc sẽ bị cây che, còn đứng phía dưới gốc sẽ hiện trước cây.

### Bước 6 — Collider chỉ nằm ở thân/gốc

Không dùng collider phủ toàn tán cây. Mẫu hiện có:

```csharp
collider.size = new Vector2(
    Mathf.Max(0.06f, bounds.size.x * 0.28f),
    Mathf.Max(0.05f, bounds.size.y * 0.14f));
collider.offset = new Vector2(
    bounds.center.x,
    bounds.min.y + collider.size.y * 0.5f);
```

Điều chỉnh nhẹ theo từng Sprite nếu thân lệch tâm, nhưng collider vẫn phải bám chân cây.

Không thêm collider cho cỏ nhỏ hoặc chi tiết nền không chặn đường.

### Bước 7 — Lưu ở Edit Mode

- Tất cả cây phải tồn tại trong `Level_Farm.unity` trước khi Play.
- Dùng `Undo.RegisterCreatedObjectUndo` khi tool tạo vật thể.
- Đánh dấu scene dirty và lưu scene sau khi người dùng xác nhận.
- Mỗi GameObject sinh tự động nên có `MapRegionGeneratedProp`, gồm `regionName` và `sourceCell`/`targetCell` để truy vết.
- Chạy tool lần hai phải cho kết quả giống lần một, không nhân đôi cây.

## 5. Quy tắc riêng của map hiện tại

### Đường đi

- Tuyệt đối không có gốc cây, bụi hoặc cỏ mọc trên đường.
- Nếu ảnh tham chiếu cho cảm giác tán cây phủ qua đường, chỉ được giữ khi gốc thật sự nằm ngoài đường.

### Vùng rừng

- Vùng được đặt tên là rừng phải dùng cây trong `trang trí map-tách riêng`.
- Quy tắc hình dáng: **2 cây nhỏ → 2 cây lớn → lặp lại**.
- Đây là quy tắc thứ tự kiểu cây, không có nghĩa là lấp kín mọi cell.
- Mật độ cuối cùng đã được yêu cầu giảm hai lần 50%; tức nên còn khoảng **25% so với mật độ rừng ban đầu**.
- Kết quả phải trải đều toàn bộ vùng được đánh dấu, không dồn hết về mép trái.
- Nếu cần ngẫu nhiên khoảng trống, dùng seed cố định để kết quả tái lập được.

### Chuối trang trí

- Chỉ dùng giai đoạn **5 và 6** của `bananatree_200.png` (không dùng giai đoạn 4).
- Scale trang trí hiện yêu cầu khoảng `0.5` để đồng bộ kích thước cây khác.
- Chuối này là cây tĩnh, không lớn và không sinh trái.
- Không nhầm với Chuối người chơi trồng.

### Vùng trồng cây

- Chỉ vùng mang ý nghĩa `Vùng trồng cây bình thường, cây lâu năm cũng có thể trồng trên này` nhận nông sản bình thường.
- Cây lâu năm vẫn tuân thủ quy tắc khoảng cách tâm ô giữa của vùng 3×3.
- Vùng Dưa chuột chỉ nhận Dưa chuột.
- Vùng Thanh long chỉ nhận Thanh long.
- Không dùng tool trang trí để lấp cây môi trường vào các vùng này.

## 6. Kiến trúc tool nên sửa hoặc viết thêm

Khuyến nghị không nhồi toàn bộ chức năng vào `MapCellAndPropTool`. Tạo một tool chuyên biệt, ví dụ:

```text
Assets/Editor/MapReferencePropImporter.cs
Assets/Settings/Map Decorations/Map Prop Placement Manifest.asset
```

Menu đề nghị:

```text
Tools → Map → Import Trees From 16x16 Reference
```

Tool nên có các nút:

1. `Analyze Reference` — đọc ảnh, sinh manifest nhưng chưa sửa scene.
2. `Preview Anchors` — vẽ marker ở từng gốc, hiển thị source cell/target cell/Sprite.
3. `Validate` — báo cây trên đường, trùng anchor, Sprite thiếu, cell ngoài map.
4. `Apply To Level_Farm` — chỉ dựng lại root do tool quản lý.
5. `Remove Generated Props` — chỉ xóa root do tool tạo, hỗ trợ Undo.

Phân tích ảnh tự động có thể tái sử dụng phần so khớp pixel trong `SetupMapProps.FindMatches`, nhưng kết quả bắt buộc phải được chuẩn hóa về anchor cell 16×16 và cho xem preview trước khi Apply.

## 7. Các lỗi đã gặp và phải tránh

- Rải cây ngẫu nhiên nên không giống ảnh tham chiếu.
- Chỉ trồng ở mép trái dù vùng rừng rộng: thường do duyệt sai `MinCell/MaxCell`, giới hạn số lượng quá sớm hoặc kiểm tra visible tile bằng sai Tilemap.
- Cây chuối quá lớn: phải dùng scale trang trí riêng, không lấy scale cây gameplay.
- Cây mọc trên đường: kiểm tra nhầm tâm Sprite thay vì gốc.
- Map bị khoét/mất đất: tool cây không được gọi `SetTile(cell, null)` trên Tilemap nền.
- Chạy lại tool làm nhân đôi: cần xóa đúng generated root hoặc dùng placement ID ổn định.
- Collider phủ tán làm Player bị chặn vô lý: collider chỉ ở thân/gốc.
- Tree/Player hiển thị sai trước sau: sorting order phải lấy theo world Y của gốc.
- Thay đổi chỉ tồn tại lúc Play: phải sinh và lưu GameObject ở Edit Mode.
- Xóa nhầm biển khóa, giếng hoặc NPC: các đối tượng gameplay không được nằm trong root cây sinh tự động.

## 8. Tiêu chí nghiệm thu

Chỉ coi là hoàn tất khi đạt toàn bộ:

- [ ] Có manifest liệt kê từng vật thể theo source cell 16×16 và target Unity cell.
- [ ] Preview anchor trùng giữa chân/gốc trong ảnh tham chiếu.
- [ ] Cây được tạo thành GameObject riêng trong `Level_Farm.unity`.
- [ ] Không sửa hoặc khoét `Map Layout Preview.prefab`.
- [ ] Không có gốc cây trên đường, nước, nhà, hàng rào, bảng khóa hoặc vùng trồng đặc biệt.
- [ ] Rừng phủ đúng toàn vùng, không dồn sang trái, theo mẫu 2 nhỏ + 2 lớn và mật độ cuối khoảng 25% mật độ ban đầu.
- [ ] Chuối trang trí chỉ dùng stage 5/6, scale 0.5, không phát triển.
- [ ] Collider bám thân/gốc; cỏ nhỏ không có collider.
- [ ] Player bị che khi đứng sau cây và hiện trước khi đứng trước cây.
- [ ] Đóng/mở Unity vẫn còn cây.
- [ ] Apply lần hai không nhân đôi và cho cùng kết quả.
- [ ] Git diff chỉ chứa các file dự kiến, không xóa thay đổi thủ công khác.

## 9. Lệnh giao việc ngắn cho bot tiếp theo

Có thể gửi nguyên văn:

```text
Đọc docs/MAP_TREE_PLACEMENT_16X16_HANDOFF.md và thực hiện đúng toàn bộ quy trình.
Phân tích Assets/Sprites/tileset/full trang tri-1.png theo lưới 16x16 pixel,
lấy giữa chân/gốc mỗi cây làm anchor, tạo manifest vị trí trước, rồi đặt từng cây
thành GameObject riêng trong Assets/Scenes/Levels/OutDoors/Level_Farm.unity.
Không scatter ngẫu nhiên toàn map, không vẽ cây vào Tilemap, không xóa/khoét terrain,
không chạm các object gameplay ngoài generated root. Trước khi Apply phải có preview
anchor và validate đường đi/nước/vùng trồng. Chạy lại phải không nhân đôi.
```

