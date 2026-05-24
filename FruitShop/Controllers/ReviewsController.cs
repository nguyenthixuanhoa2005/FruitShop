using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FruitShop.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly FruitShopContext _context;

        public ReviewsController(FruitShopContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchTerm = "", int? rating = null, byte? status = null, int page = 1, int pageSize = 10)
        {
            var query = _context.Reviews
                .Include(r => r.Product)
                .ThenInclude(p => p.ProductImages)
                .Include(r => r.User)
                .Include(r => r.Order)
                .Where(r => r.DeletedAt == null)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(r => r.Product.Name.ToLower().Contains(searchTerm) || 
                                       r.User.FullName.ToLower().Contains(searchTerm) || 
                                       r.Comment.ToLower().Contains(searchTerm));
            }

            if (rating.HasValue) query = query.Where(r => r.Rating == rating.Value);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            int totalItems = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Statistics
            ViewBag.TotalReviews = await _context.Reviews.CountAsync(r => r.DeletedAt == null);
            ViewBag.AverageRating = await _context.Reviews.Where(r => r.DeletedAt == null).AverageAsync(r => r.Rating) ?? 0;
            
            ViewData["SearchTerm"] = searchTerm;
            ViewData["Rating"] = rating;
            ViewData["Status"] = status;
            ViewData["CurrentPage"] = page;
            ViewData["PageSize"] = pageSize;
            ViewData["TotalPages"] = (int)Math.Ceiling(totalItems / (double)pageSize);

            return View(reviews);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, byte status)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                review.Status = status;
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật trạng thái thành công." });
            }
            return Json(new { success = false, message = "Không tìm thấy đánh giá." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            review.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã ẩn đánh giá khỏi hệ thống." });
        }
    }
}
