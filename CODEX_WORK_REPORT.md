# Báo cáo các hạng mục Codex đã triển khai

Ngày cập nhật: 09/09/2026  
Dự án: `Meadom`  
Scene gameplay chính: `Assets/MainScenes/Core 1.unity`

> Tài liệu này tổng hợp các thay đổi Codex đã thực hiện trong chuỗi làm việc hiện tại. Một số asset hình ảnh và vị trí UI đã được người dùng tự chỉnh trong Unity; Codex giữ lại các chỉnh sửa đó khi cập nhật logic.

## 1. Inventory Bar và cửa sổ balo

- Rút thanh công cụ nhanh xuống còn 5 ô.
- Ghép giao diện Inventory Bar từ các sprite:
  - `_0`: viền trái.
  - `_1`: đoạn nối giữa các ô.
  - `_2`: viền phải.
  - `_3`: nền ô vật phẩm.
- Tạo giao diện balo mới từ nhóm asset `Assets/Sprites/bag-shop-ui/bag`.
- Tách khu vực Quick Slot và Bag Slot để chúng có thể bố trí độc lập.
- Quick Slot sử dụng sprite thường và sprite được chọn riêng.
- Bag Slot sử dụng sprite thường và sprite được chọn riêng.
- Bảng mô tả vật phẩm nằm bên phải cửa sổ balo.
- Đồng bộ vị trí `Text_Amount` và thanh độ bền giữa các ô dựa trên các ô mẫu người dùng đã căn.
- Cho phép kéo vật phẩm sang ô đang có đồ và hoán đổi hai vật phẩm.
- Sửa Inventory Bar không xuất hiện trong gameplay sau khi thay UI.
- Cửa sổ balo được đặt tắt mặc định trong Edit Mode và chỉ mở khi người chơi yêu cầu.
- Chặn mở balo trong lúc hội thoại đang diễn ra.

Các file chính:

- `Assets/Scripts/Item/Inventory/BagInventoryUIBuilder.cs`
- `Assets/Scripts/Item/Inventory/InventorySlot.cs`
- `Assets/Scripts/User Interface/BagWindow.cs`
- `Assets/Editor/SetupBagInventoryUI.cs`

## 2. Hiển thị vật phẩm trên đầu người chơi

- Thêm trạng thái cầm vật phẩm trên đầu cho vật phẩm thường.
- Không áp dụng kiểu cầm trên đầu cho các công cụ như cuốc, bình tưới và rìu/rựa.
- Chỉnh animation giữ vật phẩm dừng ở tư thế giữ đồ thay vì dừng ở frame nhấc chân.
- Chỉnh vật phẩm bám theo nhân vật, không nảy lên xuống khi người chơi di chuyển.
- Tăng kích thước icon vật phẩm đang cầm theo yêu cầu.

Các file chính:

- `Assets/Scripts/Entity Components/Character/FullBodyPlayerSpriteAnimator.cs`
- `Assets/Editor/SetupFullBodyPlayer.cs`
- `Assets/Prefabs/World/Player.prefab`

## 3. Hệ thống cây trồng theo thời gian thực

- Bỏ cách phát triển cây theo ngày.
- Chuyển cây sang phát triển bằng giây theo bảng thời gian đã cung cấp.
- Không bắt buộc phải tưới nước để cây chuyển giai đoạn.
- Giữ hình phạt thời gian khi thiếu điều kiện theo thiết kế hiện tại.
- Hỗ trợ cây thu hoạch một lần và cây có regrowth.
- Cà chua và dưa leo có giới hạn số lần regrowth theo thiết kế.
- Cây lâu năm dùng vùng chiếm chỗ 3x3 và mọc ở ô giữa.
- Hạt cây lâu năm không cần cuốc đất trước nhưng chỉ được trồng trên vùng đất cho phép canh tác.
- Sửa thứ tự sprite các giai đoạn của cây chuối.
- Giảm scale sprite cây trồng và sản phẩm sau thu hoạch theo yêu cầu.
- Thêm offset riêng theo từng giai đoạn thông qua `CropDefinition`.
- Neo đáy các giai đoạn cây lớn để hạn chế nhảy vị trí khi đổi sprite.
- Giai đoạn hạt vừa gieo được căn giữa ô đất thay vì bị đẩy lên trên.
- Hỗ trợ giữ chuột và đi qua nhiều ô để gieo hạt liên tục.
- Có cấu trúc bón phân và status cây trồng; phần luật bón phân đầy đủ được để lại để hoàn thiện sau.

Các file chính:

- `Assets/Scripts/World/Objects/Crop.cs`
- `Assets/Scripts/World/Objects/CropDefinition.cs`
- `Assets/Scripts/Item/Actions/ItemAction_PlantSeed.cs`
- `Assets/Scripts/Item/Actions/ItemAction_Fertilizer.cs`
- `Assets/Editor/SetupTimedCropSystem.cs`
- `Assets/Editor/CropDefinitionEditor.cs`

## 4. Status cây trồng

- Thêm sprite trạng thái từ `Assets/Sprites/stat-trạng thái cây trồng`.
- Giảm kích thước status xuống 50%.
- Điều chỉnh vị trí status gần cây hơn.
- Đặt alpha theo giá trị 75/255.
- Thêm tùy chọn bật/tắt status trong Farming Cheat Tool.

## 5. Farming Cheat Tool

Đã thêm các chức năng phục vụ kiểm thử và demo:

- Thêm hạt giống và vật phẩm thử nghiệm.
- Nạp đầy bình tưới hoặc bật nước vô hạn.
- Chuẩn bị/hoàn nguyên đất trồng.
- Cuốc một hàng và giữ nguyên một hàng để so sánh.
- Bón phân toàn bộ.
- Tưới toàn bộ.
- Phát triển toàn bộ cây ngay lập tức.
- Tua nhanh thời gian phát triển theo số giây.
- Thu hoạch toàn bộ cây đã chín.
- Bật/tắt status cây trồng.
- Trưng bày tất cả cây nông nghiệp theo từng hàng và từng giai đoạn.
- Trưng bày riêng các cây ăn quả/cây lâu năm theo từng giai đoạn.
- Giai đoạn cuối của cây trưng bày có thể thu hoạch để demo.

Các file chính:

- `Assets/Editor/FarmingCheatWindow.cs`
- `Assets/Scripts/World/FarmingCheats.cs`
- `Assets/Scripts/World/GridManager.cs`

## 6. Map và Grid Manager

- Chuẩn bị map mới dựa trên `Assets/Sprites/bố cục map.png`.
- Cắt sprite map thành tile và tạo Tile Palette/Map Layout Preview.
- Thêm các tile đất đã cuốc, đất đã tưới và hồ nước vào bộ tile.
- Chuyển `GridManager` và scene gameplay sang sử dụng bố cục map mới.
- Giữ cơ chế nhiều lớp tilemap: nền map, đất cuốc, đất ướt, nước, collision và các lớp trang trí.
- Vùng canh tác được xác định bằng dữ liệu tile/grid thay vì chỉ nhìn màu cỏ.

Các file/asset chính:

- `Assets/Scripts/World/GridManager.cs`
- `Assets/Tiles/Map Layout/Map Layout Preview.prefab`
- `Assets/Scenes/Levels/OutDoors/Level_Farm.unity`
- `Assets/Sprites/tileset/đất đã cuốc.png`
- `Assets/Sprites/tileset/đất tưới nước.png`
- `Assets/Sprites/tileset/hồ nước-Sheet.png`

## 7. NPC và hội thoại

- Thêm NPC người ông và chú Hải bên cạnh nhà để kiểm thử.
- Thêm collider và prefab để có thể tái sử dụng NPC.
- Thêm status ba chấm trên đầu NPC; giữ sẵn cấu trúc cho các trạng thái khác.
- Tạo hệ thống hội thoại tiếng Anh cho phần hướng dẫn ban đầu.
- Tách UI hội thoại thành hai GameObject:
  - `NPC Dialogue Group`.
  - `Player Dialogue Group`.
- Khi NPC nói chỉ hiện portrait NPC; khi người chơi nói chỉ hiện portrait người chơi.
- Thêm nút Skip và sửa sự kiện nhấn Skip.
- Khóa mở balo trong khi hội thoại.
- Thêm tiến trình yêu cầu người chơi cuốc ít nhất 3 ô trước khi quay lại nói chuyện với người ông.
- Thêm lựa chọn hội thoại lại và mở shop cho chú Hải.
- Sử dụng font TMP từ nhóm font của dự án cho nội dung hội thoại.
- Đặt Dialogue UI sẵn trong scene để có thể căn chỉnh ở Edit Mode, nhưng tắt mặc định khi chơi chưa bắt đầu hội thoại.
- Sửa `NullReferenceException` trong `DialogueUIController.Update()` khi shop mở nhưng trạng thái khóa hội thoại vẫn còn bật.

Các file chính:

- `Assets/Scripts/World/NPC/DialogueUIController.cs`
- `Assets/Scripts/World/NPC/NpcDialogueInteractor.cs`
- `Assets/Scripts/World/NPC/NpcDialogueStatus.cs`
- `Assets/Scripts/World/NPC/TutorialProgressService.cs`
- `Assets/Editor/SetupNpcAssets.cs`

## 8. Shop của chú Hải

- Tạo giao diện Shop dựa trên `Assets/Sprites/bag-shop-ui/shop`.
- Kích thước tổng thể được căn theo cửa sổ balo.
- Khu trên hiển thị danh sách sản phẩm bán trong shop.
- Khu dưới giữ nguyên 14 ô hiển thị inventory của người chơi.
- Dữ liệu 14 ô được lấy theo đúng thứ tự inventory chính:
  - Quick Slot 1–5 trước.
  - Sau đó là các Bag Slot.
- Khu bên phải hiển thị icon, tên, giá và số lượng giao dịch.
- Nút cộng/trừ và chuyển trang có sprite Normal/Pressed riêng.
- Nút BUY và SELL có sprite Normal/Pressed riêng.
- Khi chọn sản phẩm của shop: BUY bật, SELL xám và bị vô hiệu hóa.
- Khi chọn vật phẩm trong balo: SELL bật, BUY xám và bị vô hiệu hóa.
- Đã loại Watering Can khỏi danh mục shop.
- Giá mua/bán được khai báo theo bảng thiết kế đã cung cấp.
- Đơn vị tiền trong shop được đổi từ `G` thành `Đ`.
- Ba hàng sản phẩm dùng chung `Assets/Prefabs/Shop Item 2.prefab`.
- Khoảng cách giữa các hàng sản phẩm được giảm còn 25, bằng chiều cao prefab, để các hàng nằm sát nhau.
- Chỉ các `Text_Amount` trên ô balo trong shop dùng `Assets/Content/Fonts/Munro SDF.asset`.
- `Text_Quantity` trong khung cộng/trừ vẫn giữ font hiện tại.
- Có công cụ Editor đồng bộ lại Shop Item prefab mà không thay đổi vị trí các nút khác.

Các file chính:

- `Assets/Scripts/World/NPC/ShopWindowController.cs`
- `Assets/Editor/SetupShopUI.cs`
- `Assets/Editor/SyncShopItemRows.cs`
- `Assets/Prefabs/Shop Item 2.prefab`
- `Assets/Prefabs/User Interface/Core/Shop UI.prefab`

### Logic giao dịch shop

- Nút BUY/SELL có trạng thái chọn, khóa và hiển thị giá.
- BUY kiểm tra số tiền và chỗ trống trong inventory trước khi giao dịch.
- Mua thành công sẽ trừ `Gold`, thêm đúng số lượng vật phẩm và cập nhật ngay các slot.
- SELL chỉ bật với nông sản có giá trong bảng; bán thành công sẽ lấy đúng số lượng khỏi inventory và cộng `Gold`.
- Giao dịch được hoàn tác an toàn nếu bước thêm vật phẩm hoặc cộng tiền thất bại.
- `Gold` tiếp tục dùng item ẩn có sẵn của hệ thống nên được lưu/load chung với inventory.
- Shop báo `NOT ENOUGH MONEY`, `INVENTORY FULL` hoặc lỗi giao dịch ngay tại vùng giá khi không thể thực hiện.

## 9. Start Menu và Load Game

- Sửa New Game và Load Game để đi vào scene `Core 1`.
- Chuẩn bị giao diện danh sách save slot mới trong `StartMenu 1`.
- Sử dụng nền load game và nút thêm save mới từ `Assets/Sprites/settings-loadgame-ui`.

Các file chính:

- `Assets/MainScenes/StartMenu 1.unity`
- `Assets/Scripts/Main Menu/LoadGameScreenUI.cs`
- `Assets/Editor/SetupLoadGameScreenUI.cs`

## 10. Thời gian, ngày đêm và thời tiết

- Loại UI Energy Bar và Health Bar khỏi scene gameplay.
- Loại logic quái vật theo yêu cầu của dự án hiện tại.
- Loại hiệu ứng ngày/đêm và thời tiết khỏi gameplay/UI.
- Thay bằng đồng hồ đơn giản chỉ đếm giờ, phút và giây.

Các file chính:

- `Assets/Scripts/User Interface/SimpleGameClockUI.cs`
- `Assets/Scripts/System/Systems/TimeSystem.cs`
- `Assets/Editor/RemoveDayNightWeather.cs`
- `Assets/Prefabs/Systems/Time System.prefab`

## 11. Một số lỗi đã xử lý

- Công cụ tưới theo hướng quay nhân vật thay vì tâm chuột.
- Animation tưới bị phát hai lần cho một lần dùng.
- UI Edit Mode và Play Mode có vị trí khác nhau do script dựng lại layout lúc chạy.
- Inventory Bar biến mất sau khi thay UI.
- Vật phẩm trong balo biến mất do liên kết slot sai.
- Text số lượng bị lệch xuống dưới.
- Kéo đồ vào ô đã có vật phẩm không hoán đổi được.
- Portrait NPC/người chơi cùng xuất hiện trong một câu thoại.
- Nút Skip không hoạt động.
- Balo vẫn mở được trong khi hội thoại.
- `DialogueUIController.Update()` báo NullReference khi mở shop.
- Cây/hạt bị lệch vị trí khi đổi sprite hoặc vừa gieo xuống đất.

## 12. Trạng thái kiểm tra

- Các assembly runtime và Editor đã được build nhiều lần trong quá trình triển khai.
- Lần kiểm tra gần nhất: build thành công, không có compile error.
- Còn một số warning cũ liên quan đến hệ thống ngày và thuộc tính TextMeshPro đã obsolete; các warning này không chặn build.

## 13. Lưu ý khi tiếp tục chỉnh trong Unity

- Chỉnh mẫu hàng sản phẩm tại `Assets/Prefabs/Shop Item 2.prefab`.
- Công cụ đồng bộ sẽ tham chiếu lại ba hàng Shop Item từ prefab trên.
- Không dùng lệnh Rebuild toàn bộ Shop UI nếu chỉ muốn sửa vị trí một nút, vì Rebuild có thể tạo lại bố cục.
- Các thay đổi vị trí người dùng đã căn trực tiếp cần được Apply/Save trước khi thoát Unity.
- Sau khi thay script hoặc prefab, nên thoát Play Mode rồi chạy lại để các object runtime được tạo mới.
