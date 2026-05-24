using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FruitShop.Controllers
{
    public class DashboardController : Controller
    {
        private readonly FruitShopContext _context;
        private readonly Services.MeiliSearchSyncService _syncService;

        public DashboardController(FruitShopContext context, Services.MeiliSearchSyncService syncService)
        {
            _context = context;
            _syncService = syncService;
        }

        public async Task<IActionResult> Index(DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;
            var nextDate = targetDate.AddDays(1);
            var viewModel = new DashboardViewModel();
            
            ViewData["TargetDate"] = targetDate.ToString("yyyy-MM-dd");
            ViewData["DisplayDate"] = targetDate.ToString("dd/MM/yyyy");

            // 1. Thống kê Quick View (Theo ngày đã chọn)
            viewModel.TodayRevenue = await _context.Orders
                .Where(o => o.CreatedAt >= targetDate && o.CreatedAt < nextDate && o.Status != 5)
                .SumAsync(o => o.TotalAmount ?? 0);

            viewModel.TodayOrdersCount = await _context.Orders
                .CountAsync(o => o.CreatedAt >= targetDate && o.CreatedAt < nextDate);

            viewModel.TodayNewUsers = await _context.Users
                .CountAsync(u => u.CreatedAt >= targetDate && u.CreatedAt < nextDate);

            // 2. Thống kê tổng quan (Vẫn giữ nguyên tổng tất cả thời gian)
            viewModel.TotalOrders = await _context.Orders.CountAsync();
            viewModel.TotalRevenue = await _context.Orders
                .Where(o => o.Status != 5)
                .SumAsync(o => o.TotalAmount ?? 0);
            viewModel.TotalProducts = await _context.Products.CountAsync();
            viewModel.TotalCategories = await _context.Categories.CountAsync();
            viewModel.TotalUsers = await _context.Users.CountAsync();

            // 3. Cảnh báo hàng sắp hết (Tồn kho < 10)
            viewModel.LowStockProducts = await _context.Products
                .Where(p => p.StockQuantity < 10 && p.Status == 1)
                .OrderBy(p => (int)p.StockQuantity)
                .Take(5)
                .ToListAsync();

            // 4. Trạng thái đơn hàng
            var orderStatuses = await _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key ?? 0, Count = g.Count() })
                .ToListAsync();
            
            viewModel.OrderStatusSummary = orderStatuses.ToDictionary(x => (int)x.Status, x => x.Count);

            // 5. Sản phẩm bán chạy (Top 5)
            viewModel.TopSellingProducts = await _context.OrderItems
                .Include(oi => oi.Product)
                .ThenInclude(p => p!.ProductImages)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new ProductStatItem
                {
                    ProductId = g.Key ?? 0,
                    Name = g.First().Product != null ? g.First().Product!.Name : "N/A",
                    Value = g.Sum(oi => oi.Quantity ?? 0),
                    Price = g.First().Product != null ? g.First().Product!.Price : 0,
                    ImageUrl = g.First().Product != null && g.First().Product!.ProductImages.Any()
                               ? (g.First().Product!.ProductImages.FirstOrDefault(i => i.IsMain == 1) != null 
                                  ? g.First().Product!.ProductImages.FirstOrDefault(i => i.IsMain == 1)!.ImageUrl 
                                  : g.First().Product!.ProductImages.First().ImageUrl)
                               : ""
                })
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToListAsync();

            // 6. Phân bổ danh mục
            viewModel.CategoryDistribution = await _context.Categories
                .Select(c => new CategoryStatItem
                {
                    CategoryName = c.Name ?? "N/A",
                    ProductCount = c.Products.Count()
                })
                .ToListAsync();

            // 7. Hoạt động gần đây
            ViewBag.RecentActivities = await GetRecentActivities();

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SyncAll()
        {
            try
            {
                await _syncService.SyncAllAsync();
                return Json(new { success = true, message = "Đã đồng bộ hóa toàn bộ dữ liệu tìm kiếm thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi đồng bộ hóa: " + ex.Message });
            }
        }

        private async Task<List<ActivityItem>> GetRecentActivities()
        {
            var activities = new List<ActivityItem>();

            var recentOrders = await _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new ActivityItem {
                    Title = $"Đơn hàng #{o.Id} - {o.ReceiverName} - {(o.TotalAmount ?? 0).ToString("N0")}đ",
                    Time = o.CreatedAt ?? DateTime.Now,
                    Type = "order"
                }).ToListAsync();
            activities.AddRange(recentOrders);

            return activities.OrderByDescending(a => a.Time).Take(5).ToList();
        }
    }

    // Định nghĩa class thiếu
    public class ActivityItem {
        public string Title { get; set; } = "";
        public DateTime Time { get; set; }
        public string Type { get; set; } = "";
    }
}
