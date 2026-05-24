using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using FruitShop.Models;

namespace FruitShop.Controllers
{
     public class ProductsController : Controller
     {
          private const string AllProductsSeedSessionKey = "AllProductsRandomSeed";
          private readonly FruitShopContext _context;
          private readonly FruitShop.Interfaces.ISearchService _searchService;
          private const string INDEX_NAME = "products";

          public ProductsController(FruitShopContext context, FruitShop.Interfaces.ISearchService searchService)
          {
               _context = context;
               _searchService = searchService;
          }

          public async Task<IActionResult> Index([FromQuery] int[] categoryIds, [FromQuery] string[] priceRanges, [FromQuery] string[] origins, string? sort, string? search, int? relatedTo, int page = 1)
          {
               int pageSize = 12;
               var allCategories = await _context.Categories.Where(c => c.Status == 1).ToListAsync();

               var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.Reviews)
                    .Where(p => p.Status == 1);

               if (relatedTo.HasValue)
               {
                    var baseProduct = await _context.Products
                         .AsNoTracking()
                         .FirstOrDefaultAsync(p => p.Id == relatedTo.Value);

                    if (baseProduct != null)
                    {
                         var baseOrigin = baseProduct.Origin?.Trim();
                         query = query.Where(p =>
                              p.Id != baseProduct.Id &&
                              p.StockQuantity > 0 &&
                              (
                                   (baseProduct.CategoryId.HasValue && p.CategoryId == baseProduct.CategoryId) ||
                                   (!string.IsNullOrWhiteSpace(baseOrigin) &&
                                        p.Origin != null &&
                                        p.Origin.Trim() == baseOrigin)
                              ));

                         ViewBag.CategoryName = "Sản phẩm liên quan";
                    }
               }

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
                              // Lọc trên tập dữ liệu hiện tại
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

               // 4. Lọc theo từ khóa tìm kiếm (Ưu tiên MeiliSearch, fallback sang SQL LIKE)
               List<int>? searchIdList = null;
               if (!string.IsNullOrWhiteSpace(search))
               {
                    var keyword = search.Trim().Trim('"', '\'', '“', '”');
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                         try
                         {
                              var searchIds = await _searchService.SearchIdsAsync(INDEX_NAME, keyword, 100);
                              if (searchIds != null && searchIds.Any())
                              {
                                   searchIdList = searchIds.Select(int.Parse).ToList();
                                   query = query.Where(p => searchIdList.Contains(p.Id));
                              }
                              else
                              {
                                   // Fallback sang SQL LIKE nếu MeiliSearch không có kết quả
                                   var pattern = $"%{keyword}%";
                                   query = query.Where(p =>
                                        (p.Name != null && EF.Functions.Like(EF.Functions.Collate(p.Name, "Latin1_General_100_CI_AI"), pattern)) ||
                                        (p.Description != null && EF.Functions.Like(EF.Functions.Collate(p.Description, "Latin1_General_100_CI_AI"), pattern))
                                   );
                              }
                         }
                         catch
                         {
                              // Fallback an toàn nếu Meilisearch service lỗi
                              var pattern = $"%{keyword}%";
                              query = query.Where(p =>
                                   (p.Name != null && EF.Functions.Like(EF.Functions.Collate(p.Name, "Latin1_General_100_CI_AI"), pattern)) ||
                                   (p.Description != null && EF.Functions.Like(EF.Functions.Collate(p.Description, "Latin1_General_100_CI_AI"), pattern))
                              );
                         }
                    }
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
               if (string.IsNullOrWhiteSpace(sort))
               {
                    if (searchIdList != null && searchIdList.Any())
                    {
                         // Giữ thứ tự MeiliSearch Ranking
                    }
                    else if (categoryIds == null || categoryIds.Length == 0)
                    {
                         var randomSeed = HttpContext.Session.GetInt32(AllProductsSeedSessionKey);
                         if (!randomSeed.HasValue)
                         {
                              randomSeed = Random.Shared.Next(1, 1000000);
                              HttpContext.Session.SetInt32(AllProductsSeedSessionKey, randomSeed.Value);
                         }
                         query = query.OrderBy(p => ((p.Id * 7919) + randomSeed.Value) % 104729).ThenBy(p => p.Id);
                    }
                    else
                    {
                         query = query.OrderByDescending(p => p.CreatedAt);
                    }
               }
               else
               {
                    query = sort switch
                    {
                         "price_asc" => query.OrderBy(p => p.FinalPrice),
                         "price_desc" => query.OrderByDescending(p => p.FinalPrice),
                         "name" => query.OrderBy(p => p.Name),
                         _ => query.OrderByDescending(p => p.Id)
                    };
               }

               var products = await query.ToListAsync();

               // Nếu có tìm kiếm từ MeiliSearch và không chọn sắp xếp cụ thể (giá, tên), hãy sắp xếp lại theo thứ tự ID của MeiliSearch
               if (searchIdList != null && searchIdList.Any() && string.IsNullOrWhiteSpace(sort))
               {
                    products = products.OrderBy(p => searchIdList.IndexOf(p.Id)).ToList();
               }

               int totalItems = products.Count;
               int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
               var pagedProducts = products.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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
               ViewBag.RelatedTo = relatedTo;

               // Tính toán Tiêu đề (CategoryName)
               if (ViewBag.CategoryName == null) // Nếu chưa được set bởi logic RelatedTo
               {
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
               }

               if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
               {
                    return PartialView("_ProductGridPartial", pagedProducts);
               }

               return View(pagedProducts);
          }

          private void AddCategoryAndChildren(int parentId, List<Category> allCategories, List<int> result)
          {
               if (!result.Contains(parentId)) result.Add(parentId);

               var children = allCategories.Where(c => c.ParentId == parentId).Select(c => c.Id).ToList();

               // Logic ánh xạ thủ công cho các danh mục Quà tặng đặc biệt (do cấu trúc DB phẳng)
               if (parentId == 16) children.AddRange(new[] { 11, 12, 13 });
               if (parentId == 17) children.AddRange(new[] { 18, 19, 20 });

               foreach (var childId in children.Distinct())
               {
                    AddCategoryAndChildren(childId, allCategories, result);
               }
          }

          public async Task<IActionResult> Details(int? id)
          {
               if (id == null)
               {
                    return NotFound();
               }

               var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.Reviews)
                         .ThenInclude(r => r.User)
                    .FirstOrDefaultAsync(m => m.Id == id);

               if (product == null)
               {
                    return NotFound();
               }

               // Lấy toàn bộ cây danh mục cha để làm Breadcrumb
               var allCategories = await _context.Categories.ToListAsync();
               var breadcrumbCategories = new List<Category>();
               var currentCat = allCategories.FirstOrDefault(c => c.Id == product.CategoryId);
               while (currentCat != null)
               {
                    breadcrumbCategories.Insert(0, currentCat);
                    currentCat = allCategories.FirstOrDefault(c => c.Id == currentCat.ParentId);
               }
               ViewBag.BreadcrumbCategories = breadcrumbCategories;

               const int relatedLimit = 4;
               var relatedProducts = await _context.Products
                    .Include(p => p.ProductImages)
                    .Include(p => p.Reviews)
                    .Where(p =>
                         p.Id != product.Id &&
                         p.Status == 1 &&
                         p.StockQuantity > 0 &&
                         p.CategoryId == product.CategoryId)
                    .OrderByDescending(p => p.IsFeatured)
                    .ThenByDescending(p => p.CreatedAt)
                    .Take(relatedLimit)
                    .ToListAsync();

               // Nếu cùng danh mục chưa đủ thì bổ sung theo xuất xứ để luôn có danh sách gợi ý.
               if (relatedProducts.Count < relatedLimit && !string.IsNullOrWhiteSpace(product.Origin))
               {
                    var existingIds = relatedProducts.Select(p => p.Id).Append(product.Id).ToList();
                    var originFallback = await _context.Products
                         .Include(p => p.ProductImages)
                         .Include(p => p.Reviews)
                         .Where(p =>
                              !existingIds.Contains(p.Id) &&
                              p.Status == 1 &&
                              p.StockQuantity > 0 &&
                              p.Origin != null &&
                              p.Origin.Trim() == product.Origin!.Trim())
                         .OrderByDescending(p => p.IsFeatured)
                         .ThenByDescending(p => p.CreatedAt)
                         .Take(relatedLimit - relatedProducts.Count)
                         .ToListAsync();

                    relatedProducts.AddRange(originFallback);
               }

               ViewBag.RelatedProducts = relatedProducts;

               return View(product);
          }
     }
}
