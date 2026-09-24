# Kế hoạch căn chỉnh lại toàn bộ cây trên map

## 1. Mục tiêu

Căn lại chính xác vị trí của:

- cây, bụi và cỏ trang trí được sinh từ `full trang tri-1.png`;
- chuối trang trí tĩnh dùng stage 5/6;
- Chuối và Xoài do người chơi trồng;
- collider thân/gốc và thứ tự hiển thị trước/sau Player.

Không sửa bằng cách cộng một offset chung cho mọi đối tượng. Cây trang trí và cây gameplay có hai nguồn tọa độ khác nhau nên phải kiểm tra, sửa và nghiệm thu riêng.

## 2. Hiện trạng đã xác nhận

- Grid trong `Level_Farm.unity` có cell size `0.16 x 0.16`, transform tại `(0,0,0)`.
- Ảnh tham chiếu `full trang tri-1.png` có kích thước `1808 x 1328`, tương đương `113 x 83` ô 16x16.
- Ảnh nền cũ `bố cục map.png` cao 77 ô; map mới đã thêm 6 hàng ở phía trên.
- Map hiện được neo cố định tại cell gốc `(-56,-38)` để phần cũ không bị dịch khi thêm hàng trên.
- Manifest hiện có 846 entry, gồm 825 hợp lệ và 21 bị loại; lần phân tích cuối được ghi là `2026-09-21 02:27:09`.
- Cây trang trí được lưu trong `Map Prop Placement Manifest.asset` và dựng vào root `Map Props (Generated)` của `Level_Farm.unity`.
- Chuối/Xoài gameplay không dùng manifest. Root cây nằm ở ô trồng, còn Sprite con được dịch theo pivot và `stagePositionOffsets` trong từng `CropDefinition`.
- `stagePositionOffsets` của Chuối/Xoài hiện đều bằng 0, vì vậy frame bị trim hoặc pivot khác nhau có thể làm thân/gốc nhảy khỏi ô dù root gameplay vẫn đúng.

## 3. Nguyên tắc an toàn

1. Không sửa hoặc xóa tile trong `Map Layout Preview.prefab`.
2. Không dịch cả `Grid Manager`, Tilemap hoặc root map để đuổi theo cây.
3. Không đụng NPC, biển khóa, giếng, hàng rào, ruộng Dưa chuột/Thanh long trong bước sửa cây.
4. Không xóa cây thủ công ngoài root do tool quản lý.
5. Không dùng Play Mode để ghi vị trí về scene.
6. Mọi thay đổi scene phải có Undo và chỉ lưu sau khi Validate đạt.
7. Không chạy các script rải thêm cây/cỏ trước khi hệ tọa độ cơ sở đã đúng.

## 4. Nguồn tọa độ chuẩn duy nhất

Nguồn chuẩn là Grid trong `Level_Farm.unity`:

```text
cell size = 0.16
map min cell = (-56,-38)
map size = 113 x 83
map max cell dự kiến = (56,44)
```

Một gốc cây tại `targetCell=(x,y)` phải dùng ground anchor:

```text
anchor.x = CellToWorld(targetCell).x + 0.08
anchor.y = CellToWorld(targetCell).y
```

Không tiếp tục duy trì song song các hằng số `MapLeftWorld`, `MapTopWorld`, offset preview và công thức chia world thủ công. Tool phải lấy origin/cell size trực tiếp từ Grid và bounds thật của Tilemap.

## 5. Giai đoạn A — Chụp hiện trạng và khóa tự động ghi scene

1. Dừng Play Mode và chờ Unity compile xong.
2. Ghi danh sách đầy đủ object dưới:
   - `Map Props (Generated)`;
   - `Named Region Content (Generated)`;
   - các nhóm cây/cỏ sinh thêm;
   - cây thủ công không thuộc các root trên.
3. Xuất báo cáo CSV/JSON gồm:
   - object name;
   - prop ID;
   - sprite;
   - target cell;
   - world position;
   - world position của đáy giữa Sprite;
   - collider bounds;
   - scene sở hữu object.
4. Tạm ngăn các `InitializeOnLoad` tự Apply lại manifest trong lúc hiệu chỉnh. Auto-run chỉ được phép Analyze/Validate; không tự sửa scene.
5. Không dùng Git reset vì worktree đang có nhiều thay đổi hợp lệ khác.

## 6. Giai đoạn B — Hiệu chuẩn ảnh tham chiếu với map

Chọn tối thiểu 5 landmark rõ ràng, trải đều toàn map:

1. góc trên trái;
2. góc trên phải;
3. khu nhà/NPC ở giữa;
4. góc dưới trái;
5. góc dưới phải.

Với mỗi landmark, ghi:

- cell trong ảnh 16x16;
- cell thật trong `Level_Farm` lấy bằng Cell Debug Tool;
- sai số X/Y.

Chỉ chấp nhận mapping nếu cả 5 điểm cho cùng một phép biến đổi. Nếu sai số thay đổi theo vị trí thì phải kiểm tra crop ảnh, chiều Y hoặc kích thước ảnh; tuyệt đối không bù riêng từng khu.

Mapping mục tiêu:

```text
source cell trái -> phải giữ nguyên chiều X
source cell trên -> dưới phải đảo sang chiều Y của Unity
6 hàng mới nằm phía trên, không làm dịch 77 hàng cũ
```

Kết quả của giai đoạn này là một `MapReferenceCalibration` được lưu thành asset hoặc trường serialized, không còn hằng số rải rác trong nhiều script.

## 7. Giai đoạn C — Viết chế độ Audit/Preview, chưa Apply

Bổ sung vào `MapReferencePropImporter`:

### Audit

Với mỗi manifest entry, tính ba điểm:

1. `expectedAnchor`: anchor theo Grid và target cell;
2. `manifestAnchor`: `worldAnchor` đang lưu;
3. `sceneAnchor`: đáy giữa SpriteRenderer trong scene.

Phân loại lỗi:

- `MappingError`: target cell sai so với ảnh tham chiếu;
- `ManifestWorldError`: target cell đúng nhưng worldAnchor sai;
- `SpritePivotError`: root đúng nhưng đáy/gốc Sprite lệch;
- `SceneDrift`: manifest đúng nhưng GameObject trong scene bị kéo lệch;
- `ColliderDrift`: hình đúng nhưng collider lệch;
- `ForbiddenCell`: gốc nằm trên đường, nước, nhà, hàng rào hoặc vùng trồng cấm.

### Preview

- marker xanh: anchor đúng;
- marker đỏ: anchor sai;
- đường vàng nối vị trí hiện tại tới vị trí dự kiến;
- label hiển thị `propId`, `targetCell`, sai số theo số ô và world unit;
- nút lọc: tất cả / cây / chuối trang trí / cỏ-bụi / collider.

Chưa cho phép Apply nếu:

- bất kỳ landmark nào lệch;
- có target cell ngoài bounds map;
- có hai prop ID trùng;
- có gốc cây trên vùng cấm;
- sai số toàn cục chưa được giải thích.

## 8. Giai đoạn D — Sửa cây trang trí

1. Phân tích lại `full trang tri-1.png` theo cell 16x16 bằng calibration mới.
2. Với vật thể lớn, lấy đúng giữa chân/gốc làm anchor; không lấy tâm texture hoặc tâm tán.
3. Regenerate manifest trước, chỉ xem Preview và báo cáo diff:
   - bao nhiêu cây đổi cell;
   - bao nhiêu cây chỉ đổi world anchor;
   - bao nhiêu entry bị thêm/xóa;
   - bao nhiêu cây rơi vào vùng cấm.
4. Sau khi duyệt, chỉ dựng lại `Map Props (Generated)`.
5. Đặt root object bằng ground anchor; đặt Sprite con sao cho đáy giữa trùng root.
6. Giữ nguyên các yêu cầu:
   - rừng theo chu kỳ 2 cây nhỏ + 2 cây lớn;
   - mật độ và seed tái lập được;
   - chuối trang trí chỉ stage 5/6, scale 0.5;
   - không có cây trên đường hoặc vùng canh tác đặc biệt.
7. Các script rải bổ sung như hàng cây/cụm cỏ phải tạo entry có ID trong manifest hoặc một manifest phụ. Không để object “ngoài sổ” vì lần Apply sau sẽ lệch hoặc mất.

## 9. Giai đoạn E — Sửa Chuối/Xoài gameplay

Không di chuyển root của Crop vì root là tâm footprint 3x3 và là khóa save/load.

1. Tạo preview trong Editor hiển thị toàn bộ stage của Chuối và Xoài trên cùng một ground anchor.
2. Với từng frame:
   - xác định pixel chân thân;
   - đo sai số so với đáy giữa mong muốn;
   - ghi vào `stagePositionOffsets` của `Crop Banana.asset` và `Crop Mango.asset`.
3. Nếu sprite pivot import sai đồng loạt, ưu tiên sửa Sprite Editor pivot; nếu frame được trim khác nhau, dùng `stagePositionOffsets` riêng từng stage.
4. Xác nhận root Crop luôn trùng tâm ô được chọn, không nhảy khi đổi stage.
5. Collider thân cây lấy theo root/ground anchor, không cộng lại stage offset hai lần.
6. Status icon, vùng tương tác và sorting đều lấy root hoặc bounds đã hiệu chỉnh.
7. Test cây ở:
   - ruộng thường;
   - vườn quanh nhà sau mở khóa;
   - gần mép vùng nhưng vẫn đủ footprint 3x3.
8. Save/load giữa từng stage để xác nhận cây không nhảy vị trí sau khi vào lại game.

## 10. Giai đoạn F — Collider và chiều sâu

### Cây trang trí

- collider chỉ phủ thân/gốc;
- collider center phải nằm trên cùng target cell với anchor;
- cỏ nhỏ không có collider;
- không dùng bounds toàn tán.

### Chuối/Xoài gameplay

- collider theo thân cây, root vẫn là tâm footprint;
- mỗi stage không làm collider nhảy sang ô khác;
- cây non có collider nhỏ hơn cây trưởng thành nếu cần.

### Sorting

- cùng Sorting Layer `Dynamic`;
- order dựa trên world Y của gốc, không dựa trên tâm hoặc đáy tán;
- Player đứng trước gốc: Player hiện phía trước;
- Player đứng sau gốc: cây che Player.

## 11. Giai đoạn G — Đồng bộ scene và save/load

1. Chỉ lưu cây môi trường trong `Level_Farm.unity`.
2. `Core 1.unity` chỉ giữ hệ thống/UI, không chứa bản sao cây map.
3. Mở `Core 1` với `Level_Farm` additive và so vị trí Edit Mode với Play Mode.
4. Tạo save mới, trồng Chuối/Xoài, lưu rồi load lại ở nhiều stage.
5. Kiểm tra save cũ: root cell không đổi, chỉ Sprite con được căn lại.
6. Apply tool hai lần phải cho cùng object count và cùng transform, không nhân đôi.

## 12. Thứ tự triển khai đề nghị

1. Tắt auto-apply và thêm audit xuất báo cáo.
2. Chốt bounds/origin Grid và 5 landmark.
3. Sửa mapping ảnh tham chiếu -> target cell.
4. Regenerate manifest, chỉ Preview.
5. Validate vùng cấm và diff.
6. Apply lại cây trang trí.
7. Căn riêng từng stage Chuối/Xoài gameplay.
8. Căn collider và sorting.
9. Test Edit/Play và save/load.
10. Chỉ sau khi đạt toàn bộ mới bật lại auto-run ở chế độ Validate-only.

## 13. File dự kiến được phép thay đổi

Chính:

- `Assets/Editor/MapReferencePropImporter.cs`
- `Assets/Settings/Map Decorations/Map Prop Placement Manifest.asset`
- `Assets/Scenes/Levels/OutDoors/Level_Farm.unity`
- `Assets/Scripts/World/Objects/Crop.cs` nếu công thức anchor/collider cần sửa
- `Assets/ScriptableObjects/Farming/Crop Definitions/Crop Banana.asset`
- `Assets/ScriptableObjects/Farming/Crop Definitions/Crop Mango.asset`

Có thể thêm:

- `Assets/Scripts/World/MapReferenceCalibration.cs`
- `Assets/Settings/Map Decorations/Map Reference Calibration.asset`
- báo cáo audit trong `docs` hoặc thư mục Settings.

Không được thay đổi trong công việc này:

- `Assets/Tiles/Map Layout/Map Layout Preview.prefab`
- terrain/tile nền;
- NPC, biển khóa, giếng, ruộng đặc biệt;
- các scene UI.

## 14. Tiêu chí nghiệm thu

- [ ] 5 landmark khớp tuyệt đối cùng một mapping.
- [ ] Mọi manifest entry có target cell và anchor tái tính được từ Grid.
- [ ] Không còn cây/cỏ có gốc trên đường, nước hoặc vùng cấm.
- [ ] Cây trang trí khớp ảnh tham chiếu theo chân/gốc, không theo tâm tán.
- [ ] Chuối trang trí stage 5/6 đúng vị trí và scale.
- [ ] Tất cả stage Chuối/Xoài gameplay giữ nguyên một ground anchor.
- [ ] Root Chuối/Xoài luôn đúng tâm footprint 3x3.
- [ ] Collider bám thân, không lệch khỏi cell sau đổi stage/load save.
- [ ] Player/cây che nhau đúng theo vị trí trước/sau.
- [ ] Edit Mode và Play Mode giống nhau.
- [ ] Save/load không làm cây nhảy.
- [ ] Chạy Apply lần hai không đổi kết quả và không nhân đôi object.
- [ ] Không có diff ngoài danh sách file dự kiến.

