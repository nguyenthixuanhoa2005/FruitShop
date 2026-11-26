using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.Services;

namespace FruitShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly FruitShopContext _context;
        private readonly PasswordPolicyService _passwordPolicy;
        private readonly TotpService _totpService;
        private readonly SensitiveDataService _sensitiveData;

        public AccountController(FruitShopContext context, PasswordPolicyService passwordPolicy, TotpService totpService, SensitiveDataService sensitiveData)
        {
            _context = context;
            _passwordPolicy = passwordPolicy;
            _totpService = totpService;
            _sensitiveData = sensitiveData;
        }

        private int GetUserId()
        {
            var idStr = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var id)) return id;
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Home");

            user.Phone = _sensitiveData.Unprotect(user.Phone);
            return View(user);
        }

        public async Task<IActionResult> EditProfile()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Home");

            user.Phone = _sensitiveData.Unprotect(user.Phone);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(User model)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Home");

            user.FullName = model.FullName;
            user.Phone = _sensitiveData.Protect(model.Phone);
            // Cập nhật các trường khác nếu cần

            _context.Update(user);
            await _context.SaveChangesAsync();

            // Cập nhật lại UserName trong Session nếu nó thay đổi
            HttpContext.Session.SetString("UserName", user.FullName ?? "");

            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Index");
        }

        public IActionResult ChangePassword()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Home");

            if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không chính xác");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp");
                return View();
            }

            if (!_passwordPolicy.ValidateStrength(newPassword, out var passwordError, isAdmin: user.RoleId == 1))
            {
                ModelState.AddModelError("", passwordError);
                return View();
            }

            if (await _passwordPolicy.WasUsedBeforeAsync(user.Id, newPassword, user.PasswordHash))
            {
                ModelState.AddModelError("", $"Không được sử dụng lại {PasswordPolicyService.HistoryLimit} mật khẩu gần nhất.");
                return View();
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordHash = newHash;
            user.LastPasswordChangedAt = DateTime.UtcNow;
            _context.Update(user);
            await _passwordPolicy.RecordPasswordAsync(user.Id, newHash);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            if (user.RoleId == 1)
            {
                return RedirectToAction("Index", "AdminHome");
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> EnableTwoFactor()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
            if (user == null) return RedirectToAction("Login", "Home");

            ViewBag.TwoFactorEnabled = user.TwoFactorEnabled;
            ViewBag.IsAdmin = user.RoleId == 1;

            if (!user.TwoFactorEnabled)
            {
                var secret = HttpContext.Session.GetString("Pending2FASetupSecret");
                if (string.IsNullOrEmpty(secret))
                {
                    secret = _totpService.GenerateSecretKey();
                    HttpContext.Session.SetString("Pending2FASetupSecret", secret);
                }

                var uri = _totpService.BuildOtpAuthUri(user.Email ?? user.FullName ?? "user", secret);
                ViewBag.SecretKey = secret;
                ViewBag.QrCodeDataUri = _totpService.GenerateQrCodeDataUri(uri);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactor(string code)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
            if (user == null) return RedirectToAction("Login", "Home");

            var secret = HttpContext.Session.GetString("Pending2FASetupSecret");
            if (string.IsNullOrEmpty(secret))
            {
                return RedirectToAction("EnableTwoFactor");
            }

            if (!_totpService.VerifyCode(secret, code))
            {
                ViewBag.Error = "Mã xác thực 6 số không đúng hoặc đã hết hạn. Vui lòng thử lại.";
                ViewBag.TwoFactorEnabled = false;
                ViewBag.IsAdmin = user.RoleId == 1;
                var uri = _totpService.BuildOtpAuthUri(user.Email ?? user.FullName ?? "user", secret);
                ViewBag.SecretKey = secret;
                ViewBag.QrCodeDataUri = _totpService.GenerateQrCodeDataUri(uri);
                return View();
            }

            user.TwoFactorSecret = secret;
            user.TwoFactorEnabled = true;
            _context.Update(user);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove("Pending2FASetupSecret");

            TempData["SuccessMessage"] = "Đã kích hoạt xác thực 2 yếu tố (2FA) thành công!";
            if (user.RoleId == 1)
            {
                return RedirectToAction("Index", "AdminHome");
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
            if (user == null) return RedirectToAction("Login", "Home");

            if (user.RoleId == 1)
            {
                TempData["SecurityWarning"] = "Tài khoản quản trị bắt buộc phải bật xác thực 2 yếu tố (2FA).";
                return RedirectToAction("EnableTwoFactor");
            }

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            _context.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã tắt xác thực 2 yếu tố (2FA).";
            return RedirectToAction("EnableTwoFactor");
        }
    }
}
