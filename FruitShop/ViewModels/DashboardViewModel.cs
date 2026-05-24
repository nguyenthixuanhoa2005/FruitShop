using System.Collections.Generic;
using FruitShop.Models;

namespace FruitShop.ViewModels
{
    public class DashboardViewModel
    {
        // Thống kê tổng quan (Quick view)
        public decimal TodayRevenue { get; set; }
        public int TodayOrdersCount { get; set; }
        public int TodayNewUsers { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        
        // Cảnh báo & Trạng thái
        public List<Product> LowStockProducts { get; set; } = new List<Product>();
        public Dictionary<int, int> OrderStatusSummary { get; set; } = new Dictionary<int, int>();

        // Phân tích Sản phẩm
        public List<ProductStatItem> TopSellingProducts { get; set; } = new List<ProductStatItem>(); // Bán chạy nhất (hôm nay/tất cả)
        public List<CategoryStatItem> CategoryDistribution { get; set; } = new List<CategoryStatItem>(); // Theo danh mục
        
        // Dữ liệu cũ (giữ lại nếu cần cho UI cũ)
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalUsers { get; set; }
    }

    public class ProductStatItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public int Value { get; set; } // Số lượng bán
        public string ImageUrl { get; set; }
        public decimal? Price { get; set; }
    }

    public class CategoryStatItem
    {
        public string CategoryName { get; set; }
        public int ProductCount { get; set; }
    }
}
