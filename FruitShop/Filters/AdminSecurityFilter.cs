using System;
using System.Threading.Tasks;
using FruitShop.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FruitShop.Filters
{
    /// <summary>
    /// Bảo vệ khu vực Admin: yêu cầu đăng nhập RoleId == 1, đã bật 2FA và mật khẩu chưa quá hạn 90 ngày.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminSecurityFilter : Attribute, IAsyncActionFilter
    {
        public const int PasswordExpiryDays = 90;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var userIdStr = httpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId) || userId <= 0)
            {
                context.Result = new RedirectToActionResult("Login", "Home", null);
                return;
            }

            var db = httpContext.RequestServices.GetRequiredService<FruitShopContext>();
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null && u.Status == 1);

            if (user == null)
            {
                httpContext.Session.Clear();
                context.Result = new RedirectToActionResult("Login", "Home", null);
                return;
            }

            if (user.RoleId != 1)
            {
                context.Result = new RedirectToActionResult("Index", "Home", null);
                return;
            }

            if (!user.TwoFactorEnabled)
            {
                if (context.Controller is Controller ctrl)
                {
                    ctrl.TempData["SecurityWarning"] = "Tài khoản quản trị bắt buộc phải kích hoạt xác thực 2 yếu tố (2FA) trước khi vào khu vực Admin.";
                }
                context.Result = new RedirectToActionResult("EnableTwoFactor", "Account", null);
                return;
            }

            if (!user.LastPasswordChangedAt.HasValue || user.LastPasswordChangedAt.Value.AddDays(PasswordExpiryDays) <= DateTime.UtcNow)
            {
                if (context.Controller is Controller ctrl)
                {
                    ctrl.TempData["SecurityWarning"] = $"Mật khẩu quản trị đã quá {PasswordExpiryDays} ngày chưa đổi. Vui lòng cập nhật mật khẩu mới trước khi tiếp tục.";
                }
                context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
                return;
            }

            await next();
        }
    }
}
