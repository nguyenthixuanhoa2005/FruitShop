# 📋 CHECKPOINTS — Dự Án FruitShop (ASP.NET MVC 8)

> Danh sách các checkpoint kiểm tra chất lượng & bảo mật phù hợp với ngữ cảnh học phần **Lập trình Web Nâng cao** và yêu cầu môn học (OWASP, bảo mật cơ bản web).

---

## 🔐 NHÓM A — Bảo mật xác thực & Session (Authentication & Session Management)

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| A1 | Mật khẩu được **hash** trước khi lưu DB (BCrypt / PBKDF2) | 🔴 Cao |
| A2 | Login **không** so sánh mật khẩu dạng plain-text | 🔴 Cao |
| A3 | Session cookie được đặt `HttpOnly = true` | 🟡 Trung bình |
| A4 | Session cookie được đặt `IsEssential = true` | 🟢 Thấp |
| A5 | Session có timeout hợp lý (≤ 60 phút) | 🟢 Thấp |
| A6 | Logout **xóa toàn bộ** session (`Session.Clear()`) | 🟡 Trung bình |
| A7 | Forgot Password gửi OTP qua Email (không lộ token trên URL) | 🟡 Trung bình |
| A8 | OTP có thời hạn hiệu lực (expiry) và bị xóa sau khi dùng | 🟡 Trung bình |
| A9 | Reset token bị **xóa khỏi DB** sau khi đặt lại mật khẩu thành công | 🟡 Trung bình |

---

## 🛡️ NHÓM B — Phân quyền & Kiểm soát truy cập (Authorization & Access Control)

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| B1 | Admin routes được bảo vệ — không cho phép user thường truy cập | 🔴 Cao |
| B2 | Có middleware / filter kiểm tra role trước khi vào Controller Admin | 🔴 Cao |
| B3 | User chỉ xem được đơn hàng **của chính mình** | 🔴 Cao |
| B4 | User chỉ sửa được profile **của chính mình** | 🔴 Cao |
| B5 | Trang Dashboard / Statistics chỉ dành cho Admin | 🔴 Cao |
| B6 | Kiểm tra session hợp lệ trước mỗi action nhạy cảm | 🟡 Trung bình |

---

## 🧪 NHÓM C — Chống SQL Injection & Input Validation

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| C1 | Dùng Entity Framework / Parameterized Query (không raw SQL string concat) | 🔴 Cao |
| C2 | Không có câu SQL thủ công nối chuỗi từ input người dùng | 🔴 Cao |
| C3 | Validate input phía server (ModelState.IsValid) trước khi xử lý | 🟡 Trung bình |
| C4 | Không trả về thông báo lỗi DB chi tiết ra ngoài | 🟡 Trung bình |

---

## 🌐 NHÓM D — Chống XSS (Cross-Site Scripting)

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| D1 | Views dùng Razor @Model.Field (tự encode HTML — không dùng @Html.Raw với dữ liệu user) | 🔴 Cao |
| D2 | Không inject nội dung người dùng thẳng vào JS/HTML mà không encode | 🔴 Cao |
| D3 | Error/success message hiển thị qua Razor (encoded) | 🟡 Trung bình |

---

## 🔄 NHÓM E — Chống CSRF (Cross-Site Request Forgery)

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| E1 | Form POST có @Html.AntiForgeryToken() | 🔴 Cao |
| E2 | Action POST có [ValidateAntiForgeryToken] | 🔴 Cao |
| E3 | AJAX POST gửi RequestVerificationToken trong header | 🟡 Trung bình |
| E4 | Antiforgery header name được cấu hình trong Program.cs | 🟢 Thấp |

---

## 🌍 NHÓM F — HTTPS & Transport Security

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| F1 | app.UseHttpsRedirection() được kích hoạt | 🔴 Cao |
| F2 | HSTS được bật ở môi trường Production (app.UseHsts()) | 🟡 Trung bình |
| F3 | Connection string không chứa plain-text password trong code commit | 🟡 Trung bình |
| F4 | API keys / secret không hardcode rõ ràng trong appsettings | 🟡 Trung bình |

---

## 🖼️ NHÓM G — Upload File & Direct Object Reference

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| G1 | Upload ảnh chỉ cho phép định dạng hình ảnh (PNG, JPG, WEBP) | 🔴 Cao |
| G2 | File upload được lưu qua Cloudinary — không lưu local trên server | 🟡 Trung bình |
| G3 | Transformation ảnh được áp dụng (nén/optimize) | 🟢 Thấp |
| G4 | Không lộ đường dẫn file nội bộ server cho user | 🟡 Trung bình |

---

## 🧩 NHÓM H — Tính năng Nghiệp vụ Cốt lõi

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| H1 | Trang chủ hiển thị sản phẩm nổi bật / bán chạy | 🟢 |
| H2 | Đăng ký tài khoản mới | 🟢 |
| H3 | Đăng nhập / Đăng xuất | 🟢 |
| H4 | Quên mật khẩu / OTP / Reset Password | 🟢 |
| H5 | Giỏ hàng: thêm, sửa số lượng, xóa | 🟢 |
| H6 | Thanh toán / Đặt hàng | 🟢 |
| H7 | Xem lịch sử đơn hàng | 🟢 |
| H8 | Hủy đơn hàng | 🟢 |
| H9 | Đánh giá sản phẩm sau khi mua | 🟢 |
| H10 | Tìm kiếm sản phẩm (MeiliSearch full-text) | 🟢 |
| H11 | Mã giảm giá (Coupon) khi checkout | 🟢 |
| H12 | Admin: CRUD Sản phẩm + Upload ảnh Cloudinary | 🟢 |
| H13 | Admin: Quản lý đơn hàng + thay đổi trạng thái | 🟢 |
| H14 | Admin: Dashboard thống kê | 🟢 |
| H15 | Admin: Quản lý kho (Inventory Log) | 🟢 |
| H16 | Admin: Xuất Excel (ClosedXML) | 🟢 |
| H17 | Admin: Quản lý danh mục (Categories) phân cấp | 🟢 |
| H18 | Admin: Quản lý nhà cung cấp (Suppliers) | 🟢 |
| H19 | Admin: Quản lý người dùng | 🟢 |

---

## ⚙️ NHÓM I — Cấu hình & Chất lượng Code

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| I1 | Không có TODO bảo mật nghiêm trọng còn bỏ ngỏ | 🔴 Cao |
| I2 | Error handling không lộ stack trace ở production | 🟡 Trung bình |
| I3 | Swagger chỉ bật ở Development | 🟢 Thấp |
| I4 | Docker / docker-compose hỗ trợ deploy | 🟢 Thấp |
| I5 | MeiliSearch được cấu hình và đồng bộ dữ liệu | 🟢 Thấp |

---
*Checkpoint xây dựng dựa trên: OWASP Top 10, yêu cầu môn Lập trình Web Nâng cao, đặc thù dự án FruitShop ASP.NET MVC 8.*

---

## 🔥 NHÓM J — WAF (Tường lửa ứng dụng web)
> Nguồn: Slide "Tường lửa ứng dụng web (WAF)" — Mức độ: Trung bình, Thời gian: 2-6 tuần

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| J1 | Có cơ chế chặn / giới hạn các request độc hại (SQL Injection, XSS patterns) | 🔴 Cao |
| J2 | Thiết lập quy tắc chặn truy cập trái phép và kiểm soát IP đáng ngờ | 🔴 Cao |
| J3 | Giới hạn số lần gọi API / login (rate limiting / brute-force protection) | 🔴 Cao |
| J4 | Cân nhắc giải pháp WAF cloud (Cloudflare, AWS WAF) hoặc phần mềm tích hợp | 🟡 Trung bình |
| J5 | Log lại các request bị chặn / bất thường để phục vụ audit | 🟡 Trung bình |

---

## 🔒 NHÓM K — HTTPS & Mã hóa dữ liệu
> Nguồn: Slide "HTTPS: Mã hóa dữ liệu" — Mức độ ưu tiên: CAO, Tính khả thi: Dễ, Timeline: 1-3 ngày

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| K1 | Triển khai chứng chỉ SSL/TLS hợp lệ (Let's Encrypt / tự ký cho dev) | 🔴 Cao |
| K2 | Tất cả request HTTP tự động redirect sang HTTPS | 🔴 Cao |
| K3 | Không có mixed content (ảnh, script, CSS tải qua HTTP trên trang HTTPS) | 🟡 Trung bình |
| K4 | HTTP Strict Transport Security (HSTS) được thiết lập | 🟡 Trung bình |
| K5 | Cookie chỉ truyền qua HTTPS (Secure flag) | 🟡 Trung bình |
| K6 | Không lưu dữ liệu nhạy cảm (password, token) dạng plain-text trong cookie / localStorage | 🔴 Cao |

---

## 🔄 NHÓM L — Cập nhật ứng dụng & Dependencies
> Nguồn: Slide "Cập nhật ứng dụng & plugin" — Mức độ: CƠ BẢN (Đã), Ưu tiên: CAO, Timeline: 1-3 ngày ban đầu

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| L1 | Tất cả NuGet packages được cập nhật lên phiên bản mới nhất / không có CVE đã biết | 🔴 Cao |
| L2 | ASP.NET Core framework đang dùng phiên bản LTS được hỗ trợ (.NET 8 ✓) | 🟡 Trung bình |
| L3 | Không còn package đã bị deprecate hoặc không còn maintain | 🟡 Trung bình |
| L4 | Frontend libraries (JS/CSS từ CDN) dùng version mới nhất | 🟢 Thấp |
| L5 | Có quy trình kiểm tra và cập nhật dependency định kỳ (script / CI check) | 🟢 Thấp |

---

## 💥 NHÓM M — Phòng chống DDoS
> Nguồn: Slide "Phòng chống DDoS" — Mức độ: NÂNG CAO, Ưu tiên: Cao, Timeline: 2-4 tuần

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| M1 | Có Rate Limiting trên các endpoint nhạy cảm (Login, Register, OTP, API) | 🔴 Cao |
| M2 | Giới hạn kích thước request body (không nhận payload quá lớn) | 🟡 Trung bình |
| M3 | Timeout cho các request chậm / connection lâu | 🟡 Trung bình |
| M4 | CDN phân tán traffic (Cloudflare hoặc tương đương) — giảm tải server | 🟡 Trung bình |
| M5 | Blacklist / block IP khi phát hiện hành vi bất thường | 🟡 Trung bình |
| M6 | Log và monitor traffic bất thường (đột biến request) | 🟢 Thấp |

---

## 🛡️ NHÓM N — Bảo mật Cơ bản Tổng hợp (Security Baseline)
> Nguồn: Slide "Thiết lập các biện pháp bảo mật website cơ bản" — Mức độ: CƠ BẢN (Đã), Timeline: 1-2 tuần, Tính khả thi: Rất cao

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| N1 | Triển khai đầy đủ các biện pháp bảo mật cơ bản: HTTPS, Input Validation, Password Hashing | 🔴 Cao |
| N2 | Duy trì ứng dụng web luôn chạy (uptime monitoring) | 🟡 Trung bình |
| N3 | Xác thực đầu vào người dùng ở mọi điểm nhập dữ liệu | 🔴 Cao |
| N4 | Cập nhật phần mềm định kỳ để vá lỗ hổng bảo mật đã biết | 🟡 Trung bình |
| N5 | Có Error page thân thiện, không lộ thông tin hệ thống | 🟡 Trung bình |

---
*Checkpoint cập nhật thêm từ: Slides bảo mật EPU14 — WAF, HTTPS, App Updates, DDoS Protection, Security Baseline.*

---

## 💾 NHÓM O — Kế hoạch Sao lưu & Phục hồi Dữ liệu (Backup & Recovery)
> Slide: "Yêu cầu bảo mật: Kế hoạch sao lưu và phục hồi dữ liệu" — Mức CƠ BẢN, Ưu tiên CAO, 1-2 tuần

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| O1 | Có kế hoạch sao lưu DB tự động định kỳ (hàng ngày / hàng tuần) | 🔴 Cao |
| O2 | Bản sao lưu được lưu đa nơi (local + cloud hoặc trung tâm dữ liệu khác) | 🔴 Cao |
| O3 | Định kỳ kiểm tra / thử phục hồi từ bản sao lưu | 🟡 Trung bình |
| O4 | Có ít nhất 1 bản sao lưu offline (bảo vệ khỏi ransomware / tấn công mạng) | 🟡 Trung bình |
| O5 | Tài liệu hóa quy trình phục hồi (Disaster Recovery Plan) | 🟢 Thấp |

---

## 👥 NHÓM P — Quản lý & Phân quyền Người dùng An toàn (Least Privilege)
> Slide: "Quản lý và Phân quyền Người dùng An toàn" — Mức TRUNG BÌNH, Ưu tiên CAO, 2-4 tuần

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| P1 | Áp dụng nguyên tắc Least Privilege — cấp quyền tối thiểu cần thiết theo vai trò | 🔴 Cao |
| P2 | Phân quyền rõ ràng theo Role (Admin, Biên tập viên, User thông thường) | 🔴 Cao |
| P3 | Ghi log đầy đủ hoạt động của Admin / User quan trọng (audit log) | 🟡 Trung bình |
| P4 | Định kỳ đánh giá và thu hồi quyền không còn cần thiết | 🟡 Trung bình |
| P5 | Admin không thể tự cấp quyền Admin cho chính mình | 🟡 Trung bình |

---

## 📋 NHÓM Q — Chính sách Bảo mật & Nâng cao Nhận thức
> Slide: "Thiết lập Chính sách Bảo mật và Nâng cao Nhận thức" — Mức CƠ BẢN, Ưu tiên CAO, 1-2 tháng

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| Q1 | Có file README / tài liệu ghi rõ các quy tắc bảo mật của dự án | 🔴 Cao |
| Q2 | Code review tập trung vào bảo mật trước khi merge | 🟡 Trung bình |
| Q3 | Có quy trình báo cáo và xử lý sự cố bảo mật (Incident Response) | 🟡 Trung bình |
| Q4 | Không commit thông tin nhạy cảm lên Git (secret scanning / .gitignore) | 🔴 Cao |
| Q5 | Tài liệu hóa các endpoint / route nhạy cảm và cách bảo vệ chúng | 🟢 Thấp |

---

## 🔑 NHÓM R — Bảo vệ Tài khoản Quản trị (Admin Account Security)
> Slide: "Bảo vệ tài khoản quản trị" — Mức TRUNG BÌNH, Ưu tiên CAO, 2-3 ngày

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| R1 | Tài khoản Admin dùng mật khẩu mạnh (≥12 ký tự, chữ hoa+thường+số+ký tự đặc biệt) | 🔴 Cao |
| R2 | Mật khẩu Admin được hash bằng BCrypt / thuật toán mạnh | 🔴 Cao |
| R3 | Admin không tái sử dụng mật khẩu từ dịch vụ khác (chính sách) | 🟡 Trung bình |
| R4 | Xác thực đa nhân tố (MFA / 2FA) cho tài khoản Admin | 🟡 Trung bình |
| R5 | Thay đổi mật khẩu Admin định kỳ (tối thiểu mỗi năm 1 lần) | 🟢 Thấp |
| R6 | Tài khoản Admin mặc định (default admin) bị đổi tên hoặc vô hiệu hóa | 🟡 Trung bình |

---

## 📊 NHÓM S — Giám sát & Cải tiến Liên tục (Monitoring & Continuous Improvement)
> Slide: "Giám sát & Cải tiến liên tục" — Mức TRUNG BÌNH (Vừa), Ưu tiên CAO, Timeline: 3-6 tháng

| ID | Checkpoint | Mức độ ưu tiên |
|----|-----------|----------------|
| S1 | Ghi log đầy đủ hoạt động hệ thống (login, logout, thay đổi dữ liệu quan trọng) | 🔴 Cao |
| S2 | Có cảnh báo (alert) khi phát hiện hoạt động bất thường | 🟡 Trung bình |
| S3 | Tập trung log về một nơi để phân tích (SIEM hoặc log aggregator) | 🟡 Trung bình |
| S4 | Định kỳ thực hiện Security Assessment / kiểm tra bảo mật | 🟡 Trung bình |
| S5 | Penetration Testing bởi bên thứ ba để phát hiện lỗ hổng | 🟢 Thấp |
| S6 | Văn hóa bảo mật: đào tạo nhóm dev về secure coding định kỳ | 🟢 Thấp |

---
*Bổ sung thêm từ: Slides EPU14 — Backup/Recovery, Least Privilege, Security Policy, Admin Protection, Monitoring (17/09/2026)*
