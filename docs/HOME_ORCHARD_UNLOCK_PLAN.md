# Kế hoạch: Mở khóa đất quanh nhà, Chuối/Xoài và hoạt cảnh giới thiệu

> Trạng thái: **chỉ lập kế hoạch, chưa triển khai gameplay hoặc sửa map**  
> Cập nhật: 2026-09-16  
> Phụ thuộc: [CUCUMBER_DRAGONFRUIT_TRELLIS_WELL_PLAN.md](./CUCUMBER_DRAGONFRUIT_TRELLIS_WELL_PLAN.md)

## 1. Mục tiêu

- Ép đúng thứ tự tiến trình:
  1. Khu vườn ban đầu.
  2. Trả 30Đ sửa khu Dưa leo.
  3. Trả 30Đ sửa khu Thanh long; không thể đảo thứ tự hai khu.
  4. Ông Nội hiện dấu chấm than.
  5. Người chơi nói chuyện với Ông Nội.
  6. Camera lia tới đất quanh nhà, tự zoom để thấy đủ khu vực và bốn góc trắng nhấp nháy trong khoảng 5 giây.
  7. Đất quanh nhà được mở khóa; hạt/cây giống Chuối và Xoài mới được bán.
- Chuối và Xoài được trồng trực tiếp trên tile cỏ, không cần cuốc đất.
- Sau khi mở khóa, Chuối/Xoài được trồng trong:
  - khu vườn ban đầu;
  - khu đất quanh nhà vừa mở.
- Không cho cuốc hoặc trồng ngoài những vùng canh tác đã được khai báo.
- Cảnh giới thiệu có thể xem lại từ đầu khi tương tác lại với Ông Nội.

## 2. Sơ đồ tiến trình bắt buộc

```text
Vườn ban đầu
    -> sửa Dưa leo (30Đ)
Dưa leo đã sửa
    -> sửa Thanh long (30Đ)
Thanh long đã sửa / người chơi sở hữu 3 khu trồng
    -> Ông Nội hiện dấu !
Nói chuyện với Ông Nội
    -> camera giới thiệu đất quanh nhà + nhấp nháy 4 góc trong 5 giây
Hoạt cảnh hoàn tất
    -> mở đất quanh nhà
    -> mở bán Chuối và Xoài
```

Không dùng số tiền hiện có làm điều kiện hiện dấu chấm than. Điều kiện chính xác là **cả khu Dưa leo và khu Thanh long đã sửa thành công**. Nhờ vậy người chơi tiêu tiền sau khi sửa vẫn không làm mất tiến trình.

## 3. Trạng thái và dữ liệu lưu

Mở rộng `SpecialCropProgressService` trong kế hoạch khu giàn hoặc tạo `FarmExpansionProgressService` dùng chung:

```text
version
cucumberTrellisRepaired
dragonFruitTrellisRepaired
homeOrchardIntroPending
homeOrchardIntroSeen
homeOrchardUnlocked
clearedHomeOrchardPropIds[]
damagedHomeOrchardProps[] { propId, remainingHits }
```

State machine:

```text
Locked
  -> AwaitingGrandfather     khi Thanh long vừa sửa xong
  -> Revealing              khi thoại Ông Nội kết thúc hoặc bị Skip
  -> Unlocked               khi camera/highlight kết thúc
```

Quy tắc save/load:

- Save cũ thiếu dữ liệu mới mặc định `Locked`.
- Nếu đã sửa Thanh long nhưng chưa mở đất, load lại phải trở về `AwaitingGrandfather` và Ông Nội hiện `!`.
- Nếu thoát game giữa hoạt cảnh, load lại trở về `AwaitingGrandfather`; không tự mở đất nửa chừng.
- Sau khi đã mở, hoạt cảnh xem lại không trừ tiền, không mở khóa lần hai và không nhân đôi vật thể.
- Trạng thái vật cản đã chặt và số hit còn lại phải được khôi phục đúng.

## 4. NPC Ông Nội và hội thoại

### 4.1. Điều kiện ưu tiên

Trong `Assets/Scripts/World/NPC/NpcDialogueInteractor.cs`, nhánh hội thoại mở đất phải có ưu tiên cao hơn menu nói chuyện lặp lại bình thường:

1. Nếu `homeOrchardIntroPending`: chạy thoại mở đất.
2. Nếu đã mở đất và người chơi chọn xem lại: chạy lại thoại + camera reveal.
3. Nếu không: dùng các thoại/menu hiện có.

`Assets/Scripts/World/NPC/NpcDialogueStatus.cs` đã có trạng thái `Exclamation`, nên chỉ cần đồng bộ dấu `!` theo cờ `homeOrchardIntroPending`. Sau khi bắt đầu thoại, ẩn dấu `!`; nếu chuỗi bị gián đoạn trước khi hoàn tất thì bật lại.

### 4.2. Nội dung DialogueSequence

Tạo một `DialogueSequence` riêng, không hard-code chuỗi trong controller.

Tiếng Việt:

- Ông Nội: `Thằng cu này được việc phết nhỉ?`
- Ông Nội: `Thôi! Phần đất nhà mình vẫn còn một ít sau vườn, cháu ra đó dọn đống cỏ dại rồi đi mua ít xoài với chuối giống về mà trồng cho có ít quả mà ăn.`

Tiếng Anh:

- Grandfather: `You're actually pretty handy, huh?`
- Grandfather: `Alright, listen! We've got a small plot left in the backyard. Go clear out the weeds, then grab a few mango and banana saplings to plant so we'll have some fresh fruit to eat.`

### 4.3. Skip và xem lại

- Nút Skip chỉ bỏ qua phần chữ; callback hoàn tất dialogue vẫn phải khởi chạy cảnh camera.
- `DialogueUIController.Skip()` hiện đi qua `FinishSequence()`, nên móc reveal vào callback hoàn tất thay vì nút Next riêng.
- Sau khi đất đã mở, thêm lựa chọn trong menu Ông Nội, ví dụ `Show me the backyard plot again.`
- Lựa chọn xem lại chạy từ câu thoại đầu tiên rồi chạy lại toàn bộ camera/highlight 5 giây.

## 5. Hoạt cảnh camera và bốn góc trắng

Tạo `HomeOrchardRevealController` gồm các bước:

1. Đóng các UI xung đột và khóa di chuyển, dùng tool, đổi quick slot, mở bag/shop/pause.
2. Ghi lại camera target, vị trí và mức zoom hiện tại.
3. Lấy bounds từ `Home Orchard Mask`, cộng safety margin.
4. Lia camera tới tâm vùng đất trong khoảng 0,5–0,8 giây.
5. Zoom tạm vừa đủ để bốn góc nằm trong viewport.
6. Bật bốn marker chữ L màu trắng tại bốn góc bounds.
7. Nhấp nháy alpha nhẹ, có thể kết hợp scale 0,95–1,05, trong đúng khoảng 5 giây.
8. Tắt marker, lia/zoom về player, khôi phục camera follow.
9. Đặt `homeOrchardUnlocked = true`, mở gate shop và trả quyền điều khiển.

### Lưu ý Pixel Perfect

`Assets/Scripts/Camera/PixelPerfectCamera.cs` đang ép `Lens.OrthographicSize` theo chiều cao màn hình mỗi frame. Vì vậy không chỉnh trực tiếp Orthographic Size rồi chờ giữ nguyên. Cần một trong hai cách:

- Khuyến nghị: thêm `temporaryZoomMultiplier`/override vào `PixelPerfectCamera`; reveal controller bật override và luôn khôi phục bằng `try/finally` hoặc nhánh cleanup.
- Phương án dự phòng: dùng một Cinemachine Camera ưu tiên cao hơn, không gắn extension ép zoom, chỉ tồn tại trong reveal.

Zoom được tính từ cả chiều rộng, chiều cao vùng và aspect ratio màn hình, không dùng một con số cố định. Mục tiêu là thấy đủ bốn marker ở 16:9 và các tỷ lệ cửa sổ khác.

### Marker bốn góc

Tạo prefab `Home Orchard Corner Highlight` gồm bốn SpriteRenderer chữ L trắng:

- neo theo bounds của mask, không nhập world position thủ công;
- sorting trên tile/prop, dưới UI hội thoại;
- không collider;
- dùng unscaled time để vẫn nhấp nháy khi gameplay đang bị pause bởi chuỗi thoại;
- không lưu vào save.

Nếu tileset chưa có sprite góc trắng riêng, dùng sprite chữ L trắng đơn giản 1-bit/pixel-art; không lấy tạm asset không liên quan.

## 6. Vùng đất — chờ người dùng cung cấp tọa độ

Không đoán vùng chữ nhật từ ảnh. Tạo placeholder authoring:

```text
HOME_ORCHARD_MIN_CELL = TBD
HOME_ORCHARD_MAX_CELL = TBD
```

Hoặc người dùng cung cấp bốn cell:

```text
TOP_LEFT     = TBD
TOP_RIGHT    = TBD
BOTTOM_LEFT  = TBD
BOTTOM_RIGHT = TBD
```

Khi nhận tọa độ, tô một Tilemap logic ẩn `Home Orchard Mask`. Camera, giới hạn trồng và vị trí bốn marker đều đọc cùng mask này để không bị lệch khi map thay đổi.

Nâng `Assets/Editor/MapCellAndPropTool.cs` tại menu duy nhất **Tools → Map → Cell Debug & Prop Tool**:

- chế độ Rectangle `Home Orchard`;
- click hai góc để tô/xóa mask;
- hiển thị min/max cell và nút Copy Bounds;
- overlay màu riêng khi debug;
- validate vùng không vượt map, không chồng nước và báo những ô đường đi cần loại khỏi mask.

Nếu vùng chữ nhật chứa nhà, đường hoặc hồ, mask phải hỗ trợ các lỗ loại trừ; không được coi toàn bộ bounding rectangle là đất canh tác.

## 7. Ma trận vùng canh tác

Tạo các mask logic rõ ràng:

- `Initial Farm Mask` — vườn ban đầu.
- `Cucumber Plot Mask` — chỉ Dưa leo.
- `Dragon Fruit Plot Mask` — chỉ Thanh long.
- `Home Orchard Mask` — đất quanh nhà sau reveal.

| Hành động/cây | Vườn ban đầu | Dưa leo | Thanh long | Đất quanh nhà | Ngoài vùng |
|---|---:|---:|---:|---:|---:|
| Cuốc đất | Có | Khi đã sửa | Khi đã sửa | Khi đã mở | Không |
| Cây thường | Có | Không | Không | Có* | Không |
| Dưa leo | Không | Có | Không | Không | Không |
| Thanh long | Không | Không | Có | Không | Không |
| Chuối/Xoài | Sau khi mở đất, trồng trên cỏ | Không | Không | Sau khi mở đất, trồng trên cỏ | Không |

`*` Giả định hiện tại: sau khi mở, đất quanh nhà là vùng canh tác đầy đủ vì yêu cầu có nhắc chặn hành động cuốc bên ngoài vùng cho phép. Nếu chỉ muốn đất này dành riêng Chuối/Xoài, đổi một policy flag thay vì sửa mask.

## 8. Logic trồng Chuối và Xoài

Hiện logic cây lâu năm trong `GridManager.CanPlantPerennialTree` yêu cầu footprint 3x3 đều là đất đã cuốc. Cần tách điều kiện địa hình khỏi điều kiện footprint:

```text
CanPlantFruitTree(crop, centerCell)
  -> tiến trình home orchard đã mở
  -> crop là Banana hoặc Mango
  -> toàn bộ footprint 3x3 nằm trong Initial Farm Mask HOẶC Home Orchard Mask
  -> không nước, vật cản, crop, công trình hoặc reserved cell
  -> không yêu cầu HasDirt/đã cuốc
```

Không cho footprint 3x3 nằm vắt qua ranh giới mask. Nếu center hợp lệ nhưng một ô footprint ra ngoài, hành động thất bại và báo vùng không canh tác.

Mọi loại hạt khác giữ quy trình cuốc/trồng hiện hành, trừ Dưa leo/Thanh long đã có policy riêng.

## 9. Vật cản có thể dọn trong đất quanh nhà

Không sửa toàn bộ cây cảnh trên map thành cây chặt được. Chỉ những prop thuộc `Home Orchard Mask` và được gắn `ClearableOrchardProp` mới nhận hit.

| Loại vật cản | Số lần chặt | Ghi chú |
|---|---:|---|
| Hàng cỏ dài | 2 | Chỉ xóa cụm đã đánh dấu |
| Cỏ đơn | 1 | Xóa ngay sau hit hợp lệ |
| Bụi hoa | 2 | Không coi là crop |
| Cây gỗ to | 6 | Cùng sức bền yêu cầu của cây Xoài |
| Cây gỗ nhỏ | 4 | Prop môi trường riêng |
| Chuối dại | 3 | Không lớn, không ra trái, không thu hoạch |

`ClearableOrchardProp` chứa:

- `propId` ổn định: `HomeOrchard/{cellX}_{cellY}/{type}`;
- `propType`;
- `requiredHits` và `remainingHits`;
- danh sách cell footprint bị chiếm;
- collider/sorting references;
- hit feedback nhỏ.

Quy tắc:

- Chỉ rìu/rựa hợp lệ mới gây hit.
- Chỉ trừ độ bền khi hit trúng prop hợp lệ.
- Prop ngoài vùng mở khóa không phản ứng.
- Khi về 0 hit: tắt visual/collider, giải phóng cell, lưu cleared ID.
- Chuối dại không dùng `Crop`, không chạy growth timer và không sinh nông sản.
- Chưa có yêu cầu rơi vật phẩm, nên giai đoạn đầu dọn prop **không drop đồ**.

`Assets/Scripts/Action/Actions/ItemAction_Attack.cs` hiện chỉ cho rìu tác động cây trồng lâu năm; khi triển khai sẽ thêm nhánh rõ ràng cho `ClearableOrchardProp`, không nới quyền chặt mọi cảnh vật.

## 10. Thông báo vùng không hợp lệ

Tạo một `GameplayToastController` dùng chung vì thông báo giao dịch hiện chỉ nằm cục bộ trong `ShopWindowController`.

Thông báo tiếng Việt:

```text
Khu vực không được canh tác
```

Áp dụng khi:

- cuốc một ô ngoài mask được phép;
- gieo cây thường ngoài vùng;
- trồng Chuối/Xoài ngoài `Initial Farm Mask + Home Orchard Mask`;
- footprint cây 3x3 tràn ra ngoài vùng.

Toast là một dòng TMP nhỏ, fade sau khoảng 1,5 giây, không chặn gameplay. Thêm cooldown/de-duplicate khoảng 0,5 giây để giữ chuột không tạo hàng loạt thông báo.

## 11. Shop Chuối và Xoài

Yêu cầu mới thay thế việc mở Chuối/Xoài chỉ theo thứ tự crop cũ:

- Trước `homeOrchardUnlocked`: ẩn hoàn toàn Banana Seed và Mango Seed.
- Sau reveal hoàn tất: mở cả hai mặt hàng.
- Dưa leo và Thanh long vẫn mở theo trạng thái sửa từng khu như kế hoạch liên quan.
- Shop đang mở trong lúc debug thay đổi trạng thái phải rebuild catalog; trong luồng chơi thật, người chơi không thể đồng thời ở shop và xem reveal.
- Save/load không được làm hai hạt xuất hiện sớm do `highestUnlockedCropOrder` cũ.

Nên mở rộng điều kiện catalog thành tổ hợp policy thay vì chỉ một chỉ số thứ tự:

```text
requiredCropOrder
requiredSpecialArea
requiredFarmExpansion
```

## 12. Khóa input/UI trong chuỗi

Trong lúc dialogue hoặc reveal:

- không di chuyển;
- không dùng tool/trồng/chặt;
- không đổi quick slot bằng phím hoặc con lăn;
- không mở Bag/Shop;
- Esc ưu tiên Skip/đóng dialogue theo flow hiện tại, không mở Pause chồng lên;
- sau cleanup phải trả lại đúng trạng thái input trước đó kể cả khi scene unload.

Không dùng `Time.timeScale = 0` làm cơ chế duy nhất; camera tween và corner flash cần unscaled time.

## 13. Trình tự triển khai sau khi có tọa độ

1. Hoàn thiện và lưu trạng thái sửa Dưa leo → Thanh long theo kế hoạch khu giàn.
2. Thêm state `AwaitingGrandfather/Revealing/Unlocked` và migration save.
3. Tạo `Home Orchard Mask`, nhập bounds/các lỗ loại trừ từ tọa độ người dùng.
4. Thêm dialogue sequence của Ông Nội, dấu `!`, ưu tiên interaction và menu xem lại.
5. Tạo `HomeOrchardRevealController`, zoom override Pixel Perfect và prefab bốn góc.
6. Tạo `ClearableOrchardProp`, đánh dấu đúng cỏ/bụi/cây/chuối dại trong vùng.
7. Chuyển logic cuốc/gieo sang kiểm tra mask; thêm toast vùng không hợp lệ.
8. Đổi Chuối/Xoài sang trồng trực tiếp trên cỏ với footprint 3x3 trong vùng hợp lệ.
9. Gate Banana/Mango trong shop bằng `homeOrchardUnlocked`.
10. Bổ sung cheat/debug reset từng state, show mask, đánh dấu prop và replay reveal.
11. Compile, Play Mode test, save/load test và kiểm tra các tỷ lệ màn hình.

## 14. Checklist kiểm thử

### Tiến trình

- [ ] Không thể sửa Thanh long trước Dưa leo.
- [ ] Sửa Dưa leo chưa làm Ông Nội hiện `!`.
- [ ] Sửa Thanh long làm Ông Nội hiện `!` ngay hoặc khi quay lại scene.
- [ ] Save/load ở trạng thái chờ vẫn giữ dấu `!`.
- [ ] Nói chuyện xong mới chạy camera reveal.
- [ ] Reveal hoàn tất mới mở đất và Chuối/Xoài.

### Dialogue/camera

- [ ] Skip chữ vẫn chạy camera reveal.
- [ ] Camera thấy đủ bốn góc trên 16:9 và tỷ lệ cửa sổ khác.
- [ ] Bốn góc nhấp nháy khoảng 5 giây.
- [ ] Camera trở về player đúng vị trí/zoom và không giật.
- [ ] Xem lại chạy đủ thoại + reveal nhưng không đổi tiến trình lần nữa.
- [ ] Bag/shop/pause/con lăn/tool không hoạt động chồng trong chuỗi.

### Trồng trọt

- [ ] Chuối/Xoài bị ẩn khỏi shop trước khi mở đất.
- [ ] Sau mở khóa, Chuối/Xoài trồng được trực tiếp trên cỏ ở vườn ban đầu.
- [ ] Sau mở khóa, Chuối/Xoài trồng được trực tiếp trên cỏ ở đất quanh nhà.
- [ ] Không cần cuốc đất cho Chuối/Xoài.
- [ ] Footprint 3x3 không được tràn ranh giới.
- [ ] Cuốc/trồng ngoài mask không thay đổi world state và hiện đúng toast.
- [ ] Dưa leo/Thanh long vẫn chỉ trồng trong khu riêng.

### Dọn vật cản

- [ ] Cỏ đơn 1 hit; hàng cỏ 2; bụi hoa 2.
- [ ] Cây lớn 6; cây nhỏ 4; chuối dại 3.
- [ ] Hit hụt/prop không hợp lệ không trừ độ bền.
- [ ] Chuối dại không phát triển và không ra trái.
- [ ] Chỉ prop được đánh dấu trong khu này mới chặt được.
- [ ] Save/load giữ prop đã xóa và số hit còn lại.

## 15. Dữ liệu còn chờ người dùng

Trước khi triển khai map cần nhận:

1. Hai cell góc `MIN/MAX`, hoặc đủ bốn cell góc của phần đất trong khung đỏ.
2. Danh sách ô phải khoét khỏi mask nếu trong hình chữ nhật có nhà, đường, nước hoặc vật thể cố định.

Mặc định kế hoạch đang dùng ba quyết định có thể đổi bằng dữ liệu:

- Đất quanh nhà cho phép cả cây thường, không chỉ Chuối/Xoài.
- Dọn vật cản chưa rơi vật phẩm.
- Bốn góc dùng marker chữ L trắng đơn giản nếu chưa có sprite tileset cụ thể.

