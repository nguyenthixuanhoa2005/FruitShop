# 📌 KẾ HOẠCH TRIỂN KHAI — Dự Án FruitShop

## Tổng quan
- **Công nghệ:** ASP.NET MVC 8 (.NET 8), SQL Server, Entity Framework Core
- **Bổ sung:** Cloudinary (ảnh), MeiliSearch (tìm kiếm), ClosedXML (Excel), BCrypt (mã hóa)
- **Môi trường:** Docker (docker-compose), HTTPS local

---

## GIAI ĐOẠN 1 — Bảo mật Xác thực (Ưu tiên CỰC CAO)
**Mục tiêu:** Khắc phục các lỗ hổng bảo mật Authentication theo OWASP A2, A3

### Bước 1.1 — Hash mật khẩu (BCrypt)
- Thư viện `BCrypt.Net-Next` đã có trong dự án (csproj)
- **Việc cần làm:**
  - `HomeController.cs` → Login: thay `user.PasswordHash != model.Password` bằng `BCrypt.Verify()`
  - `HomeController.cs` → Register: thay `PasswordHash = model.Password` bằng `BCrypt.HashPassword()`
  - `AccountController.cs` → ChangePassword: dùng `BCrypt.Verify()` + `BCrypt.HashPassword()`
  - `HomeController.cs` → ResetPassword: dùng `BCrypt.HashPassword()`
- **Lưu ý:** Cần migrate dữ liệu users cũ nếu đã có plain-text trong DB

### Bước 1.2 — Xóa TODO bảo mật bỏ ngỏ
- Xóa comment `// TODO: thực hiện xác thực mật khẩu đúng cách (KHÔNG AN TOÀN, chỉ để demo)` sau khi sửa

---

## GIAI ĐOẠN 2 — Phân quyền Admin (Ưu tiên CAO)
**Mục tiêu:** Ngăn user thường truy cập vào các route Admin

### Bước 2.1 — Tạo ActionFilter kiểm tra Role
```csharp
// Tạo file Filters/AdminOnlyFilter.cs
public class AdminOnlyFilter : ActionFilterAttribute {
    public override void OnActionExecuting(ActionExecutingContext context) {
        var role = context.HttpContext.Session.GetString("UserRole");
        if (role != "1") {
            context.Result = new RedirectToActionResult("Login", "Home", null);
        }
    }
}
```

### Bước 2.2 — Áp dụng filter cho các Controller Admin
- `DashboardController` — thêm `[AdminOnly]`
- `AdminProductsController` — thêm `[AdminOnly]`
- `AdminOrdersController` — thêm `[AdminOnly]`
- `CategoriesController` — thêm `[AdminOnly]`
- `SuppliersController` — thêm `[AdminOnly]`
- `InventoryController` — thêm `[AdminOnly]`
- `StatisticsController` — thêm `[AdminOnly]`
- `UsersController` — thêm `[AdminOnly]`
- `CouponsController` — thêm `[AdminOnly]`

---

## GIAI ĐOẠN 3 — CSRF & Form Security (Ưu tiên TRUNG BÌNH)
**Mục tiêu:** Đảm bảo tất cả form POST đều được bảo vệ

### Bước 3.1 — Kiểm tra các form chưa có token
- Rà soát tất cả View có form POST
- Đặc biệt: `Views/Account/`, `Views/Home/` (Login, Register)
- Thêm `@Html.AntiForgeryToken()` và `[ValidateAntiForgeryToken]` nếu còn thiếu

### Bước 3.2 — AJAX với token
- Xác nhận các AJAX call quan trọng (CartController, AdminProducts) đã gửi header `RequestVerificationToken`

---

## GIAI ĐOẠN 4 — Secrets & Config Management (Ưu tiên TRUNG BÌNH)
**Mục tiêu:** Không để lộ API keys trong git commit

### Bước 4.1 — Dùng User Secrets / Environment Variables
- Cloudinary ApiKey, ApiSecret → chuyển sang `dotnet user-secrets` hoặc ENV variable
- MeiliSearch ApiKey → ENV variable
- EmailSettings Password → ENV variable
- Connection String → ENV variable
- `.gitignore` đã có `appsettings*.json` ngoại trừ file mẫu

---

## GIAI ĐOẠN 5 — Upload File & Validation (Ưu tiên TRUNG BÌNH)
**Mục tiêu:** Validate loại file upload

### Bước 5.1 — Kiểm tra MIME type và extension
- `AdminProductsController.UploadToCloudinary()`: thêm kiểm tra file extension
- Chỉ cho phép: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`
- Kiểm tra ContentType bắt đầu bằng `image/`

---

## GIAI ĐOẠN 6 — Polish & Chức năng còn thiếu
**Mục tiêu:** Hoàn thiện các tính năng nghiệp vụ

### Checklist nghiệp vụ cần hoàn thiện:
- [ ] Thanh toán online (VNPay / COD) — kiểm tra flow Payment
- [ ] Email xác nhận đơn hàng
- [ ] Phân trang trang chủ (nếu sản phẩm nhiều)
- [ ] SEO meta tags
- [ ] Responsive mobile

---

## Timeline Gợi ý

| Tuần | Việc cần làm |
|------|-------------|
| Tuần 1 | Giai đoạn 1 + 2 (Bảo mật xác thực + phân quyền) |
| Tuần 2 | Giai đoạn 3 + 4 (CSRF + Config) |
| Tuần 2-3 | Giai đoạn 5 + 6 (Upload + polish) |
| Tuần 3 | Chạy scan lại theo checkpoint, fix còn lại |

---

## Tài liệu tham khảo
- OWASP Top 10: https://owasp.org/www-project-top-ten/
- BCrypt .NET: https://github.com/BcryptNet/bcrypt.net
- ASP.NET Anti-Forgery: https://docs.microsoft.com/en-us/aspnet/core/security/anti-request-forgery
- Cloudinary .NET SDK: https://cloudinary.com/documentation/dotnet_integration
