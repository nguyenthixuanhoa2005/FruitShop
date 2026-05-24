using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Web_hoaqua.Models;

namespace Web_hoaqua.Controllers
{
    public class HomeController : Controller
    {
        private readonly FruitShopContext _context;
        private readonly IConfiguration _configuration;

        public HomeController(FruitShopContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Lấy tất cả danh mục để xử lý phân cấp trong bộ nhớ
            var categories = await _context.Categories.ToListAsync();
            
            // 2. Xác định danh sách ID con cho từng loại
            var importedCategoryIds = categories.Where(c => c.Id == 1 || c.ParentId == 1).Select(c => c.Id).ToList();
            var localCategoryIds = categories.Where(c => c.Id == 2 || c.ParentId == 2).Select(c => c.Id).ToList();
            var giftCategoryIds = categories.Where(c => c.Id == 3 || c.ParentId == 3 || (c.ParentId != null && categories.Any(p => p.Id == c.ParentId && p.ParentId == 3))).Select(c => c.Id).ToList();

            // 3. Lấy tất cả sản phẩm đang kinh doanh
            var allProducts = await _context.Products
                .Include(p => p.Reviews)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == 1)
                .ToListAsync();

            var viewModel = new HomeViewModel
            {
                Categories = categories.Where(c => c.Status == 1 && c.ParentId == null).ToList(),
                
                // Ưu tiên dữ liệu sản phẩm nổi bật từ DB (is_featured = 1).
                // Nếu chưa đủ 12 sản phẩm, bổ sung thêm sản phẩm đang kinh doanh khác.
                FeaturedProducts = allProducts
                    .Where(p => p.IsFeatured == 1)
                    .Take(12)
                    .Concat(
                        allProducts
                            .Where(p => p.IsFeatured != 1)
                            .Take(Math.Max(0, 12 - allProducts.Count(p => p.IsFeatured == 1)))
                    )
                    .ToList(),
                
                // Lấy đầy đủ sản phẩm nhập khẩu (bao gồm táo, nho, cherry...)
                ImportedFruits = allProducts
                    .Where(p => importedCategoryIds.Contains(p.CategoryId ?? 0))
                    .Take(8)
                    .ToList(),

                // Lấy đầy đủ sản phẩm nội địa
                LocalFruits = allProducts
                    .Where(p => localCategoryIds.Contains(p.CategoryId ?? 0))
                    .Take(8)
                    .ToList(),

                // Lấy đầy đủ quà tặng (bao gồm giỏ và hộp)
                GiftFruits = allProducts
                    .Where(p => giftCategoryIds.Contains(p.CategoryId ?? 0))
                    .OrderByDescending(p => p.IsFeatured)
                    .ThenByDescending(p => p.CreatedAt)
                    .Take(8)
                    .ToList()
            };
            return View(viewModel);
        }

        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string Email)
        {
            ViewBag.Email = Email;

            if (string.IsNullOrEmpty(Email))
            {
                ViewBag.Error = "Vui lòng nhập Email";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);
            if (user == null)
            {
                ViewBag.Error = "Email không tồn tại trong hệ thống";
                return View();
            }

            // Sinh mã OTP 6 số
            string otp = new Random().Next(100000, 999999).ToString();

            // Lưu vào DB
            user.ResetToken = otp;
            user.ResetTokenExpiry = DateTime.Now.AddMinutes(5);

            _context.Update(user);
            await _context.SaveChangesAsync();

            // Lưu Email vào Session để dùng cho bước xác thực tiếp theo
            HttpContext.Session.SetString("ResetEmail", Email);

            try
            {
                var smtpHost = _configuration["EmailSettings:SmtpHost"];
                var smtpPort = int.TryParse(_configuration["EmailSettings:SmtpPort"], out var port) ? port : 587;
                var smtpUser = _configuration["EmailSettings:Username"];
                var smtpPassword = _configuration["EmailSettings:Password"];
                var smtpFrom = _configuration["EmailSettings:FromEmail"] ?? smtpUser;
                var smtpFromName = _configuration["EmailSettings:FromName"] ?? "Fruit Shop";

                if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPassword) || string.IsNullOrWhiteSpace(smtpFrom))
                {
                    ViewBag.Error = "Thiếu cấu hình email SMTP. Hãy cấu hình EmailSettings trong appsettings.json.";
                    return View();
                }

                using var message = new MailMessage();
                message.From = new MailAddress(smtpFrom, smtpFromName);
                message.To.Add(Email);
                message.Subject = "Mã OTP khôi phục mật khẩu";
                message.Body = $"Xin chào,\n\nMã OTP của bạn là: {otp}\nMã này có hiệu lực trong 5 phút.\n\nNếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.";
                message.IsBodyHtml = false;

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var enableSsl) && enableSsl,
                    Credentials = new NetworkCredential(smtpUser, smtpPassword)
                };

                await client.SendMailAsync(message);
                TempData["SuccessMessage"] = "Đã gửi mã OTP đến email của bạn.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Send OTP email failed: {ex.Message}");
                ViewBag.Error = "Không gửi được email OTP. Kiểm tra lại cấu hình SMTP Gmail hoặc app password.";
                return View();
            }

            return RedirectToAction("VerifyOTP");
        }

        public IActionResult VerifyOTP()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("ResetEmail")))
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOTP(string OTP)
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null && user.ResetToken == OTP && user.ResetTokenExpiry > DateTime.Now)
            {
                // OTP hợp lệ
                return RedirectToAction("ResetPassword");
            }

            // OTP không hợp lệ hoặc hết hạn
            ViewBag.Error = "Mã OTP không chính xác hoặc đã hết hạn";
            return View();
        }

        public IActionResult ResetPassword()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("ResetEmail")))
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string NewPassword, string ConfirmPassword)
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("ForgotPassword");
            }

            if (NewPassword != ConfirmPassword)
            {
                ViewBag.Error = "Mật khẩu không khớp";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                // Cập nhật mật khẩu (Lưu ý: Trong thực tế cần Hash mật khẩu)
                user.PasswordHash = NewPassword;
                
                // Xóa mã OTP sau khi dùng xong
                user.ResetToken = null;
                user.ResetTokenExpiry = null;

                _context.Update(user);
                await _context.SaveChangesAsync();

                // Xóa email khỏi session
                HttpContext.Session.Remove("ResetEmail");

                TempData["SuccessMessage"] = "Mật khẩu đã được cập nhật thành công!";
                return RedirectToAction("Login");
            }

            ViewBag.Error = "Có lỗi xảy ra, vui lòng thử lại";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email && u.Status == 1);

            if (user == null)
            {
                model.ErrorMessage = "Email hoặc mật khẩu không chính xác";
                return View(model);
            }

            // TODO: thực hiện xác thực mật khẩu đúng cách (ví dụ: sử dụng hashing và salting)
            // Temp: so sánh trực tiếp (KHÔNG AN TOÀN, chỉ để demo)
            if (user.PasswordHash != model.Password)
            {
                model.ErrorMessage = "Email hoặc mật khẩu không chính xác";
                return View(model);
            }

            // TODO: Implement proper authentication/session
            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.FullName);

            // Load giỏ hàng từ database vào Session
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == user.Id && c.Status == 1)
                .ToListAsync();
            
            if (cartItems.Count > 0)
            {
                var cart = new Dictionary<int, int>();
                foreach (var item in cartItems)
                {
                    cart[item.ProductId ?? 0] = item.Quantity ?? 0;
                }
                HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));
            }
            else
            {
                HttpContext.Session.Remove("Cart");
            }

            // Nếu có sản phẩm chờ thêm vào giỏ, quay lại trang sản phẩm
            // Nếu không, quay lại trang chủ
            return RedirectToAction("Index", "Products");
        }

        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model == null) return View(new RegisterViewModel { ErrorMessage = "Dữ liệu không hợp lệ" });

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp");
                return View(model);
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (existingUser != null)
                {
                    ModelState.AddModelError("", "Email này đã được sử dụng");
                    return View(model);
                }

                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    PasswordHash = model.Password, // TODO: Hash password properly
                    RoleId = 2, // User role
                    Status = 1,
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                if (ex.InnerException != null) error += " - " + ex.InnerException.Message;
                ModelState.AddModelError("", "Lỗi hệ thống: " + error);
                return View(model);
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Products");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
