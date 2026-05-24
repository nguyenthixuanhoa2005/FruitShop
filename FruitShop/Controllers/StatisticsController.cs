using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FruitShop.Controllers
{
    public class StatisticsController : Controller
    {
        private readonly FruitShopContext _context;

        public StatisticsController(FruitShopContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Doanh thu theo tháng (12 tháng gần nhất)
            var last12Months = Enumerable.Range(0, 12)
                .Select(i => DateTime.Today.AddMonths(-i))
                .Select(d => new { Year = d.Year, Month = d.Month })
                .Reverse()
                .ToList();

            var revenueByMonth = await _context.Orders
                .Where(o => o.Status != 5 && o.CreatedAt.HasValue)
                .GroupBy(o => new { o.CreatedAt.Value.Year, o.CreatedAt.Value.Month })
                .Select(g => new { 
                    Year = g.Key.Year, 
                    Month = g.Key.Month, 
                    Total = g.Sum(o => o.TotalAmount ?? 0) 
                })
                .ToListAsync();

            var monthlyData = last12Months.Select(m => new {
                Label = $"{m.Month}/{m.Year}",
                Value = revenueByMonth.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month)?.Total ?? 0
            }).ToList();

            // 2. Khách hàng thân thiết (Top 5)
            var topCustomers = await _context.Orders
                .Where(o => o.Status != 5)
                .GroupBy(o => o.UserId)
                .Select(g => new {
                    UserId = g.Key,
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(o => o.TotalAmount ?? 0)
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(5)
                .Join(_context.Users, 
                    stat => stat.UserId, 
                    user => user.Id, 
                    (stat, user) => new { 
                        user.FullName, 
                        user.Email, 
                        stat.OrderCount, 
                        stat.TotalSpent 
                    })
                .ToListAsync();

            // 3. Tỷ lệ quay lại (Returning Customer Rate)
            var totalUsersWithOrders = await _context.Orders.Select(o => o.UserId).Distinct().CountAsync();
            var usersWithMultipleOrders = await _context.Orders
                .GroupBy(o => o.UserId)
                .Where(g => g.Count() > 1)
                .CountAsync();
            
            double returningRate = totalUsersWithOrders > 0 
                ? (double)usersWithMultipleOrders / totalUsersWithOrders * 100 
                : 0;

            // 4. Giá trị đơn hàng trung bình (AOV)
            var totalCompletedOrders = await _context.Orders.Where(o => o.Status == 3 || o.Status == 4).CountAsync();
            var totalCompletedRevenue = await _context.Orders
                .Where(o => o.Status == 3 || o.Status == 4)
                .SumAsync(o => o.TotalAmount ?? 0);
            
            decimal aov = totalCompletedOrders > 0 ? totalCompletedRevenue / totalCompletedOrders : 0;

            // 5. Tỉ lệ hủy đơn
            var totalOrders = await _context.Orders.CountAsync();
            var cancelledOrders = await _context.Orders.Where(o => o.Status == 5).CountAsync();
            double cancellationRate = totalOrders > 0 ? (double)cancelledOrders / totalOrders * 100 : 0;

            // 6. Top sản phẩm theo doanh thu (Không chỉ số lượng)
            var topProductsByRevenue = await _context.OrderItems
                .Include(oi => oi.Product)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new {
                    ProductName = g.First().Product.Name,
                    Revenue = g.Sum(oi => (oi.Quantity ?? 0) * (oi.UnitPrice ?? 0))
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            ViewBag.MonthlyLabels = monthlyData.Select(x => x.Label).ToList();
            ViewBag.MonthlyValues = monthlyData.Select(x => x.Value).ToList();
            ViewBag.TopCustomers = topCustomers;
            ViewBag.ReturningRate = returningRate;
            ViewBag.AOV = aov;
            ViewBag.CancellationRate = cancellationRate;
            ViewBag.TopProductsRevenue = topProductsByRevenue;

            return View();
        }
    }
}
