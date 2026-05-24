using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;

namespace FruitShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly FruitShopContext _context;

        public AccountController(FruitShopContext context)
        {
            _context = context;
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

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Home");

            return View(user);
        }

        public async Task<IActionResult> EditProfile()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Home");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Home");

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
            user.Phone = model.Phone;
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

            if (user.PasswordHash != currentPassword)
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không chính xác");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp");
                return View();
            }

            user.PasswordHash = newPassword;
            _context.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index");
        }
    }
}
