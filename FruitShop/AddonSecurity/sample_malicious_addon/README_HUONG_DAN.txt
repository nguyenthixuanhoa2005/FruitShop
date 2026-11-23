HƯỚNG DẪN TEST CHỨC NĂNG QUÉT ADD-ON (BƯỚC 6 - CHƯƠNG 8)
======================================================
Thư mục này chứa 3 tệp mẫu cố tình chứa các chuỗi vi phạm luật bảo mật:
1. addon_widget.html : Chứa thẻ <script> và sự kiện onclick=
2. plugin_script.js  : Chứa eval(), atob() (giải mã base64) và gọi URL bên ngoài
3. Installer.cs      : Chứa Process.Start và cmd.exe

CÁCH TẠO TỆP ZIP ĐỂ TEST:
- Chọn cả 3 tệp trên (addon_widget.html, plugin_script.js, Installer.cs)
- Nhấp chuột phải -> Chọn "Compress to ZIP file" (hoặc Send to -> Compressed (zipped) folder)
- Đặt tên tệp là: test_malicious_addon.zip

VÀO WEB ĐỂ TEST:
- Mở trang: https://localhost:7082/AdminAddon
- Tải tệp test_malicious_addon.zip lên -> Bấm "Tải lên & Quét bảo mật"
- Màn hình sẽ hiển thị cảnh báo đỏ rực rỡ (Hình 8.5) liệt kê đầy đủ 6 phát hiện!
