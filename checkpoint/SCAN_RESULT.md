# 🔍 KẾT QUẢ SCAN — FruitShop Web Project
**Ngày scan:** 17/09/2026  
**Người scan:** Antigravity AI  
**Phiên bản:** ASP.NET MVC 8 (.NET 8)

---

## 📊 TỔNG KẾT NHANH

| Nhóm | Tổng CP | ✅ Đạt | ❌ Chưa đạt | ⚠️ Một phần |
|------|---------|--------|------------|------------|
| A — Authentication & Session | 9 | 5 | 2 | 2 |
| B — Authorization / Access Control | 6 | 2 | 4 | 0 |
| C — SQL Injection & Validation | 4 | 3 | 0 | 1 |
| D — XSS Protection | 3 | 2 | 0 | 1 |
| E — CSRF Protection | 4 | 3 | 0 | 1 |
| F — HTTPS & Transport Security | 4 | 2 | 0 | 2 |
| G — Upload File | 4 | 2 | 1 | 1 |
| H — Tính năng nghiệp vụ | 19 | 16 | 1 | 2 |
| I — Cấu hình & Code Quality | 5 | 2 | 1 | 2 |
| **TỔNG CỘNG** | **62** | **37** | **9** | **10** |

---

## 🔐 NHÓM A — Bảo mật Xác thực & Session

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| A1 | Mật khẩu được hash (BCrypt) trước khi lưu DB | ❌ **CHƯA ĐẠT** | `HomeController.cs:195` — `PasswordHash = model.Password` lưu plain-text. Thư viện BCrypt.Net-Next đã cài nhưng CHƯA được sử dụng. |
| A2 | Login không so sánh plain-text | ❌ **CHƯA ĐẠT** | `HomeController.cs:118` — `user.PasswordHash != model.Password` so sánh trực tiếp. Comment TODO vẫn còn: "KHÔNG AN TOÀN, chỉ để demo". |
| A3 | Session cookie HttpOnly = true | ✅ **ĐẠT** | `Program.cs:20` — `options.Cookie.HttpOnly = true` đã cấu hình. |
| A4 | Session cookie IsEssential = true | ✅ **ĐẠT** | `Program.cs:21` — `options.Cookie.IsEssential = true` đã cấu hình. |
| A5 | Session timeout hợp lý (≤ 60 phút) | ✅ **ĐẠT** | `Program.cs:19` — `IdleTimeout = TimeSpan.FromMinutes(30)` (30 phút). |
| A6 | Logout xóa toàn bộ session | ✅ **ĐẠT** | `HomeController.cs:368` — `HttpContext.Session.Clear()`. `AdminHomeController.cs:28` — tương tự. |
| A7 | Forgot Password gửi OTP qua Email | ✅ **ĐẠT** | `HomeController.cs:221-288` — Gửi OTP 6 số qua SMTP Gmail. OTP lưu trong DB, không lộ trên URL. |
| A8 | OTP có thời hạn hiệu lực và bị xóa sau dùng | ⚠️ **MỘT PHẦN** | OTP có expiry 5 phút (A8 đạt). Sau reset password, `ResetToken = null` (A9 đạt). Tuy nhiên: nếu OTP sai nhiều lần liên tiếp không có rate-limit. |
| A9 | Reset token bị xóa sau reset thành công | ✅ **ĐẠT** | `HomeController.cs:350-351` — `user.ResetToken = null; user.ResetTokenExpiry = null` sau khi đặt lại mật khẩu. |

**🚨 Vấn đề nghiêm trọng nhóm A:** A1 và A2 — Mật khẩu plain-text là lỗ hổng bảo mật NGHIÊM TRỌNG nhất của dự án. BCrypt đã được cài sẵn, cần áp dụng ngay.

---

## 🛡️ NHÓM B — Phân quyền & Kiểm soát truy cập

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| B1 | Admin routes được bảo vệ | ❌ **CHƯA ĐẠT** | Các Controller Admin (Dashboard, AdminProducts, AdminOrders...) KHÔNG có bất kỳ middleware hay filter nào kiểm tra role. Bất kỳ ai biết URL đều có thể truy cập. |
| B2 | Có middleware/filter kiểm tra role Admin | ❌ **CHƯA ĐẠT** | Không có ActionFilter, không có `[Authorize]`, không có role-check nào trong Controller Admin. `AdminHomeController.cs` chỉ redirect sang Dashboard mà không kiểm tra. |
| B3 | User chỉ xem đơn hàng của chính mình | ✅ **ĐẠT** | `OrdersController.cs:29` — `Where(o => o.UserId == userId)`. `OrdersController.cs:52` — `o.UserId == userId`. |
| B4 | User chỉ sửa profile của chính mình | ✅ **ĐẠT** | `AccountController.cs:48-52` — lấy userId từ session, chỉ sửa đúng user đó. |
| B5 | Dashboard chỉ dành cho Admin | ❌ **CHƯA ĐẠT** | `DashboardController.cs` không có bất kỳ role check nào. |
| B6 | Kiểm tra session hợp lệ trước action nhạy cảm | ❌ **CHƯA ĐẠT** | Admin Controllers như `DashboardController`, `AdminProductsController` không check session UserId hay UserRole trước khi thực thi. |

**🚨 Vấn đề nghiêm trọng nhóm B:** Toàn bộ khu vực Admin chỉ bảo vệ bằng việc không hiển thị link trên UI — không có server-side authorization thực sự. Đây là lỗ hổng **Broken Access Control** (OWASP A01:2021).

---

## 🧪 NHÓM C — Chống SQL Injection & Input Validation

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| C1 | Dùng Entity Framework (không raw SQL concat) | ✅ **ĐẠT** | Toàn bộ code dùng EF Core LINQ queries. Không phát hiện raw SQL string concatenation. |
| C2 | Không có SQL thủ công nối chuỗi từ user input | ✅ **ĐẠT** | Scan qua tất cả Controller không thấy `ExecuteSqlRaw` hay `FromSqlRaw` với string concat. |
| C3 | Validate input server-side (ModelState.IsValid) | ⚠️ **MỘT PHẦN** | `HomeController.cs:102` Register có check ModelState. Login có check. Nhưng nhiều action trong AdminProducts nhận data mà không check ModelState. |
| C4 | Không lộ lỗi DB chi tiết | ✅ **ĐẠT** | `HomeController.cs:207-213` catch exception, trả về message tóm tắt, không lộ stack trace. `Program.cs:49` dùng `/Home/Error` ở production. |

---

## 🌐 NHÓM D — Chống XSS

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| D1 | Views dùng Razor @Model.Field (auto-encode) | ✅ **ĐẠT** | Razor Views tự động HTML-encode khi dùng `@`. Không phát hiện `@Html.Raw()` với dữ liệu người dùng. |
| D2 | Không inject user content vào JS/HTML trực tiếp | ✅ **ĐẠT** | Dữ liệu truyền vào JS qua ViewBag đều là primitive types (id, số lượng). |
| D3 | Error/success message qua Razor (encoded) | ⚠️ **MỘT PHẦN** | TempData và ViewBag messages hiển thị qua Razor (encoded). Một số ViewBag.Error được render bằng Razor nên an toàn. Cần kiểm tra thêm các view có dynamic content phức tạp. |

---

## 🔄 NHÓM E — Chống CSRF

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| E1 | Form POST có @Html.AntiForgeryToken() | ✅ **ĐẠT** | Tìm thấy AntiForgeryToken ở: Suppliers, Users, Reviews, Inventory, Categories, AdminProducts, AdminOrders, Coupons. |
| E2 | Action POST có [ValidateAntiForgeryToken] | ✅ **ĐẠT** | Tìm thấy ở: AdminProductsController (5 action), CouponsController (5 action), CategoriesController, InventoryController, ReviewsController, UsersController, SuppliersController. |
| E3 | AJAX POST gửi RequestVerificationToken header | ⚠️ **MỘT PHẦN** | Cấu hình header name `RequestVerificationToken` trong `Program.cs:33` đã có. Cần verify từng AJAX call trong JS files gửi đúng header. |
| E4 | Antiforgery header name cấu hình trong Program.cs | ✅ **ĐẠT** | `Program.cs:31-34` — `options.HeaderName = "RequestVerificationToken"`. |

**⚠️ Lưu ý:** Form Login và Register trong `Views/Home/` cần kiểm tra thêm xem có AntiForgeryToken không — không thấy trong kết quả grep hiện tại.

---

## 🌍 NHÓM F — HTTPS & Transport Security

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| F1 | app.UseHttpsRedirection() | ✅ **ĐẠT** | `Program.cs:53` — `app.UseHttpsRedirection()` đã có. |
| F2 | HSTS ở Production | ✅ **ĐẠT** | `Program.cs:50` — `app.UseHsts()` trong block `else` (production). |
| F3 | Connection string không có plain-text password | ⚠️ **MỘT PHẦN** | `appsettings.json:10` — dùng Windows Authentication (Trusted_Connection=True) nên không có password. Tuy nhiên appsettings.json có thể bị commit lên git. |
| F4 | API keys không hardcode trong appsettings | ⚠️ **MỘT PHẦN** | `appsettings.json` chứa: Cloudinary ApiKey/ApiSecret, MeiliSearch ApiKey, Email password — tất cả plain-text. Cần chuyển sang User Secrets hoặc ENV variables. |

---

## 🖼️ NHÓM G — Upload File

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| G1 | Upload chỉ cho phép định dạng ảnh | ❌ **CHƯA ĐẠT** | `AdminProductsController.cs:37-59` — `UploadToCloudinary()` không kiểm tra file extension hay MIME type trước khi upload. Cloudinary có thể nhận bất kỳ file nào. |
| G2 | File lưu qua Cloudinary (không lưu local) | ✅ **ĐẠT** | `AdminProductsController.cs:51` — dùng `_cloudinary.UploadAsync()`. Không lưu file local. |
| G3 | Transformation ảnh được áp dụng | ✅ **ĐẠT** | `AdminProductsController.cs:49` — `Quality("auto").FetchFormat("auto")` — tự động nén và chọn format tốt nhất (WebP/AVIF). |
| G4 | Không lộ đường dẫn file nội bộ | ⚠️ **MỘT PHẦN** | URL ảnh là Cloudinary CDN URL — không lộ đường dẫn server nội bộ. Tuy nhiên folder structure Cloudinary (`FruitShop/Products`) có thể thấy trong URL. |

---

## 🧩 NHÓM H — Tính năng Nghiệp vụ Cốt lõi

| ID | Checkpoint | Kết quả | Ghi chú |
|----|-----------|---------|---------|
| H1 | Trang chủ — sản phẩm bán chạy | ✅ ĐẠT | HomeController tính theo OrderItems, fallback sang sản phẩm mới nhất. |
| H2 | Đăng ký tài khoản | ✅ ĐẠT | HomeController.Register() hoạt động, check email trùng. |
| H3 | Đăng nhập / Đăng xuất | ✅ ĐẠT | Login lưu session, Logout clear session. Redirect theo role. |
| H4 | Quên mật khẩu / OTP / Reset | ✅ ĐẠT | Flow đầy đủ: ForgotPassword → VerifyOTP → ResetPassword. |
| H5 | Giỏ hàng: thêm/sửa/xóa | ✅ ĐẠT | CartController.cs 901 dòng — đầy đủ tính năng. |
| H6 | Thanh toán / Đặt hàng | ⚠️ MỘT PHẦN | CartController có checkout nhưng cần kiểm tra Payment flow (COD/online). |
| H7 | Xem lịch sử đơn hàng | ✅ ĐẠT | OrdersController.Index() + Details(). |
| H8 | Hủy đơn hàng | ✅ ĐẠT | OrdersController.Cancel(). |
| H9 | Đánh giá sản phẩm | ✅ ĐẠT | ReviewsController, link từ order details. |
| H10 | Tìm kiếm MeiliSearch | ✅ ĐẠT | MeiliSearchService + MeiliSearchSyncService. Tìm kiếm users, products. |
| H11 | Mã giảm giá (Coupon) | ✅ ĐẠT | CouponsController 225 dòng + CartController áp dụng coupon. |
| H12 | Admin: CRUD Sản phẩm + Cloudinary | ✅ ĐẠT | AdminProductsController 1049 dòng — đầy đủ. |
| H13 | Admin: Quản lý đơn hàng | ✅ ĐẠT | AdminOrdersController 9508 bytes. |
| H14 | Admin: Dashboard thống kê | ✅ ĐẠT | DashboardController — revenue, orders, top products, low stock. |
| H15 | Admin: Quản lý kho | ✅ ĐẠT | InventoryController 14531 bytes. |
| H16 | Admin: Xuất Excel | ✅ ĐẠT | ClosedXML được dùng trong UsersController, AdminProductsController, CouponsController. |
| H17 | Admin: Danh mục phân cấp | ✅ ĐẠT | CategoriesController + HomeController xử lý ParentId. |
| H18 | Admin: Nhà cung cấp | ✅ ĐẠT | SuppliersController 17482 bytes. |
| H19 | Admin: Quản lý người dùng | ❌ CHƯA RÕ | UsersController có, nhưng cần xác minh view chỉnh sửa role user. |

---

## ⚙️ NHÓM I — Cấu hình & Chất lượng Code

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| I1 | Không có TODO bảo mật nghiêm trọng bỏ ngỏ | ❌ **CHƯA ĐẠT** | `HomeController.cs:116-117`: `// TODO: thực hiện xác thực mật khẩu đúng cách (KHÔNG AN TOÀN, chỉ để demo)`. `HomeController.cs:195`: `// TODO: Hash password properly`. `AccountController.cs:97`: tương tự. |
| I2 | Error không lộ stack trace ở production | ✅ **ĐẠT** | `Program.cs:49` — `/Home/Error` ở production. ErrorViewModel chỉ trả về RequestId. |
| I3 | Swagger chỉ bật ở Development | ✅ **ĐẠT** | `Program.cs:42-46` — Swagger chỉ trong `if (app.Environment.IsDevelopment())`. |
| I4 | Docker / docker-compose | ⚠️ **MỘT PHẦN** | `docker-compose.yml` có ở root. Cần kiểm tra nội dung đầy đủ và Dockerfile. |
| I5 | MeiliSearch đồng bộ dữ liệu | ⚠️ **MỘT PHẦN** | Service đã có. Dashboard có nút SyncAll. Nhưng sync tự động khi thêm/sửa/xóa cần verify. |

---

## 🔴 DANH SÁCH LỖI CẦN SỬA NGAY (CRITICAL)

1. **[A1+A2] Mật khẩu plain-text** — `HomeController.cs:118,195`, `AccountController.cs:85,97`
   - Cần dùng `BCrypt.Verify()` và `BCrypt.HashPassword()` (thư viện đã cài sẵn)

2. **[B1+B2+B5+B6] Không có phân quyền Admin** — Tất cả Controller Admin
   - Tạo `AdminOnlyFilter` và áp dụng cho toàn bộ admin routes

3. **[G1] Không validate file upload** — `AdminProductsController.cs:37`
   - Thêm kiểm tra MIME type và extension trước khi upload Cloudinary

4. **[I1] TODO bảo mật còn bỏ ngỏ** — HomeController, AccountController
   - Khắc phục sau khi làm xong A1+A2

---

## 🟡 DANH SÁCH CẦN CẢI THIỆN (MEDIUM)

5. **[F4] API keys trong appsettings.json** — Cloudinary, MeiliSearch, Email password
   - Chuyển sang `dotnet user-secrets` hoặc Environment Variables

6. **[A8] Rate-limit cho OTP** — Chưa có giới hạn số lần nhập sai OTP

7. **[E3] Xác minh AJAX CSRF token** — Cần kiểm tra từng file JS

8. **[C3] ModelState validation đầy đủ** — Một số Admin actions chưa check

---

## ✅ NHỮNG GÌ ĐÃ LÀM TỐT

- ✅ Session configuration đúng chuẩn (HttpOnly, timeout, IsEssential)
- ✅ Logout clear session đúng cách
- ✅ OTP flow hoàn chỉnh (gửi mail, expiry, xóa sau dùng)
- ✅ HTTPS + HSTS đã cấu hình
- ✅ EF Core ngăn SQL Injection
- ✅ CSRF protection trên nhiều admin forms
- ✅ Cloudinary upload + tối ưu ảnh tự động
- ✅ MeiliSearch full-text search
- ✅ Swagger chỉ ở Development
- ✅ Error handling không lộ stack trace
- ✅ User chỉ xem được đơn hàng/profile của chính mình
- ✅ Tính năng nghiệp vụ phong phú (16/19 checkpoint đạt)
- ✅ Excel export, Coupon, Inventory, Dashboard đầy đủ

---

*Scan thực hiện bởi: đọc source code tĩnh (static analysis). Cần test động (black-box) để xác nhận hoàn toàn.*

---

## 🔥 NHÓM J — WAF (Tường lửa ứng dụng web)
> Từ slide: "Tường lửa ứng dụng web (WAF)" — Triển khai: Trung bình, 2-6 tuần

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| J1 | Chặn request độc hại (SQL Injection, XSS patterns) | ❌ **CHƯA ĐẠT** | Không có middleware WAF hay filter nào kiểm tra pattern độc hại ở tầng ứng dụng. Chỉ dựa vào EF Core ngăn SQL và Razor encode XSS. Chưa có WAF thực sự. |
| J2 | Chặn IP đáng ngờ / truy cập trái phép | ❌ **CHƯA ĐẠT** | Không có cơ chế blacklist IP, không có filter kiểm soát IP trong codebase. |
| J3 | Rate limiting / chống brute-force | ❌ **CHƯA ĐẠT** | Login, Register, OTP endpoints không có rate limiting. Kẻ tấn công có thể thử mật khẩu / OTP vô giới hạn lần. |
| J4 | WAF cloud (Cloudflare, v.v.) | ⚠️ **CHƯA RÕ** | Dự án dùng Cloudinary (CDN ảnh) nhưng chưa có cấu hình WAF cloud (Cloudflare) cho toàn site. |
| J5 | Log request bất thường | ❌ **CHƯA ĐẠT** | Logging chỉ ở mức mặc định ASP.NET. Không có middleware log request đáng ngờ hay alert. |

**🔴 Đánh giá nhóm J:** 0/5 đạt. Đây là nhóm yếu nhất — toàn bộ WAF chưa được triển khai.

---

## 🔒 NHÓM K — HTTPS & Mã hóa dữ liệu
> Từ slide: "HTTPS: Mã hóa dữ liệu" — Ưu tiên: CAO, Tính khả thi: Dễ, Timeline: 1-3 ngày

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| K1 | SSL/TLS hợp lệ | ✅ **ĐẠT** | Dự án dùng HTTPS local (dev certificate). Production cần Let's Encrypt hoặc cert thực. |
| K2 | HTTP tự redirect sang HTTPS | ✅ **ĐẠT** | `Program.cs:53` — `app.UseHttpsRedirection()` đã cấu hình. |
| K3 | Không có mixed content | ⚠️ **MỘT PHẦN** | Ảnh lưu trên Cloudinary CDN (HTTPS). Nhưng cần kiểm tra các link CSS/JS external trong Views có dùng HTTP không. |
| K4 | HSTS được thiết lập | ✅ **ĐẠT** | `Program.cs:50` — `app.UseHsts()` trong production. |
| K5 | Cookie Secure flag (chỉ qua HTTPS) | ❌ **CHƯA ĐẠT** | `Program.cs:17-22` — Session cookie cấu hình `HttpOnly=true`, `IsEssential=true` nhưng **không có** `options.Cookie.SecurePolicy = CookieSecurePolicy.Always`. |
| K6 | Không lưu dữ liệu nhạy cảm plain-text trong cookie/localStorage | ✅ **ĐẠT** | Session lưu UserId, UserName, UserRole (không nhạy cảm). Password không lưu vào session hay cookie. |

**🟡 Đánh giá nhóm K:** 4/6 đạt. Thiếu Secure flag cho cookie (K5).

---

## 🔄 NHÓM L — Cập nhật ứng dụng & Dependencies
> Từ slide: "Cập nhật ứng dụng & plugin" — Mức độ CƠ BẢN, Ưu tiên CAO

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| L1 | NuGet packages không có CVE đã biết | ⚠️ **MỘT PHẦN** | Các package trong csproj: EF Core 8.0.0, BCrypt.Net-Next 4.1.0, CloudinaryDotNet 1.26.2, MeiliSearch 0.18.0, ClosedXML 0.102.2 — cần chạy `dotnet list package --vulnerable` để xác nhận. |
| L2 | ASP.NET Core .NET 8 LTS | ✅ **ĐẠT** | `FruitShop.csproj:4` — `<TargetFramework>net8.0</TargetFramework>` — .NET 8 là LTS đến Nov 2026. |
| L3 | Không có package deprecated | ⚠️ **MỘT PHẦN** | `MeiliSearch 0.18.0` — thư viện chính thức đã đổi tên sang `Meilisearch` (lowercase). Version 0.18 khá cũ (2022). Cần kiểm tra lại. |
| L4 | Frontend JS/CSS CDN dùng version mới | ⚠️ **CHƯA RÕ** | Cần kiểm tra `Views/Shared/_Layout.cshtml` các CDN link. |
| L5 | Có quy trình cập nhật dependency định kỳ | ❌ **CHƯA ĐẠT** | Không có CI/CD pipeline, không có script tự động kiểm tra package lỗi thời. |

**🟡 Đánh giá nhóm L:** 1/5 đạt đủ, 3 một phần. Framework OK nhưng quản lý dependency cần cải thiện.

---

## 💥 NHÓM M — Phòng chống DDoS
> Từ slide: "Phòng chống DDoS" — Mức độ NÂNG CAO, Ưu tiên Cao, 2-4 tuần

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| M1 | Rate Limiting trên endpoint nhạy cảm | ❌ **CHƯA ĐẠT** | Login (`/Home/Login`), Register (`/Home/Register`), OTP (`/Home/VerifyOTP`), API cart — không có rate limiting. ASP.NET 8 có `AddRateLimiter` middleware nhưng chưa được cấu hình. |
| M2 | Giới hạn kích thước request body | ❌ **CHƯA ĐẠT** | `Program.cs` không có cấu hình `MaxRequestBodySize`. Default của Kestrel là 30MB — có thể bị lợi dụng. |
| M3 | Timeout cho request chậm | ⚠️ **MỘT PHẦN** | ASP.NET có default timeout nhưng không có cấu hình tường minh trong code. |
| M4 | CDN / Cloudflare phân tán traffic | ❌ **CHƯA ĐẠT** | Chưa có cấu hình CDN cho toàn site. Cloudinary chỉ dùng cho ảnh. |
| M5 | Blacklist / block IP bất thường | ❌ **CHƯA ĐẠT** | Không có cơ chế trong codebase. |
| M6 | Log và monitor traffic bất thường | ❌ **CHƯA ĐẠT** | Logging mặc định, không có alert / monitoring dashboard. |

**🔴 Đánh giá nhóm M:** 0/6 đạt. DDoS protection hoàn toàn chưa có — đây là mức NÂNG CAO, phù hợp với giai đoạn sau khi deploy thực tế.

---

## 🛡️ NHÓM N — Bảo mật Cơ bản Tổng hợp (Security Baseline)
> Từ slide: "Thiết lập các biện pháp bảo mật website cơ bản" — Mức CƠ BẢN, Tính khả thi: Rất cao

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| N1 | Đủ bảo mật cơ bản: HTTPS ✓, Input Validation ⚠️, Password Hashing ❌ | ⚠️ **MỘT PHẦN** | HTTPS đã có. Input Validation một phần. Password hashing chưa implement dù thư viện đã cài. |
| N2 | Duy trì uptime (monitoring) | ❌ **CHƯA ĐẠT** | Không có health check endpoint, không có uptime monitoring cấu hình trong dự án. |
| N3 | Xác thực đầu vào ở mọi điểm nhập | ⚠️ **MỘT PHẦN** | ModelState check có ở một số nơi. Còn nhiều action admin không validate đầu vào. |
| N4 | Cập nhật phần mềm định kỳ | ⚠️ **MỘT PHẦN** | Framework .NET 8 LTS OK. Package versions cần review. Không có CI auto-update. |
| N5 | Error page thân thiện, không lộ hệ thống | ✅ **ĐẠT** | `Program.cs:49` — `/Home/Error` ở production. Swagger chỉ ở Development. |

**🟡 Đánh giá nhóm N:** 1/5 đạt, 3 một phần.

---

## 📊 TỔNG KẾT CẬP NHẬT (SAU KHI THÊM NHÓM J-N)

| Nhóm | Tổng CP | ✅ Đạt | ❌ Chưa đạt | ⚠️ Một phần |
|------|---------|--------|------------|------------|
| A — Authentication & Session | 9 | 5 | 2 | 2 |
| B — Authorization / Access Control | 6 | 2 | 4 | 0 |
| C — SQL Injection & Validation | 4 | 3 | 0 | 1 |
| D — XSS Protection | 3 | 2 | 0 | 1 |
| E — CSRF Protection | 4 | 3 | 0 | 1 |
| F — HTTPS & Transport Security | 4 | 2 | 0 | 2 |
| G — Upload File | 4 | 2 | 1 | 1 |
| H — Tính năng nghiệp vụ | 19 | 16 | 1 | 2 |
| I — Cấu hình & Code Quality | 5 | 2 | 1 | 2 |
| **J — WAF** | **5** | **0** | **4** | **1** |
| **K — HTTPS & Mã hóa** | **6** | **4** | **1** | **1** |
| **L — Cập nhật Dependencies** | **5** | **1** | **1** | **3** |
| **M — Phòng chống DDoS** | **6** | **0** | **6** | **0** |
| **N — Security Baseline** | **5** | **1** | **1** | **3** |
| **TỔNG CỘNG** | **85** | **43** | **21** | **20** |

---

## 🔴 LỖI BỔ SUNG CẦN XỬ LÝ (từ nhóm J-N)

5. **[K5] Cookie không có Secure flag** — Thêm `options.Cookie.SecurePolicy = CookieSecurePolicy.Always` trong `Program.cs`
6. **[J3+M1] Không có Rate Limiting** — Thêm `builder.Services.AddRateLimiter()` cho Login, Register, OTP
7. **[M2] Không giới hạn request body size** — Cấu hình `MaxRequestBodySize` trong Kestrel
8. **[L3] MeiliSearch package cũ** — Cân nhắc cập nhật lên `Meilisearch` package chính thức

---
*Bổ sung scan từ: Slides bảo mật EPU14 — WAF, HTTPS, App Updates, DDoS, Security Baseline (17/09/2026)*

---

## 💾 NHÓM O — Kế hoạch Sao lưu & Phục hồi Dữ liệu
> Slide: "Yêu cầu bảo mật: Kế hoạch sao lưu và phục hồi dữ liệu" — CƠ BẢN, CAO, 1-2 tuần

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| O1 | Sao lưu DB tự động định kỳ | ❌ **CHƯA ĐẠT** | Không tìm thấy script backup, cron job, hoặc cấu hình scheduled backup trong codebase. DB dùng SQL Server local — phụ thuộc hoàn toàn vào người quản trị tự backup thủ công. |
| O2 | Bản sao lưu đa nơi (local + cloud) | ❌ **CHƯA ĐẠT** | Không có cấu hình backup to cloud (Azure Blob, GCS, S3...). |
| O3 | Kiểm tra / thử phục hồi từ bản sao lưu | ❌ **CHƯA ĐẠT** | Không có tài liệu hay script restore test. |
| O4 | Bản sao lưu offline | ❌ **CHƯA ĐẠT** | Chưa có chiến lược backup offline. |
| O5 | Tài liệu hóa Disaster Recovery Plan | ❌ **CHƯA ĐẠT** | Không có file DRP trong repository. |

**🔴 Đánh giá nhóm O:** 0/5 đạt. Đây là điểm yếu nghiêm trọng — nếu server hỏng hoặc bị tấn công thì mất toàn bộ dữ liệu. Cần bổ sung ngay kế hoạch backup.

---

## 👥 NHÓM P — Quản lý & Phân quyền Người dùng An toàn (Least Privilege)
> Slide: "Quản lý và Phân quyền Người dùng An toàn" — TRUNG BÌNH, CAO, 2-4 tuần

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| P1 | Áp dụng Least Privilege — cấp quyền tối thiểu theo vai trò | ⚠️ **MỘT PHẦN** | Hệ thống có 2 role (Admin RoleId=1, User RoleId=2). Phân chia đơn giản nhưng chưa có granular permission (ví dụ: chỉ xem được inventory nhưng không sửa được). |
| P2 | Phân quyền rõ ràng theo Role | ⚠️ **MỘT PHẦN** | Role được lưu trong DB và session. Tuy nhiên việc enforce role ở server-side KHÔNG có (nhóm B đã phát hiện — không có AdminOnly filter). |
| P3 | Ghi log hoạt động Admin / User (audit log) | ❌ **CHƯA ĐẠT** | Không có audit log nào trong codebase. Các thao tác CRUD (thêm/sửa/xóa sản phẩm, duyệt đơn hàng) không được ghi lại. |
| P4 | Định kỳ đánh giá thu hồi quyền | ❌ **CHƯA ĐẠT** | Không có tính năng / quy trình này trong hệ thống. |
| P5 | Admin không tự cấp quyền Admin cho mình | ⚠️ **MỘT PHẦN** | `UsersController` có chức năng thay đổi role. Cần kiểm tra xem có bảo vệ không cho user tự nâng quyền không. |

**🟡 Đánh giá nhóm P:** 0 đạt đủ, 3 một phần, 2 chưa đạt. Cần thêm audit log và AdminOnly filter.

---

## 📋 NHÓM Q — Chính sách Bảo mật & Nâng cao Nhận thức
> Slide: "Thiết lập Chính sách Bảo mật và Nâng cao Nhận thức" — CƠ BẢN, CAO, 1-2 tháng

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| Q1 | Có README / tài liệu quy tắc bảo mật | ⚠️ **MỘT PHẦN** | Có `GEMINI.md` ghi một số quy tắc chỉnh sửa code. Không có tài liệu bảo mật riêng (Security Policy). |
| Q2 | Code review tập trung bảo mật | ⚠️ **MỘT PHẦN** | Có `.github` folder — có thể có PR template. Nhưng không thấy security checklist trong review process. |
| Q3 | Quy trình xử lý sự cố bảo mật (Incident Response) | ❌ **CHƯA ĐẠT** | Không có tài liệu Incident Response Plan. |
| Q4 | Không commit thông tin nhạy cảm lên Git | ❌ **CHƯA ĐẠT** | `appsettings.json` chứa Cloudinary API secret, MeiliSearch key, Email password — có thể đã bị commit. `.gitignore` cần kiểm tra xem có exclude `appsettings.json` không. |
| Q5 | Tài liệu hóa endpoint nhạy cảm | ❌ **CHƯA ĐẠT** | Swagger có (chỉ ở dev) nhưng không có security documentation riêng cho từng endpoint. |

**🔴 Đánh giá nhóm Q:** 0 đạt, 2 một phần, 3 chưa đạt. Đặc biệt Q4 (secret trong git) là vấn đề NGHIÊM TRỌNG.

---

## 🔑 NHÓM R — Bảo vệ Tài khoản Quản trị
> Slide: "Bảo vệ tài khoản quản trị" — TRUNG BÌNH, CAO, 2-3 ngày

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| R1 | Admin dùng mật khẩu mạnh (≥12 ký tự, đa dạng ký tự) | ❌ **CHƯA ĐẠT** | Không có validation độ mạnh mật khẩu khi đăng ký / đổi mật khẩu. User có thể đặt mật khẩu "123" và hệ thống vẫn chấp nhận. |
| R2 | Mật khẩu Admin được hash BCrypt | ❌ **CHƯA ĐẠT** | Giống A1/A2 — mật khẩu lưu plain-text, áp dụng cho cả Admin. Đây là lỗ hổng NGHIÊM TRỌNG nhất. |
| R3 | Admin không tái sử dụng mật khẩu (chính sách) | ❌ **CHƯA ĐẠT** | Không có cơ chế / chính sách enforce việc này trong code. |
| R4 | MFA / 2FA cho Admin | ❌ **CHƯA ĐẠT** | Không có tính năng MFA/2FA trong hệ thống. Chỉ login bằng email + password đơn giản. |
| R5 | Thay đổi mật khẩu Admin định kỳ | ❌ **CHƯA ĐẠT** | Không có cơ chế nhắc thay đổi mật khẩu định kỳ. |
| R6 | Tài khoản Admin mặc định bị đổi tên / vô hiệu hóa | ⚠️ **CHƯA RÕ** | Cần kiểm tra DB xem có tài khoản admin mặc định như "admin@admin.com" không. |

**🔴 Đánh giá nhóm R:** 0/6 đạt. Bảo vệ tài khoản Admin rất yếu — không hash password, không MFA, không validate độ mạnh password.

---

## 📊 NHÓM S — Giám sát & Cải tiến Liên tục (Monitoring & SIEM)
> Slide: "Giám sát & Cải tiến liên tục" — TRUNG BÌNH (Vừa), CAO, 3-6 tháng

| ID | Checkpoint | Kết quả | Chi tiết |
|----|-----------|---------|---------|
| S1 | Log đầy đủ hoạt động hệ thống (login, CRUD quan trọng) | ❌ **CHƯA ĐẠT** | ASP.NET logging mặc định (console/file). Không có structured logging cho security events (login thành công/thất bại, thay đổi role, xóa dữ liệu). |
| S2 | Cảnh báo hoạt động bất thường | ❌ **CHƯA ĐẠT** | Không có alert system, không có notification khi đăng nhập thất bại nhiều lần hay IP lạ. |
| S3 | Tập trung log (SIEM / log aggregator) | ❌ **CHƯA ĐẠT** | Không có SIEM, Elasticsearch, Splunk hay bất kỳ log aggregator nào. |
| S4 | Định kỳ Security Assessment | ❌ **CHƯA ĐẠT** | Không có quy trình / lịch audit bảo mật. |
| S5 | Penetration Testing bởi bên thứ ba | ❌ **CHƯA ĐẠT** | Chưa thực hiện (phù hợp với dự án học tập — cần sau khi deploy thực tế). |
| S6 | Đào tạo nhóm dev về secure coding | ⚠️ **MỘT PHẦN** | Đây là môn học Lập trình Web Nâng cao — có học về bảo mật. Nhưng chưa áp dụng đầy đủ vào code. |

**🔴 Đánh giá nhóm S:** 0/6 đạt. Nhóm này phù hợp với dự án thực tế production hơn — có thể để sprint sau.

---

## 📊 TỔNG KẾT CUỐI CÙNG (ĐẦY ĐỦ TẤT CẢ NHÓM A-S)

| Nhóm | Tổng CP | ✅ Đạt | ❌ Chưa đạt | ⚠️ Một phần |
|------|---------|--------|------------|------------|
| A — Authentication & Session | 9 | 5 | 2 | 2 |
| B — Authorization / Access Control | 6 | 2 | 4 | 0 |
| C — SQL Injection & Validation | 4 | 3 | 0 | 1 |
| D — XSS Protection | 3 | 2 | 0 | 1 |
| E — CSRF Protection | 4 | 3 | 0 | 1 |
| F — HTTPS & Transport Security | 4 | 2 | 0 | 2 |
| G — Upload File | 4 | 2 | 1 | 1 |
| H — Tính năng nghiệp vụ | 19 | 16 | 1 | 2 |
| I — Cấu hình & Code Quality | 5 | 2 | 1 | 2 |
| J — WAF | 5 | 0 | 4 | 1 |
| K — HTTPS & Mã hóa | 6 | 4 | 1 | 1 |
| L — Cập nhật Dependencies | 5 | 1 | 1 | 3 |
| M — Phòng chống DDoS | 6 | 0 | 6 | 0 |
| N — Security Baseline | 5 | 1 | 1 | 3 |
| **O — Backup & Recovery** | **5** | **0** | **5** | **0** |
| **P — Least Privilege / User Mgmt** | **5** | **0** | **2** | **3** |
| **Q — Security Policy** | **5** | **0** | **3** | **2** |
| **R — Admin Account Protection** | **6** | **0** | **5** | **1** |
| **S — Monitoring & SIEM** | **6** | **0** | **5** | **1** |
| **TỔNG CỘNG** | **112** | **43** | **42** | **27** |

---

## 🎯 PHÂN LOẠI THEO ĐỘ KHÓ TRIỂN KHAI

### 🟢 Fix nhanh (≤ 3 ngày) — Nên làm NGAY
| Ưu tiên | Vấn đề | Checkpoint |
|---------|--------|-----------|
| 1 | Hash mật khẩu bằng BCrypt (thư viện đã cài!) | A1, A2, R2 |
| 2 | Thêm AdminOnly filter cho Controller Admin | B1, B2, B5 |
| 3 | Thêm Secure flag cho Session cookie | K5 |
| 4 | Validate độ mạnh mật khẩu khi đăng ký | R1 |
| 5 | Validate file extension trước khi upload | G1 |

### 🟡 Fix trong 1-2 tuần — Trung bình
| Ưu tiên | Vấn đề | Checkpoint |
|---------|--------|-----------|
| 6 | Rate Limiting cho Login / Register / OTP | J3, M1 |
| 7 | Chuyển API secrets ra biến môi trường | Q4, F4 |
| 8 | Thêm audit log cho thao tác Admin | P3, S1 |
| 9 | Thiết lập backup DB tự động | O1, O2 |

### 🔵 Dài hạn — Nâng cao (sau khi deploy thực tế)
| Vấn đề | Checkpoint |
|--------|-----------|
| MFA/2FA cho Admin | R4 |
| WAF / Cloudflare | J4, M4 |
| SIEM / Log aggregator | S3 |
| Penetration Testing | S5 |
| DDoS protection cloud | M4, M5 |

---
*Scan hoàn chỉnh — Tổng 112 checkpoints từ 19 nhóm (A-S). Bổ sung từ Slides EPU14: Backup, Least Privilege, Security Policy, Admin Protection, Monitoring (17/09/2026)*
