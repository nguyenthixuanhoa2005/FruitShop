# Project Mandates

1. **Feature Preservation:** Khi sửa lỗi hoặc cập nhật tính năng, tuyệt đối không được xóa hoặc làm mất các tính năng đã có sẵn trong hệ thống. Mọi thay đổi phải đảm bảo tính kế thừa và không gây ra hồi quy (regression) đối với các chức năng hiện tại.
2. **Logic Integrity:** Khi thực hiện rút gọn code (refactoring), tuyệt đối không được làm mất hoặc rút gọn các logic chức năng đang hoạt động khác. Code mới phải giữ nguyên hành vi của hệ thống cũ nhưng ở dạng tối ưu hơn.
<!-- 3. **Product Management Freeze:** Các màn hình liên quan đến quản lý sản phẩm hiện đã hoàn tất. Tuyệt đối không được tự ý xóa, thay đổi cấu trúc hoặc chỉnh sửa logic của chúng trừ khi có yêu cầu trực tiếp. -->

## Safe Editing Rules

3. **Scope Control:** Chỉ chỉnh đúng phần được yêu cầu. Nếu cần thay đổi lan sang file, component, route, service, API, style, hoặc validation khác thì phải dừng lại để nêu rõ lý do trước khi sửa.
4. **No Silent Deletion:** Không tự ý xóa feature, component, function, prop, endpoint, route, handler, test, hoặc logic mà chưa được yêu cầu trực tiếp.
5. **Behavior First:** Ưu tiên giữ nguyên hành vi hiện tại. Nếu có cách làm sạch code nhưng có nguy cơ đổi hành vi, phải chọn phương án an toàn hơn.
6. **Compatibility Check:** Mọi thay đổi phải kiểm tra tương thích với luồng hiện có, dữ liệu hiện có, và các màn hình/chức năng liên quan để tránh hỏng phần khác.
7. **Minimal Diff:** Chỉ thay đổi tối thiểu cần thiết để đạt mục tiêu. Tránh refactor lớn, đổi tên hàng loạt, hoặc dọn code không liên quan trong cùng một lần sửa.
8. **Explain Before Breaking Change:** Nếu buộc phải xóa hoặc thay đổi đáng kể một phần nào đó, phải mô tả rõ tác động, lý do, và phần sẽ bị ảnh hưởng trước khi thực hiện.

## Completion Checklist

Trước khi kết thúc, luôn tự kiểm tra:

- Có vô tình xóa tính năng nào không.
- Có làm thay đổi hành vi của phần khác không.
- Có file/logic nào bị sửa ngoài phạm vi yêu cầu không.
- Có cần thêm test hoặc cập nhật mô tả để bảo vệ thay đổi không.
