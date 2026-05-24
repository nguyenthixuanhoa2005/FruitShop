using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using FruitShop.Models;

namespace FruitShop.Controllers
{
    public class OrdersController : Controller
    {
        private readonly FruitShopContext _context;

        public OrdersController(FruitShopContext context)
        {
            _context = context;
        }

        // Danh sách đơn hàng của user
        public async Task<IActionResult> Index()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = int.Parse(userIdStr);

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Chi tiết đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = int.Parse(userIdStr);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(o => o.Coupon)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == id);
            ViewBag.Payment = payment;

            // Chỉ đánh dấu "đã đánh giá" theo đúng đơn hàng hiện tại.
            var reviewedProductIds = await _context.Reviews
                .Where(r => r.OrderId == id && r.UserId == userId)
                .Select(r => r.ProductId)
                .ToListAsync();
            ViewBag.ReviewedProductIds = reviewedProductIds;

            return View(order);
        }

        // Hủy đơn hàng
        public async Task<IActionResult> Cancel(int id)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = int.Parse(userIdStr);

            var order = await _context.Orders
                .Include(o => o.OrderItems) // Need to include OrderItems to access them
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            // Chỉ cho phép hủy khi trạng thái = 1 (Chờ)
            if (order.Status != 1)
            {
                TempData["Error"] = "Không thể hủy đơn hàng ở trạng thái này!";
                return RedirectToAction("Details", new { id = id });
            }

            // Rollback coupon usage if this order used one
            if (order.CouponId.HasValue && (order.DiscountAmount ?? 0) > 0)
            {
                var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == order.CouponId.Value);
                if (coupon != null && (coupon.UsedCount ?? 0) > 0)
                {
                    coupon.UsedCount = coupon.UsedCount.Value - 1;
                    _context.Coupons.Update(coupon); // Mark coupon for update
                }
            }


            // Cập nhật trạng thái thành 4 (Đã hủy)
            order.Status = 4;

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đơn hàng đã được hủy thành công.";
            return RedirectToAction("Details", new { id = id });
        }

        // Xác nhận đã nhận hàng
        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userIdStr))
            {
                return RedirectToAction("Login", "Home");
            }

            int userId = int.Parse(userIdStr);

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            // Chỉ cho phép xác nhận khi trạng thái = 2 (Đang giao)
            if (order.Status != 2)
            {
                TempData["Error"] = "Không thể xác nhận nhận hàng ở trạng thái này!";
                return RedirectToAction("Details", new { id = id });
            }

            // Cập nhật trạng thái thành 3 (Đã giao)
            order.Status = 3;

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xác nhận nhận hàng thành công! Bạn có thể đánh giá sản phẩm ngay bây giờ.";
            return RedirectToAction("Details", new { id = id });
        }

        // Gửi đánh giá sản phẩm
        [HttpPost]
        public async Task<IActionResult> SubmitReview([FromBody] ReviewRequest request)
        {
            try
            {
                if (request == null)
                {
                    return Json(new { success = false, message = "Dữ liệu đánh giá không hợp lệ" });
                }

                var userIdStr = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userIdStr))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập để đánh giá" });
                }

                int userId = int.Parse(userIdStr);
                var normalizedComment = (request.Comment ?? string.Empty).Trim();

                if (request.Rating < 1 || request.Rating > 5)
                {
                    return Json(new { success = false, message = "Số sao phải từ 1 đến 5" });
                }

                if (normalizedComment.Length > 500)
                {
                    return Json(new { success = false, message = "Nội dung đánh giá tối đa 500 ký tự" });
                }

                // Kiểm tra xem user có thực sự mua sản phẩm này trong đơn hàng này và đơn đã hoàn tất chưa (Status 3 = Đã giao)
                var hasBought = await _context.OrderItems
                    .AnyAsync(oi => oi.OrderId == request.OrderId && oi.ProductId == request.ProductId 
                                    && oi.Order.UserId == userId && oi.Order.Status == 3);

                if (!hasBought)
                {
                    return Json(new { success = false, message = "Bạn không thể đánh giá sản phẩm khi chưa nhận hàng" });
                }

                var alreadyReviewed = await _context.Reviews
                    .AnyAsync(r => r.OrderId == request.OrderId && r.ProductId == request.ProductId && r.UserId == userId);

                if (alreadyReviewed)
                {
                    return Json(new { success = false, message = "Sản phẩm này trong đơn hàng này đã được đánh giá rồi" });
                }

                var review = new Review
                {
                    OrderId = request.OrderId,
                    ProductId = request.ProductId,
                    UserId = userId,
                    Rating = request.Rating,
                    Comment = string.IsNullOrWhiteSpace(normalizedComment) ? null : normalizedComment,
                    Status = 1, // Mặc định cho hiển thị ngay
                    CreatedAt = DateTime.Now
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }

    public class ReviewRequest
    {
        public int ProductId { get; set; }
        public int OrderId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
