# Báo cáo chỉnh sửa map và đối chiếu yêu cầu

Ngày thực hiện: 17/09/2026

## 1. Các thay đổi vừa triển khai

### Khôi phục bảng mở khóa

- Tài nguyên sử dụng: `Assets/Sprites/props-items/ban-khoa.png`.
- Bảng không bị xóa khỏi cấu hình, nhưng trước đó phần hình ảnh được hiển thị ở tỉ lệ `0.5`, khiến bảng rất nhỏ và nhìn giống như đã biến mất.
- Đã đổi tỉ lệ hình ảnh bảng thành `1.0` trong cả bản xem trước ở Edit Mode và lúc chạy game.
- Nếu cấu hình không lấy được sprite bảng, công cụ dựng map sẽ thử nạp trực tiếp sprite từ đường dẫn tài nguyên trên.
- Hai bảng sửa chữa vẫn được tạo tại các ô đã đánh dấu:
  - Khu dưa chuột: `[-13, 16]`.
  - Khu thanh long: `[32, -8]`.
- Hai bảng đều có `BoxCollider2D` dạng trigger để dùng cho tương tác mở khóa.

Các file liên quan:

- `Assets/Editor/BuildSpecialCropMapPreview.cs`
- `Assets/Scripts/World/SpecialCropRuntime.cs`
- `Assets/Resources/Farming/Special Crop Runtime Config.asset`
- `Assets/Scenes/Levels/OutDoors/Level_Farm.unity`

### Điều chỉnh cây chuối trang trí

- Đã loại bỏ hoàn toàn sprite chuối giai đoạn 4 khỏi bộ sinh cây trang trí.
- Cây chuối trang trí hiện chỉ chọn ngẫu nhiên giữa:
  - `bananatree_200_5`
  - `bananatree_200_6`
- Đây vẫn là cây trang trí tĩnh, không phát triển theo thời gian.
- Kết quả trong scene sau khi dựng lại:
  - Giai đoạn 4: `0` cây.
  - Giai đoạn 5: `9` cây.
  - Giai đoạn 6: `7` cây.

### Giảm mật độ vùng rừng

- Trước lần chỉnh này, vùng rừng dùng mẫu bàn cờ và chiếm khoảng 50% số ô.
- Đã giảm tiếp 50% so với mật độ đó bằng cách chỉ giữ các ô có cả tọa độ X và Y là số chẵn.
- Mật độ mới xấp xỉ 25% tổng số ô trong vùng rừng.
- Số cây/bụi được sinh trong vùng rừng giảm từ `682` xuống còn `360`.
- Cây vẫn được phân bố xuyên suốt toàn bộ vùng đã chọn, không chỉ tập trung ở phía trái.

## 2. Trạng thái áp dụng vào Unity

- Scene `Assets/Scenes/Levels/OutDoors/Level_Farm.unity` đã được dựng lại và lưu.
- Hai đối tượng `Repair Lock` và sprite `ban-khoa` đều còn trong scene.
- Hình ảnh bảng trong scene có scale `(1, 1, 1)`.
- Chuối giai đoạn 4 không còn trong scene.
- Dự án đã được kiểm tra bằng `dotnet build Assembly-CSharp-Editor.csproj --no-restore`.
- Kết quả: build thành công, không có lỗi biên dịch. Các cảnh báo còn lại là cảnh báo cũ, không phát sinh từ các thay đổi trên.

## 3. Đối chiếu `Cơ chế (1).docx`

Yêu cầu chính trong tài liệu:

- Có màn hình nhập tên người chơi theo mẫu `Welcome to Meadow!`.
- Có nội dung hỏi tên và ô nhập tên.
- Dùng font `dearpix-1.94 Ygygfu.ttf`.

Trạng thái hiện tại: **mới hoàn thành một phần**.

Đã có:

- Hai trường nhập `Character Name` và `Farm Name` trong scene Start Menu.
- `SaveData` có trường `playerName`.
- Hệ thống Save Slot có phần hiển thị tên người chơi đã lưu.

Chưa hoàn thành hoặc chưa đúng mẫu:

- Chưa có đầy đủ bố cục `Welcome to Meadow! / What's your name? / Enter Name / OK` giống hình mẫu.
- Font `dearpix-1.94 Ygygfu` chưa được gắn cho phần nhập tên trong `StartMenu 1.unity`.
- Nội dung hiện tại vẫn là `Character Name` và `Farm Name`, chưa phải nội dung trong mẫu.

## 4. Đối chiếu `lưu ý (1).docx`

Yêu cầu chính trong tài liệu:

- Map cần có mật độ cây và cỏ gần giống ảnh mẫu.
- Khu vực đất giữa hai căn nhà cần được bố trí gần giống mẫu.
- Dùng bốn mẫu cây, chia thành hai cây nhỏ và hai cây lớn, rồi luân phiên để tránh lặp.
- Các vùng được đánh dấu đỏ và xanh cần được rải cây tương ứng với vùng mẫu bên trái.
- Vùng đỏ gần chân núi phải có mật độ cây dày hơn vùng xanh.

Trạng thái hiện tại: **mới hoàn thành một phần**.

Đã có:

- Công cụ đọc ảnh tham chiếu và dựng các prop trang trí thành GameObject trong map.
- Đã có các sprite cây nhỏ, cây vừa, cây lớn, cỏ và bụi cây riêng biệt.
- Có xử lý tránh đặt cây/cỏ lên các ô đường đi.
- Có vùng rừng riêng và hệ thống sinh cây/bụi xuyên suốt vùng được đánh dấu.

Chưa hoàn thành hoặc chưa có cơ chế rõ ràng:

- Chưa có hai loại vùng logic riêng biệt cho vùng đỏ và vùng xanh.
- Chưa có quy tắc bắt buộc vùng đỏ luôn dày hơn vùng xanh.
- Chưa ép đúng cơ chế luân phiên hai cây nhỏ và hai cây lớn; vùng rừng hiện vẫn chọn ngẫu nhiên từ danh sách cây và bụi.
- Chưa có bước kiểm tra tự động để bảo đảm khu vực giữa hai căn nhà giống mật độ trong ảnh mẫu.
- Mật độ vùng rừng vừa được giảm xuống khoảng 25% theo yêu cầu mới nhất, vì vậy chưa đáp ứng yêu cầu vùng đỏ phải rậm rạp trong tài liệu.

## 5. Kết luận

- Yêu cầu khôi phục bảng khóa, bỏ chuối giai đoạn 4 và giảm tiếp 50% mật độ rừng đã được triển khai và áp vào scene.
- Hai tài liệu Word chưa được thực hiện toàn bộ.
- Phần còn thiếu lớn nhất là giao diện nhập tên đúng mẫu và hệ thống phân biệt vùng đỏ/xanh với mật độ cây riêng.
