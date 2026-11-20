# 🍊 FruitShop

<div align="center">

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)
![Python](https://img.shields.io/badge/Python-3.10+-3776AB?style=for-the-badge&logo=python&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**Nền tảng thương mại điện tử trái cây thông minh — tích hợp AI Chatbot, full-text search và thanh toán tự động.**

[✨ Tính năng](#-tính-năng-chính) · [🚀 Cài đặt](#️-cài-đặt--chạy) · [🤖 AI Chatbot](#-ai-chatbot) · [📸 Giao diện](#-giao-diện)

</div>

---

## 📖 Giới thiệu

**FruitShop** là một ứng dụng web thương mại điện tử chuyên về trái cây, được xây dựng trên nền tảng **ASP.NET Core MVC 8** với đầy đủ chức năng từ bán hàng, quản lý kho, đến thống kê kinh doanh. Điểm đặc biệt của dự án là tích hợp **AI Chatbot** sử dụng Google Gemini 1.5 Flash và ChromaDB để tư vấn mua hàng thông minh, cùng với **MeiliSearch** cho trải nghiệm tìm kiếm tức thì và **SePay webhook** để tự động xác nhận thanh toán chuyển khoản.

---

## ✨ Điểm Nổi Bật

- 🤖 **AI Chatbot thông minh** — Tích hợp Google Gemini 1.5 Flash + ChromaDB vector search, hiểu tiếng Việt tự nhiên, nhận diện intent, sửa typo/teen-code tự động
- ⚡ **Full-text Search siêu tốc** — MeiliSearch self-hosted qua Docker, kết quả tức thì, hỗ trợ filter đa tiêu chí và pushState URL không reload trang
- 💳 **Thanh toán tự động** — Tích hợp **SePay webhook** xác nhận chuyển khoản ngân hàng real-time, hiển thị QR code động
- ☁️ **Lưu trữ ảnh thông minh** — Cloudinary với auto-convert sang WebP/AVIF, upload nhiều ảnh, chọn ảnh chính cho sản phẩm
- 📊 **Excel Import/Export toàn diện** — ClosedXML cho sản phẩm, danh mục, đơn hàng, kho hàng, nhà cung cấp, người dùng
- 🌳 **Danh mục phân cấp** — Hệ thống cha-con-cháu linh hoạt với checkbox filter đa cấp trên trang sản phẩm
- 📦 **Quản lý kho chuyên nghiệp** — Phiếu nhập/xuất có mã tự động (PN-/PX-), cảnh báo sắp hết hàng, in phiếu kho
- 📈 **Dashboard real-time** — KPI cards, biểu đồ Doughnut, top bestsellers với progress bar tồn kho, chọn ngày linh hoạt

---

## 📸 Giao Diện

> 🖼️ **Homepage Hero Banners** — Carousel 4 slides tự động chạy, hiển thị các chương trình khuyến mãi nổi bật.

| Slide 1 | Slide 2 |
|:-:|:-:|
| ![Homepage Slide 1](docs/screenshots/homepage_slide1.png) | ![Homepage Slide 2](docs/screenshots/homepage_slide2.png) |
| **Slide 3** | **Slide 4** |
| ![Homepage Slide 3](docs/screenshots/homepage_slide3.png) | ![Homepage Slide 4](docs/screenshots/homepage_slide4.jpg) |

| Bestsellers Section |
|:-:|
| ![Bestseller Section](docs/screenshots/bestseller_section.jpg) |

<details>
<summary>📐 Mô tả chi tiết các màn hình khác</summary>

| Màn hình | Mô tả |
|---|---|
| **Trang chủ** | Hero carousel 700px, 4 section sản phẩm (Bestseller, Nhập Khẩu, Nội Địa, Quà Tặng) có "Xem thêm", bar 4 USP |
| **Danh sách sản phẩm** | Sidebar filter phân cấp, khoảng giá, xuất xứ; AJAX partial update; sort & pagination |
| **Chi tiết sản phẩm** | Gallery ảnh Cloudinary, rating nửa sao, reviews, sản phẩm liên quan |
| **Giỏ hàng** | Table layout, checkbox chọn từng sản phẩm, coupon, phí ship, sticky summary |
| **Admin Dashboard** | Dark mode, doughnut chart đơn hàng, stat cards (hôm nay + tổng), top 5 bestsellers |
| **Quản lý đơn hàng** | Bảng phân trang, badge màu theo trạng thái, modal chi tiết, cập nhật trạng thái |

</details>

---

## 🚀 Tính Năng Chính

### 👤 Dành cho Khách Hàng

| # | Tính năng | Chi tiết |
|---|---|---|
| 1 | 🏠 **Trang chủ** | Hero carousel 4 slides, Bestseller/Nhập Khẩu/Nội Địa/Quà Tặng với "Xem thêm", 4 USP bars (Giao hàng 2h, Chất lượng, Hoàn tiền 100%, Hỗ trợ 24/7) |
| 2 | 🔍 **Tìm kiếm & Lọc** | MeiliSearch full-text, sidebar filter đa tiêu chí (danh mục phân cấp, khoảng giá, xuất xứ), sort, pagination, pushState URL |
| 3 | 🛍️ **Chi tiết sản phẩm** | Gallery đa ảnh Cloudinary, rating ⭐ hỗ trợ nửa sao, reviews khách hàng, sản phẩm liên quan |
| 4 | 🛒 **Giỏ hàng** | Thêm/xóa/cập nhật số lượng, checkbox chọn từng sản phẩm, áp mã coupon, tính phí ship 30,000đ |
| 5 | 💳 **Thanh toán** | COD + Chuyển khoản ngân hàng (QR code), tự động xác nhận qua SePay webhook |
| 6 | 📋 **Quản lý đơn hàng** | Lịch sử đơn, xem chi tiết, hủy đơn (khi đang chờ xử lý), xác nhận đã nhận hàng |
| 7 | ⭐ **Đánh giá sản phẩm** | Rating + comment sau khi nhận hàng, trạng thái hiển thị/ẩn |
| 8 | 👤 **Tài khoản** | Xem/sửa thông tin profile, đổi mật khẩu với BCrypt |
| 9 | 🔐 **Xác thực** | Đăng ký, Đăng nhập, Quên mật khẩu (OTP qua Gmail SMTP), Xác minh OTP, Reset mật khẩu |
| 10 | 🤖 **AI Chatbot** | Tư vấn mua hàng thông minh, NLP tiếng Việt, vector search, đề xuất sản phẩm, context memory, feedback 👍👎 |

### 🏢 Dành cho Admin

| # | Tính năng | Chi tiết |
|---|---|---|
| 1 | 📊 **Dashboard** | 4 KPI cards, biểu đồ Doughnut trạng thái đơn, top 5 bestsellers + progress bar tồn kho, chọn ngày, sync MeiliSearch |
| 2 | 📦 **Quản lý Đơn hàng** | Danh sách phân trang, lọc đa tiêu chí (tên/trạng thái/ngày/giá), modal chi tiết, cập nhật trạng thái, **Export Excel** |
| 3 | 🍎 **Quản lý Sản phẩm** | CRUD đầy đủ, upload đa ảnh Cloudinary, lọc (SKU/xuất xứ/danh mục/NCC/giá/tồn kho), **Import/Export Excel/CSV**, sao chép sản phẩm |
| 4 | 🌳 **Danh mục** | CRUD phân cấp cha-con-cháu, **Import/Export Excel/CSV/JSON/XML** |
| 5 | 🎟️ **Mã giảm giá** | CRUD coupon (phần trăm/cố định), ngày hiệu lực, giới hạn sử dụng, giá trị đơn tối thiểu, giảm tối đa, **Import/Export Excel** |
| 6 | 🏪 **Kho hàng** | Phiếu nhập (PN-) / xuất (PX-) nhiều sản phẩm, nhóm theo đợt, cảnh báo < 10 đơn vị, in phiếu, **Export Excel** |
| 7 | 📈 **Thống kê** | Doanh thu 12 tháng, top 5 khách hàng trung thành, tỷ lệ quay lại (retention), AOV |
| 8 | 🏭 **Nhà cung cấp** | CRUD, tìm kiếm, **Export/Import Excel/CSV/JSON/XML** |
| 9 | 👥 **Người dùng** | MeiliSearch search, lọc role/trạng thái, soft-delete, **Export Excel** |
| 10 | ⭐ **Duyệt đánh giá** | Duyệt/ẩn/xóa mềm reviews, thống kê tổng review và rating trung bình |

---

## 🛠️ Công Nghệ Sử Dụng

### Backend & Framework

| Công nghệ | Phiên bản | Mục đích |
|---|---|---|
| **ASP.NET Core MVC** | 8.0 | Web framework chính |
| **Entity Framework Core** | 8.0 | ORM, database migrations |
| **SQL Server** | 2019+ | Cơ sở dữ liệu chính |
| **BCrypt.Net-Next** | 4.0.3 | Hash mật khẩu an toàn |
| **MeiliSearch** | 0.18.0 | Full-text search engine (Docker) |
| **CloudinaryDotNet** | 1.26.2 | Cloud image storage, auto WebP/AVIF |
| **ClosedXML** | 0.102.2 | Đọc/ghi file Excel (.xlsx) |
| **Swashbuckle.AspNetCore** | 10.1.7 | Swagger/OpenAPI documentation |

### AI Chatbot Service

| Công nghệ | Phiên bản | Mục đích |
|---|---|---|
| **Python** | 3.10+ | Runtime cho chatbot service |
| **FastAPI** | Latest | REST API framework cho chatbot |
| **Google Gemini** | 1.5 Flash | LLM — sinh câu trả lời & đề xuất |
| **ChromaDB** | Latest | Vector database cho semantic search |
| **Dify.ai** | — | AI workflow orchestration |
| **pyodbc** | Latest | Kết nối SQL Server từ Python |

### Infrastructure & Integrations

| Công nghệ | Mục đích |
|---|---|
| **Docker / docker-compose** | Chạy MeiliSearch container |
| **SePay Webhook** | Tự động xác nhận thanh toán chuyển khoản |
| **Gmail SMTP** | Gửi OTP email quên mật khẩu |
| **Cloudinary CDN** | Phân phối hình ảnh toàn cầu, tối ưu định dạng |

---

## 📋 Yêu Cầu Hệ Thống

Trước khi cài đặt, hãy đảm bảo đã có:

- ✅ **.NET 8.0 SDK** — [Tải tại đây](https://dotnet.microsoft.com/download/dotnet/8)
- ✅ **SQL Server** — Express, Developer hoặc bất kỳ edition nào (2019+)
- ✅ **Docker Desktop** — Để chạy MeiliSearch ([Tải tại đây](https://www.docker.com/products/docker-desktop))
- ✅ **Python 3.10+** — Cho AI Chatbot service ([Tải tại đây](https://www.python.org/downloads/))
- ✅ **Git** — Để clone repository

> [!NOTE]
> Chatbot là service tùy chọn — ứng dụng chính hoạt động bình thường khi không có chatbot.

---

## ⚙️ Cài Đặt & Chạy

### 1. Clone Repository

```bash
git clone https://github.com/your-username/FruitShop.git
cd FruitShop
```

### 2. Khởi động MeiliSearch (Docker)

```bash
docker-compose up -d
```

> MeiliSearch sẽ chạy trên `http://localhost:7700`

### 3. Tạo Database

Mở **SQL Server Management Studio** (hoặc bất kỳ SQL client nào), tạo database:

```sql
CREATE DATABASE fruit_shop;
```

### 4. Chạy Database Migration

```bash
cd FruitShop
dotnet ef database update
```

### 5. Cấu hình `appsettings.json`

Mở file `FruitShop/appsettings.json` và điền các thông tin sau:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=fruit_shop;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  },
  "MeiliSearch": {
    "Url": "http://localhost:7700",
    "ApiKey": "your-meilisearch-key"
  },
  "EmailSettings": {
    "Email": "yourmail@gmail.com",
    "AppPassword": "your-gmail-app-password"
  }
}
```

> [!IMPORTANT]
> Với Gmail SMTP, bạn cần tạo **App Password** (không phải mật khẩu thường). Vào Google Account → Security → 2-Step Verification → App Passwords.

### 6. Chạy Ứng Dụng

```bash
cd FruitShop
dotnet run
```

Ứng dụng sẽ chạy tại: **`http://localhost:8081`**

---

### 7. (Tùy chọn) Chạy AI Chatbot

```bash
cd Chatbot
```

Tạo file `config_private.py`:

```python
SQL_CONN_STR = "DRIVER={ODBC Driver 17 for SQL Server};SERVER=localhost;DATABASE=fruit_shop;Trusted_Connection=yes"
GEMINI_API_KEY = "your-gemini-api-key"
DIFY_API_KEY = "your-dify-api-key"
```

Cài đặt dependencies và khởi chạy:

```bash
pip install fastapi uvicorn pyodbc chromadb google-generativeai
python main.py
```

Chatbot API sẽ chạy tại: **`http://localhost:8000`**

---

## 📁 Cấu Trúc Dự Án

```
FruitShop/
├── 📂 FruitShop/                  # ASP.NET Core MVC project
│   ├── 📂 Controllers/            # MVC Controllers (Admin + Customer)
│   │   ├── 📂 Admin/              # AdminOrderController, ProductController...
│   │   └── 📂 ...                 # HomeController, CartController, AuthController...
│   ├── 📂 Models/                 # Entity models & ViewModels
│   ├── 📂 Views/                  # Razor Views (.cshtml)
│   │   ├── 📂 Admin/              # Admin dashboard views
│   │   ├── 📂 Home/               # Trang chủ, danh sách, chi tiết
│   │   ├── 📂 Cart/               # Giỏ hàng & Thanh toán
│   │   └── 📂 Shared/             # Layout, partials
│   ├── 📂 Services/               # Business logic services
│   ├── 📂 Data/                   # DbContext, Migrations
│   ├── 📂 wwwroot/                # Static files (CSS, JS, Images)
│   └── appsettings.json           # Cấu hình ứng dụng
│
├── 📂 Chatbot/                    # Python FastAPI AI Chatbot
│   ├── main.py                    # FastAPI entry point
│   ├── config_private.py          # Credentials (không commit)
│   └── ...
│
├── 📂 docs/
│   └── 📂 screenshots/            # Ảnh chụp màn hình
│       ├── homepage_slide1.png
│       ├── homepage_slide2.png
│       ├── homepage_slide3.png
│       ├── homepage_slide4.jpg
│       └── bestseller_section.jpg
│
├── docker-compose.yml             # MeiliSearch container config
└── README.md
```

---

## 🤖 AI Chatbot

FruitShop tích hợp một AI Chatbot riêng biệt chạy dưới dạng **microservice Python**, cung cấp trải nghiệm tư vấn mua hàng thông minh ngay trên website.

### Kiến trúc Chatbot

```
Khách hàng
    ↓ (gửi tin nhắn)
ASP.NET Core → HTTP → FastAPI (Python :8000)
                           ↓
                   Intent Classifier
                   ┌─────────────────┐
                   │ Tư vấn mua hàng │ → ChromaDB Vector Search
                   │                 │   → SQL Server (sản phẩm)
                   │                 │   → Gemini 1.5 Flash (sinh câu)
                   ├─────────────────┤
                   │ Trò chuyện thông│ → Gemini 1.5 Flash
                   │ thường           │   (context memory)
                   └─────────────────┘
```

### Tính Năng Chatbot

| Tính năng | Mô tả |
|---|---|
| 🧠 **Intent Recognition** | Phân biệt tư vấn sản phẩm vs. trò chuyện thông thường |
| 🇻🇳 **NLP Tiếng Việt** | Tự động sửa typo, chuẩn hoá teen-code, hiểu ngữ cảnh |
| 🔍 **Vector Search** | ChromaDB semantic search tìm sản phẩm phù hợp nhất |
| 💡 **AI Recommendations** | Google Gemini 1.5 Flash tạo đề xuất tự nhiên, có ngữ cảnh |
| 🧵 **Context Memory** | Nhớ lịch sử hội thoại trong session, trả lời liên tục |
| 👍👎 **User Feedback** | Người dùng đánh giá câu trả lời để cải thiện chất lượng |
| 🔗 **Dify.ai Integration** | Orchestration workflow AI phức tạp |

> [!TIP]
> Chatbot có thể trả lời các câu như: *"Trái cây nào tốt cho người tiểu đường?"*, *"Có cam nhập khẩu không?thanh toán như nào?"* — kể cả khi viết không dấu hay teen-code.

---

## 🔐 Bảo Mật

| Lớp bảo mật | Giải pháp |
|---|---|
| 🔑 **Mật khẩu** | Hash với **BCrypt.Net-Next** (cost factor mặc định), không lưu plaintext |
| 🛡️ **Authentication** | Session-based auth với cookie bảo mật |
| 🚫 **CSRF Protection** | ASP.NET Core **AntiForgery** token trên toàn bộ form POST |
| 📁 **File Upload** | Kiểm tra MIME type và extension, chỉ cho phép định dạng ảnh hợp lệ |
| 👮 **Authorization** | Role-based access control — phân quyền Admin / Customer rõ ràng |
| 📧 **OTP Email** | Mã OTP có thời hạn cho luồng quên mật khẩu |
| 🔒 **Soft Delete** | Người dùng bị vô hiệu hoá (soft-delete) thay vì xoá dữ liệu thật |

---

## 📄 License

Dự án được phân phối dưới giấy phép **MIT License**.  
Xem file [LICENSE](LICENSE) để biết thêm chi tiết.

---

<div align="center">

Made with ❤️ using **ASP.NET Core 8** + **Google Gemini AI**

⭐ Nếu dự án hữu ích, hãy để lại một Star nhé!

</div>
