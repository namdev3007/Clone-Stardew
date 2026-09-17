# Kế hoạch: Khu giàn Dưa leo, Thanh long và giếng lấy nước

> Trạng thái: **chỉ lập kế hoạch, chưa triển khai gameplay**  
> Cập nhật: 2026-09-16

> Tiến trình tiếp theo sau khi sửa khu Thanh long được mô tả tại [HOME_ORCHARD_UNLOCK_PLAN.md](./HOME_ORCHARD_UNLOCK_PLAN.md): Ông Nội giới thiệu đất quanh nhà và chỉ sau đó mới mở bán Chuối/Xoài.

## 1. Mục tiêu

- Có hai khu trồng đặc biệt, mỗi khu chỉ nhận đúng loại cây của nó:
  - Khu giàn Dưa leo.
  - Khu cột Thanh long.
- Ban đầu cả hai khu ở trạng thái hỏng, chưa thể cuốc/trồng.
- Mỗi khu có một bảng khóa để người chơi tương tác và trả **30 xu** sửa chữa.
- Bắt buộc sửa khu Dưa leo trước; chưa sửa Dưa leo thì không được sửa khu Thanh long.
- Trạng thái sửa chữa và tiền đã trả phải được lưu theo save slot.
- Hạt Dưa leo chỉ mở bán sau khi khu Dưa leo đã sửa.
- Hạt Thanh long chỉ mở bán sau khi khu Thanh long đã sửa.
- Giếng có sẵn trên map phải nạp đầy bình tưới giống hồ nước: một lần lấy là đầy, không chạy animation tưới.

## 2. Hiện trạng đã kiểm tra

### Đã có

- Sprite Dưa leo:
  - `Assets/Sprites/cây trồng/Cay đặc biệt/dưa leo/cucumber.png` — 10 giai đoạn.
  - `Assets/Sprites/cây trồng/Cay đặc biệt/dưa leo/cucumber_product.png`.
  - `Assets/Sprites/cây trồng/Cay đặc biệt/dưa leo/brokenfence_200.png`.
  - `Assets/Sprites/cây trồng/Cay đặc biệt/dưa leo/cucumberfence_200.png`.
- Sprite Thanh long:
  - `Assets/Sprites/cây trồng/Cay đặc biệt/cây thanh long/cây thanh long_200.png` — 8 giai đoạn.
  - `Assets/Sprites/cây trồng/Cay đặc biệt/cây thanh long/trái-thanh-long-thu hoạch_200.png`.
  - `Assets/Sprites/cây trồng/Cay đặc biệt/cây thanh long/cột thanh long hỏng_200.png`.
- Bảng khóa: `Assets/Sprites/props-items/ban-khoa.png`.
- UI Yes/No và `ConfirmationWindow` có thể tái sử dụng cho xác nhận trả tiền.
- Tiền là item vô hình `Item_Gold`, shop đã có API kiểm tra/trừ tiền.
- `Crop`, `CropDefinition`, `ItemAction_PlantSeed` đã hỗ trợ:
  - thời gian lớn lần đầu;
  - thời gian regrowth;
  - số lần thu hoạch;
  - sản lượng mỗi lần;
  - nghỉ 10 giây sau thu hoạch;
  - yêu cầu tưới trong chu kỳ mới;
  - save/load cây đang lớn.
- `Map Cell & Prop Tool` đã hiển thị tọa độ ô, trạng thái trồng/nước và có thể dùng để đánh dấu chính xác vùng giàn.
- Bình tưới đã có nhánh lấy nước tức thì và nạp đầy khi `GridManager.HasWater(cell)` trả về true.

### Chưa có

- `CropDefinition` cho Dưa leo và Thanh long.
- Item hạt, item nông sản và action gieo cho hai cây.
- Hai mask vùng trồng đặc biệt.
- Controller bảng khóa/sửa chữa.
- Save data cho trạng thái hai khu.
- Điều kiện shop dựa trên khu đã sửa.
- Logic giếng là nguồn nạp nước.

## 3. Luồng chơi dự kiến

### 3.1. Khu Dưa leo

1. Khu hiện sprite giàn hỏng và bảng khóa.
2. Khi tương tác bảng khóa:
   - Nếu dưới 30 xu: hiện thông báo không đủ tiền, không thay đổi gì.
   - Nếu đủ tiền: mở hộp Yes/No: `Repair the cucumber trellis for 30Đ?`.
3. Chọn No: đóng hộp, không trừ tiền.
4. Chọn Yes:
   - trừ đúng 30 xu một lần;
   - lưu `cucumberTrellisRepaired = true`;
   - tắt hình giàn hỏng và bảng khóa;
   - bật hình giàn đã sửa, collider và các ô trồng Dưa leo;
   - mở bán hạt Dưa leo trong shop.
5. Người chơi chỉ gieo được hạt Dưa leo vào các ô của khu này.

### 3.2. Khu Thanh long

1. Ban đầu hiện cột hỏng và bảng khóa.
2. Nếu Dưa leo chưa sửa, tương tác chỉ hiện: `Repair the cucumber trellis first.`
3. Nếu Dưa leo đã sửa, cho xác nhận: `Repair the dragon fruit field for 30Đ?`.
4. Khi trả tiền thành công:
   - trừ 30 xu;
   - lưu `dragonFruitTrellisRepaired = true`;
   - đổi hình hỏng sang hình đã sửa;
   - bật các ô trồng Thanh long;
   - mở bán hạt Thanh long trong shop.
5. Người chơi chỉ gieo được hạt Thanh long vào các ô của khu này.

### 3.3. Trình tự khóa

```text
Khu Dưa leo hỏng
    -> trả 30Đ
Khu Dưa leo đã sửa + hạt Dưa leo mở bán
    -> cho phép trả 30Đ ở khu Thanh long
Khu Thanh long đã sửa + hạt Thanh long mở bán
```

Việc sửa khu Thanh long chỉ phụ thuộc khu Dưa leo đã sửa; không bắt buộc phải thu hoạch Dưa leo trước. Nếu sau này muốn thêm điều kiện thu hoạch, chỉ cần bổ sung một cờ tiến trình, không phải đổi cấu trúc khu trồng.

## 4. Thiết kế dữ liệu cây

| Thuộc tính | Dưa leo | Thanh long |
|---|---:|---:|
| Giá hạt | 6Đ | 20Đ |
| Thời gian lần đầu | 105 giây | 450 giây |
| Tối đa khi thiếu phân + nước | 126 giây | 471 giây |
| Thời gian regrowth | 35 giây | 180 giây |
| Tối đa regrowth thiếu nước | 56 giây | 201 giây |
| Sản lượng/lần | 3 | 3 |
| Giá bán/trái | 3Đ | 10Đ |
| Nghỉ sau thu hoạch | 10 giây | 10 giây |
| Số lần thu hoạch | 3 lần tổng cộng | Tạm để vô hạn |

Ghi chú:

- Dưa leo có 2 lần regrowth, tổng cộng 3 lần ra quả, sau đó cây biến mất.
- Thanh long có bảng thời gian regrowth nhưng chưa có giới hạn số lần thu hoạch trong yêu cầu trước đây. Kế hoạch tạm dùng vô hạn giống cây lâu năm; có thể đổi thành số cố định chỉ bằng dữ liệu.
- Hai cây không dùng footprint 3x3 của Chuối/Xoài; mỗi cây chiếm một ô được chỉ định trong khu giàn.
- Vẫn dùng quy trình hiện tại: cuốc ô hợp lệ → bón phân nếu muốn tránh phạt → gieo → tưới để bỏ phần phạt thời gian.

## 5. Kiến trúc đề xuất

### 5.1. `SpecialCropAreaController`

Tạo component mới cho từng khu với dữ liệu:

- `areaId`: `CucumberTrellis` hoặc `DragonFruitTrellis`.
- `repairCost = 30`.
- `requiredAreaId`: khu Thanh long yêu cầu `CucumberTrellis`; khu Dưa leo để trống.
- `brokenVisualRoot`.
- `repairedVisualRoot`.
- `lockSignRoot`.
- `interactionField`.
- `allowedCellMask` hoặc danh sách tọa độ ô.
- `allowedCropDefinition`.

Nhiệm vụ:

- đọc trạng thái save khi scene mở;
- bật/tắt đúng hình hỏng, hình đã sửa, bảng khóa và collider;
- mở hộp xác nhận;
- kiểm tra và trừ tiền theo giao dịch nguyên tử;
- phát event khi sửa xong để shop và UI cập nhật.

### 5.2. `SpecialCropProgressService`

Lưu riêng theo save slot:

```text
version
cucumberTrellisRepaired
dragonFruitTrellisRepaired
```

Không nhét trực tiếp vào `TutorialProgressService`, vì đây là tiến trình mở khu lâu dài chứ không phải bước hướng dẫn NPC. Service cung cấp:

- `IsAreaRepaired(areaId)`.
- `CanRepair(areaId)`.
- `MarkAreaRepaired(areaId)`.
- event `AreaStateChanged`.

Save version bắt đầu từ 1; save cũ mặc định cả hai khu chưa sửa và không bị lỗi deserialize.

### 5.3. Mask vùng trồng đặc biệt

Không hard-code world position và không dùng màu pixel để suy đoán vùng giàn. Tạo hai Tilemap logic ẩn dưới `Grid Manager`:

- `Cucumber Plot Mask`.
- `Dragon Fruit Plot Mask`.

Các ô được tô bằng logic tile không renderer. Dùng `Map Cell & Prop Tool` để lấy/kiểm tra tọa độ rồi tô đúng từng ô.

Quy tắc trong `GridManager.TryBeginPlanting`:

- Hạt Dưa leo:
  - khu Dưa leo phải đã sửa;
  - ô phải thuộc `Cucumber Plot Mask`;
  - không cho trồng ở ruộng thường hoặc khu Thanh long.
- Hạt Thanh long:
  - khu Thanh long phải đã sửa;
  - ô phải thuộc `Dragon Fruit Plot Mask`;
  - không cho trồng ở ruộng thường hoặc khu Dưa leo.
- Các cây khác:
  - không cho gieo vào hai mask đặc biệt;
  - vẫn dùng ruộng thường như hiện tại.
- Khi khu còn hỏng:
  - không cho cuốc, bón phân, gieo hoặc tưới các ô bị khóa.

Để tránh viết điều kiện theo tên item, thêm loại vùng vào `CropDefinition`:

```text
PlantingZone.Normal
PlantingZone.CucumberTrellis
PlantingZone.DragonFruitTrellis
```

### 5.4. Visual khu giàn

Hai khu hỏng hiện đã nằm trong bố cục map gốc. Khi triển khai cần đưa phần visual thay đổi ra khỏi tile nền tĩnh:

1. Xóa/che sạch đúng các tile hình giàn hỏng trong `Map Layout Preview` bằng tile đất nền phù hợp.
2. Tạo prefab/GameObject riêng cho từng khu:
   - root hình hỏng;
   - root hình đã sửa;
   - bảng khóa;
   - collider cản người chơi;
   - mask ô trồng.
3. Trạng thái hỏng bật collider ngăn đi xuyên vào kết cấu đổ vỡ.
4. Trạng thái đã sửa thay collider theo hàng cột/giàn thực tế nhưng vẫn cho người chơi tiếp cận ô trồng.
5. Sorting dùng cùng nguyên tắc Y-sort hiện có để người chơi đứng trước/sau giàn đúng lớp.

`SetupNewFarmMap` phải tạo hoặc giữ lại hai prefab vùng đặc biệt sau mỗi lần cài lại map, tránh tool map xóa trạng thái authored trong scene.

## 6. Asset và ScriptableObject cần tạo

### Dưa leo

- `Crop Cucumber.asset`.
- `Item_Seed_Cucumber.asset`.
- `Item_Crop_Cucumber.asset`.
- `Item Action Plant Seed Cucumber.asset`.
- Thêm tất cả vào `ScriptableAssetDatabase` để save/load tìm lại bằng GUID.

Cấu hình dự kiến:

- `firstGrowthSeconds = 105`.
- `regrowthSeconds = 35`.
- `maximumHarvests = 3`.
- `harvestYield = 3`.
- `regrowthStageStart`: xác định bằng Play Mode sau khi xem giai đoạn nào là thân cây sau thu hoạch.
- `plantingZone = CucumberTrellis`.
- 10 sprite tăng trưởng, căn bottom-center từng frame bằng `stagePositionOffsets`.

### Thanh long

- `Crop Dragon Fruit.asset`.
- `Item_Seed_DragonFruit.asset`.
- `Item_Crop_DragonFruit.asset`.
- `Item Action Plant Seed DragonFruit.asset`.
- Đăng ký vào `ScriptableAssetDatabase`.

Cấu hình dự kiến:

- `firstGrowthSeconds = 450`.
- `regrowthSeconds = 180`.
- `maximumHarvests = int.MaxValue` tạm thời.
- `harvestYield = 3`.
- `regrowthStageStart`: frame thân cây sau thu hoạch, xác nhận bằng demo mọi giai đoạn.
- `plantingZone = DragonFruitTrellis`.
- 8 sprite tăng trưởng, căn bottom-center riêng từng frame.

## 7. Shop

Mở rộng `NpcShopCatalog.Entry` bằng điều kiện khu:

```text
requiredSpecialArea = None | CucumberTrellis | DragonFruitTrellis
```

- Cucumber Seed: giá 6Đ, chỉ hiển thị khi khu Dưa leo đã sửa.
- Dragon Fruit Seed: giá 20Đ, chỉ hiển thị khi khu Thanh long đã sửa.
- Không đưa Water Can vào shop.
- Khi sửa khu trong lúc shop chưa mở, lần mở tiếp theo danh mục tự rebuild.
- Nếu shop đang mở bằng một luồng debug, event sửa khu cũng gọi refresh để tránh cache danh mục cũ.

## 8. Giếng lấy nước

Không thêm ô giếng vào `Water Tilemap`, vì `HasWater` hiện còn được dùng để chặn cuốc/trồng. Nếu coi giếng là nước trực tiếp, logic ruộng có thể hiểu sai các ô xung quanh.

Tạo `WaterRefillSource`:

- đặt lên từng GameObject giếng có trong map;
- có `BoxCollider2D` hoặc danh sách cell nạp nước;
- lúc enable đăng ký các cell với `GridManager`;
- lúc disable hủy đăng ký.

Mở rộng `GridManager`:

- `RegisterWaterRefillCell(cell)`.
- `UnregisterWaterRefillCell(cell)`.
- `CanRefillWaterAt(cell)` trả true nếu:
  - là tile hồ hiện tại; hoặc
  - là cell của giếng.

Sửa `ItemAction_WaterCan` chỉ ở nhánh xác định nguồn lấy nước:

```text
obtainingWater = gridManager.CanRefillWaterAt(location)
```

Sau đó dùng nguyên nhánh hiện có:

- nạp `current = max` ngay lập tức;
- cập nhật gauge trên đầu player;
- refresh inventory slot;
- không animation tưới;
- không trừ nước.

Hai giếng nhìn thấy trong map trang trí đều nên gắn source nếu cả hai còn tồn tại trong `Map Layout Preview` sau khi cài map.

## 9. UI và nội dung thông báo

Tái sử dụng nền Yes/No hiện tại, không tạo một hệ UI xác nhận thứ hai.

Text tiếng Anh giai đoạn đầu:

- Dưa leo: `Repair the cucumber trellis for 30Đ?`
- Thanh long: `Repair the dragon fruit field for 30Đ?`
- Thiếu tiền: `You need 30Đ to repair this area.`
- Sai thứ tự: `Repair the cucumber trellis first.`
- Thành công Dưa leo: `The cucumber trellis has been repaired.`
- Thành công Thanh long: `The dragon fruit field has been repaired.`

Trong lúc hộp xác nhận mở:

- khóa di chuyển và dùng công cụ;
- chặn mở bag/shop/pause chồng lên;
- Esc đóng hộp xác nhận trước, không mở Pause Menu.

## 10. Công cụ authoring

Nâng `Map Cell & Prop Tool` thêm chế độ đánh dấu vùng đặc biệt:

- Brush `Cucumber Plot`.
- Brush `Dragon Fruit Plot`.
- Brush `Well Refill Cell`.
- Màu overlay khác nhau cho ba loại.
- Hiển thị đồng thời tọa độ và trạng thái vùng.
- Nút validate:
  - không có ô nằm trong cả hai khu;
  - không có ô giàn nằm trên đường đi/nước;
  - khu Thanh long có bảng khóa và dependency;
  - mọi giếng có ít nhất một refill cell.

Việc này giúp chỉnh map sau này mà không sửa code hoặc nhập tọa độ thủ công.

## 11. Trình tự triển khai sau khi duyệt

1. Tạo enum vùng trồng và service lưu trạng thái sửa chữa.
2. Tạo `SpecialCropAreaController` và luồng xác nhận/trừ 30Đ.
3. Tách hai hình giàn hỏng khỏi nền map; dựng hai prefab vùng trong `Level_Farm`.
4. Tạo hai Tilemap mask và tô các ô bằng tool debug.
5. Chặn cuốc/bón/gieo theo trạng thái khu và loại cây.
6. Tạo đầy đủ CropDefinition, hạt, nông sản, action gieo và đăng ký save database.
7. Thêm hai hạt vào shop với gate theo khu sửa chữa.
8. Tạo `WaterRefillSource`, gắn vào giếng và đổi bình tưới sang `CanRefillWaterAt`.
9. Thêm cheat/debug:
   - cộng tiền;
   - khóa/mở lại từng khu;
   - trồng mọi giai đoạn Dưa leo/Thanh long;
   - hiển thị mask vùng đặc biệt và refill cell.
10. Chạy compile, Play Mode test, save/load test và kiểm tra scene sau khi chạy lại tool cài map.

## 12. Checklist kiểm thử

### Thanh toán và thứ tự

- [ ] Dưới 30Đ không sửa được và không bị trừ tiền.
- [ ] No không trừ tiền.
- [ ] Yes trừ đúng 30Đ một lần.
- [ ] Tương tác lại khu đã sửa không trừ thêm.
- [ ] Không sửa được Thanh long trước Dưa leo.
- [ ] Save/load giữ đúng hai cờ và số tiền.

### Trồng cây

- [ ] Dưa leo không trồng được ở ruộng thường hoặc khu Thanh long.
- [ ] Thanh long không trồng được ở ruộng thường hoặc khu Dưa leo.
- [ ] Cây thường không trồng được trong hai khu đặc biệt.
- [ ] Khu hỏng không cuốc/bón/gieo/tưới được.
- [ ] Giữ chuột để gieo nhiều ô vẫn tôn trọng mask.
- [ ] Dưa leo lớn 105/126 giây và regrowth 35/56 giây.
- [ ] Dưa leo biến mất sau lần thu hoạch thứ ba.
- [ ] Thanh long lớn 450/471 giây và regrowth 180/201 giây.
- [ ] Hai cây cho đúng 3 sản phẩm mỗi lần.
- [ ] Sprite mọi giai đoạn không lệch chân và sorting đúng với player.

### Shop

- [ ] Chưa sửa khu thì không thấy hạt tương ứng.
- [ ] Sửa Dưa leo chỉ mở hạt Dưa leo.
- [ ] Sửa Thanh long mới mở hạt Thanh long.
- [ ] Giá mua/bán đúng và đơn vị là `Đ`.

### Giếng

- [ ] Chĩa bình vào giếng và dùng một lần thì bình đầy.
- [ ] Không chạy animation tưới khi lấy nước.
- [ ] Gauge cập nhật ngay.
- [ ] Hồ nước vẫn hoạt động như cũ.
- [ ] Giếng không biến các ô đất gần đó thành tile nước và không phá logic trồng.

## 13. Điểm cần chốt trước khi triển khai

Kế hoạch hiện dùng các giả định an toàn sau:

1. Thanh long regrowth vô hạn vì chưa có giới hạn số lần thu hoạch.
2. Sửa xong khu là mở bán hạt ngay; không bắt buộc thu hoạch cây trước đó.
3. Hai cây vẫn dùng cuốc/phân/tưới như hệ cây hiện tại, chỉ khác vùng được phép trồng.
4. Cả hai giếng nhìn thấy trên map đều lấy được nước.

Nếu thiết kế cuối khác bốn điểm này, chỉ cần chỉnh dữ liệu/gate trước khi bắt đầu triển khai.
