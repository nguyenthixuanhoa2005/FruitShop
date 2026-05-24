using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_hoaqua.Models;

namespace Web.Controllers
{
    public class ProductsController : Controller
    {
        private readonly FruitShopContext _context;

        public ProductsController(FruitShopContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index([FromQuery] int[] categoryIds, [FromQuery] string[] priceRanges, [FromQuery] string[] origins, string? sort, string? search, int? relatedTo, int page = 1)
        {
            int pageSize = 9;
            var allCategories = await _context.Categories.Where(c => c.Status == 1).ToListAsync();
            
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Reviews)
                .Where(p => p.Status == 1);

            // 1. Lọc theo Danh mục
            if (categoryIds != null && categoryIds.Length > 0)
            {
                var targetCategoryIds = new List<int>();
                foreach (var id in categoryIds)
                {
                    AddCategoryAndChildren(id, allCategories, targetCategoryIds);
                }
                var distinctTargetIds = targetCategoryIds.Distinct().ToList();
                query = query.Where(p => p.CategoryId.HasValue && distinctTargetIds.Contains(p.CategoryId.Value));
            }

            // 2. Lọc theo Giá
            if (priceRanges != null && priceRanges.Length > 0)
            {
                var priceFilterIds = new List<int>();
                bool appliedPriceFilter = false;
                
                foreach (var range in priceRanges)
                {
                    var parts = range.Split('-');
                    if (parts.Length == 2 && decimal.TryParse(parts[0], out decimal min) && decimal.TryParse(parts[1], out decimal max))
                    {
                        appliedPriceFilter = true;
                        // Lọc trực tiếp trên tập dữ liệu hiện tại
                        var matchIds = await query.Where(p => p.FinalPrice >= min && p.FinalPrice <= max).Select(p => p.Id).ToListAsync();
                        priceFilterIds.AddRange(matchIds);
                    }
                }

                if (appliedPriceFilter)
                {
                    var distinctIds = priceFilterIds.Distinct().ToList();
                    query = query.Where(p => distinctIds.Contains(p.Id));
                }
            }

            // 3. Lọc theo Xuất xứ
            if (origins != null && origins.Length > 0)
            {
                query = query.Where(p => p.Origin != null && origins.Contains(p.Origin.Trim()));
            }

            // 4. Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                var pattern = $"%{keyword}%";
                query = query.Where(p =>
                     (p.Name != null && EF.Functions.Like(EF.Functions.Collate(p.Name, "Latin1_General_100_CI_AI"), pattern)) ||
                     (p.Description != null && EF.Functions.Like(EF.Functions.Collate(p.Description, "Latin1_General_100_CI_AI"), pattern))
                );
            }

            // Lấy dữ liệu cho View
            var allOrigins = await _context.Products
                 .Where(p => !string.IsNullOrWhiteSpace(p.Origin))
                 .Select(p => p.Origin!.Trim())
                 .Distinct()
                 .OrderBy(o => o)
                 .ToListAsync();

            // Breadcrumb
            var breadcrumbCategories = new List<Category>();
            if (categoryIds != null && categoryIds.Length > 0)
            {
                var tempCat = allCategories.FirstOrDefault(c => c.Id == categoryIds[0]);
                while (tempCat != null)
                {
                    breadcrumbCategories.Insert(0, tempCat);
                    tempCat = allCategories.FirstOrDefault(c => c.Id == tempCat.ParentId);
                }
            }
            ViewBag.BreadcrumbCategories = breadcrumbCategories;

            // Sorting
            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.FinalPrice),
                "price_desc" => query.OrderByDescending(p => p.FinalPrice),
                "name" => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.Id)
            };

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var products = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentCategoryIds = categoryIds ?? new int[0];
            ViewBag.CurrentPriceRanges = priceRanges ?? new string[0];
            ViewBag.CurrentOrigins = origins ?? new string[0];
            ViewBag.Sort = sort;
            ViewBag.Search = search;
            ViewBag.Origins = allOrigins;
            ViewBag.AllCategories = allCategories;
            ViewBag.Categories = allCategories.Where(c => c.ParentId == null).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            // Tính toán Tiêu đề (CategoryName)
            if (categoryIds != null && categoryIds.Length == 1)
            {
                ViewBag.CategoryName = allCategories.FirstOrDefault(c => c.Id == categoryIds[0])?.Name ?? "Sản phẩm";
            }
            else if (categoryIds != null && categoryIds.Length > 1)
            {
                ViewBag.CategoryName = "Kết quả lọc";
            }
            else
            {
                ViewBag.CategoryName = "Tất cả sản phẩm";
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_ProductGridPartial", products);
            }

            return View(products);
        }

        private void AddCategoryAndChildren(int parentId, List<Category> allCategories, List<int> result)
        {
            if (!result.Contains(parentId)) result.Add(parentId);
            
            var children = allCategories.Where(c => c.ParentId == parentId).Select(c => c.Id).ToList();

            // Ép thêm quan hệ ảo cho Quà tặng (do DB đang để phẳng)
            if (parentId == 16) children.AddRange(new[] { 11, 12, 13 });
            if (parentId == 17) children.AddRange(new[] { 18, 19, 20 });

            foreach (var childId in children.Distinct())
            {
                AddCategoryAndChildren(childId, allCategories, result);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var relatedProducts = await _context.Products
                .Include(p => p.ProductImages)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.Status == 1)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;
            return View(product);
        }

        public async Task<IActionResult> Promotions(int page = 1)
        {
            int pageSize = 12;

            // 1. Lấy danh sách sản phẩm có giảm giá (> 0%)
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Reviews)
                .Where(p => p.Status == 1 && (p.DiscountPercent ?? 0) > 0)
                .OrderByDescending(p => p.DiscountPercent);

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var discountedProducts = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // 2. Lấy danh sách các mã giảm giá còn hiệu lực
            var activeCoupons = await _context.Coupons
                .Where(c => c.Status == 1 && c.EndDate >= DateTime.Now && (c.UsageLimit == null || c.UsedCount < c.UsageLimit))
                .ToListAsync();

            ViewBag.Coupons = activeCoupons;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(discountedProducts);
        }
    }
}
