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

          public async Task<IActionResult> Index(int? categoryId, string? sort, int page = 1, int pageSize = 12, int? minPrice = null, int? maxPrice = null, string? origin = null, string? search = null, int? relatedTo = null)
          {
               var allCategories = await _context.Categories.ToListAsync();

               var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .Include(p => p.Reviews)
                    .AsQueryable();

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
                              p.Status == 1 &&
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

               // Lọc theo từ khóa tìm kiếm (Ưu tiên MeiliSearch, fallback sang SQL LIKE)
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

               if (categoryId.HasValue)
               {
                    var selectedCategory = allCategories.FirstOrDefault(c => c.Id == categoryId.Value);
                    if (selectedCategory == null)
                    {
                         categoryId = null;
                    }
               }

               if (categoryId.HasValue)
               {
                    // Lấy category và tất cả category con
                    var categoryIds = new List<int> { categoryId.Value };

                    // Logic ánh xạ thủ công cho các danh mục Quà tặng đặc biệt (do cấu trúc DB phẳng)
                    if (categoryId.Value == 16) // Giỏ quà trái cây
                    {
                         categoryIds.AddRange(new List<int> { 11, 12, 13 });
                    }
                    else if (categoryId.Value == 17) // Hộp quà trái cây
                    {
                         categoryIds.AddRange(new List<int> { 18, 19, 20 });
                    }
                    else
                    {
                         var childCategories = allCategories.Where(c => c.ParentId == categoryId.Value).Select(c => c.Id).ToList();
                         categoryIds.AddRange(childCategories);
                         
                         // Lấy cả category con cấp 2
                         foreach (var childId in childCategories)
                         {
                              var grandchildCategories = allCategories.Where(c => c.ParentId == childId).Select(c => c.Id).ToList();
                              categoryIds.AddRange(grandchildCategories);
                         }
                    }

                    query = query.Where(p => categoryIds.Contains(p.CategoryId ?? 0));
               }

               // Lấy chuỗi danh mục cha cho Breadcrumb
               var breadcrumbCategories = new List<Category>();
               if (categoryId.HasValue)
               {
                    var tempCat = allCategories.FirstOrDefault(c => c.Id == categoryId);
                    while (tempCat != null)
                    {
                         breadcrumbCategories.Insert(0, tempCat);
                         tempCat = allCategories.FirstOrDefault(c => c.Id == tempCat.ParentId);
                    }
               }
               ViewBag.BreadcrumbCategories = breadcrumbCategories;

               // Áp dụng lọc giá
               if (minPrice.HasValue)
               {
                    query = query.Where(p => p.FinalPrice >= minPrice.Value);
               }

               if (maxPrice.HasValue)
               {
                    query = query.Where(p => p.FinalPrice <= maxPrice.Value);
               }

               // Áp dụng lọc xuất xứ
               if (!string.IsNullOrWhiteSpace(origin))
               {
                    var normalizedOrigin = origin.Trim();
                    query = query.Where(p =>
                         p.Origin != null &&
                         EF.Functions.Like(
                              EF.Functions.Collate(p.Origin.Trim(), "Latin1_General_100_CI_AI"),
                              normalizedOrigin
                         )
                    );
               }

               // Sorting (Chỉ áp dụng các kiểu sắp xếp cụ thể, nếu không thì dùng Ranking của MeiliSearch hoặc mặc định)
               if (string.IsNullOrWhiteSpace(sort))
               {
                    if (searchIdList != null && searchIdList.Any())
                    {
                         // Nếu đang tìm kiếm, tạm thời không áp dụng OrderBy ở SQL để giữ nguyên thứ tự MeiliSearch Ranking
                    }
                    else if (!categoryId.HasValue)
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
                    switch (sort)
                    {
                         case "price_asc": query = query.OrderBy(p => p.FinalPrice); break;
                         case "price_desc": query = query.OrderByDescending(p => p.FinalPrice); break;
                         case "name": query = query.OrderBy(p => p.Name); break;
                    }
               }

               var products = await query.ToListAsync();

               // Nếu có tìm kiếm từ MeiliSearch và không chọn sắp xếp cụ thể (giá, tên), hãy sắp xếp lại theo thứ tự ID của MeiliSearch
               if (searchIdList != null && searchIdList.Any() && string.IsNullOrWhiteSpace(sort))
               {
                    products = products.OrderBy(p => searchIdList.IndexOf(p.Id)).ToList();
               }

               var totalCount = products.Count;
               var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

               var pagedProducts = products
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

               var categories = await _context.Categories
                    .Where(c => c.Status == 1)
                    .ToListAsync();

               var allOrigins = await _context.Products
                    .Where(p => !string.IsNullOrWhiteSpace(p.Origin))
                    .Select(p => p.Origin!.Trim())
                    .Distinct()
                    .OrderBy(o => o)
                    .ToListAsync();

               string categoryName = ViewBag.CategoryName as string ?? "Tất cả sản phẩm";
               if (!relatedTo.HasValue && categoryId.HasValue)
               {
                    var currentCat = categories.FirstOrDefault(c => c.Id == categoryId);
                    if (currentCat != null)
                    {
                         categoryName = currentCat.Name ?? "Sản phẩm";
                    }
               }

               ViewBag.CategoryName = categoryName;
               ViewBag.CurrentCategory = categoryId;
               ViewBag.CurrentPage = page;
               ViewBag.TotalPages = totalPages;
               ViewBag.Categories = categories.Where(c => c.ParentId == null).ToList();
               ViewBag.MinPrice = minPrice;
               ViewBag.MaxPrice = maxPrice;
               ViewBag.Origin = origin;
               ViewBag.Origins = allOrigins;
               ViewBag.Sort = sort;
               ViewBag.Search = search;
               ViewBag.RelatedTo = relatedTo;

               return View("Index", pagedProducts);
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
