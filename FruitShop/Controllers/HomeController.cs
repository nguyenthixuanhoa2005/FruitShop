using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using System.Net.Mail;
using FruitShop.Models;
using FruitShop.ViewModels;

namespace FruitShop.Controllers
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

            // 4. Lấy danh sách sản phẩm bán chạy nhất dựa trên số lượng đã bán (OrderItems)
            var bestSellingProductIds = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.Status != 5) // Không tính đơn đã hủy
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(oi => oi.Quantity ?? 0) })
                .OrderByDescending(x => x.TotalSold)
                .Take(12)
                .Select(x => x.ProductId)
                .ToListAsync();

            var featuredProducts = allProducts
                .Where(p => bestSellingProductIds.Contains(p.Id))
                .OrderBy(p => bestSellingProductIds.IndexOf(p.Id))
                .ToList();

            // Nếu không đủ sản phẩm bán chạy, lấy thêm sản phẩm mới nhất
            if (featuredProducts.Count < 12)
            {
                var additionalProducts = allProducts
                    .Where(p => !bestSellingProductIds.Contains(p.Id))
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(12 - featuredProducts.Count)
                    .ToList();
                featuredProducts.AddRange(additionalProducts);
            }

            var viewModel = new HomeViewModel
            {
                Categories = categories.Where(c => c.Status == 1 && c.ParentId == null).ToList(),
                
                // Hiển thị sản phẩm bán chạy thực tế
                FeaturedProducts = featuredProducts,
                
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
                    .Take(8)
                    .ToList()
            };
            return View(viewModel);
        }

        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
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
            HttpContext.Session.SetString("UserRole", user.RoleId?.ToString() ?? "2");

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

            // Redirect dựa trên RoleId (Giả sử 1 là Admin, 2 là User)
            if (user.RoleId == 1)
            {
                return RedirectToAction("Index", "AdminHome");
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Products");
        }

        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                model.ErrorMessage = "Vui lòng kiểm tra lại thông tin: " + string.Join(", ", errors);
                return View(model);
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (existingUser != null)
                {
                    model.ErrorMessage = "Email này đã được sử dụng";
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
                model.ErrorMessage = "Lỗi hệ thống: " + error;
                return View(model);
            }
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
            
            user.ResetToken = otp;
            user.ResetTokenExpiry = DateTime.Now.AddMinutes(5);
            
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

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(smtpPassword))
                {
                    ViewBag.Error = "Thiếu cấu hình email SMTP. Hãy cấu hình EmailSettings trong appsettings.json.";
                    return View();
                }

                using var message = new MailMessage();
                message.From = new MailAddress(smtpFrom, smtpFromName);
                message.To.Add(Email);
                message.Subject = "Mã OTP khôi phục mật khẩu";
                message.Body = $"Xin chào,\n\nMã OTP của bạn là: {otp}\nMã này có hiệu lực trong 5 phút.\n\nNếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new System.Net.NetworkCredential(smtpUser, smtpPassword),
                    EnableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var enableSsl) && enableSsl,
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
                ViewBag.Error = "Mật khẩu xác nhận không khớp";
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                user.PasswordHash = NewPassword; // TODO: Hash password properly
                
                // Xóa mã OTP sau khi dùng xong
                user.ResetToken = null;
                user.ResetTokenExpiry = null;
                
                await _context.SaveChangesAsync();
                
                // Xóa email khỏi session
                HttpContext.Session.Remove("ResetEmail");
                
                TempData["SuccessMessage"] = "Mật khẩu của bạn đã được cập nhật thành công.";
                return RedirectToAction("Login");
            }

            ViewBag.Error = "Có lỗi xảy ra, vui lòng thử lại.";
            return View();
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
