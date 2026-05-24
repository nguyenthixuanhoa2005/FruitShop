using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.ViewModels;
using FruitShop.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.IO;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace FruitShop.Controllers
{
    public class AdminProductsController : Controller
    {
        private readonly FruitShopContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly ISearchService _searchService;
        private const string INDEX_NAME = "products";

        public AdminProductsController(FruitShopContext context, Microsoft.Extensions.Configuration.IConfiguration configuration, ISearchService searchService)
        {
            _context = context;
            _searchService = searchService;
            var account = new Account(configuration["CloudinarySettings:CloudName"], configuration["CloudinarySettings:ApiKey"], configuration["CloudinarySettings:ApiSecret"]);
            _cloudinary = new Cloudinary(account);
        }

        private async Task<string> UploadToCloudinary(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;
            try 
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "FruitShop/Products",
                    DisplayName = file.FileName,
                    // Tối ưu hóa: Tự động nén và chọn định dạng tốt nhất (WebP/AVIF)
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null) throw new Exception(uploadResult.Error.Message);
                return uploadResult.SecureUrl.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("Lỗi upload Cloudinary: " + ex.Message);
            }
        }

        private async Task DeleteFromCloudinary(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || !imageUrl.Contains("cloudinary")) return;
            try
            {
                // Trích xuất PublicId từ URL Cloudinary
                var uri = new Uri(imageUrl);
                var segments = uri.AbsolutePath.Split('/');
                var uploadIndex = Array.IndexOf(segments, "upload");
                if (uploadIndex == -1) return;

                // PublicId bắt đầu sau segment version (v...)
                var startIndex = uploadIndex + 2;
                if (!segments[uploadIndex + 1].StartsWith("v")) startIndex = uploadIndex + 1;

                var publicIdWithExt = string.Join("/", segments.Skip(startIndex));
                var publicId = Path.Combine(Path.GetDirectoryName(publicIdWithExt) ?? "", Path.GetFileNameWithoutExtension(publicIdWithExt)).Replace("\\", "/");

                var deleteParams = new DeletionParams(publicId);
                await _cloudinary.DestroyAsync(deleteParams);
            }
            catch { /* Bỏ qua lỗi để không làm gián đoạn luồng chính */ }
        }

        public async Task<IActionResult> Index(
            int page = 1, 
            int pageSize = 10, 
            string searchTerm = "", 
            string sortBy = "Id", 
            string sortOrder = "asc",
            // Granular filters
            string fName = "",
            string fSku = "",
            string fOrigin = "",
            string fUnit = "",
            int? fCategoryId = null,
            int? fSupplierId = null,
            byte? fStatus = null,
            decimal? fMinPrice = null,
            decimal? fMaxPrice = null,
            decimal? fMinDiscountPercent = null,
            decimal? fMaxDiscountPercent = null,
            int? fMinQty = null,
            int? fMaxQty = null)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.ProductImages)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm)) {
                var ids = await _searchService.SearchIdsAsync(INDEX_NAME, searchTerm, 1000);
                if (ids != null && ids.Any()) {
                    var intIds = ids.Select(int.Parse).ToList();
                    query = query.Where(p => intIds.Contains(p.Id));
                } else {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(p => (p.Name != null && p.Name.ToLower().Contains(searchTerm)) || 
                                           (p.Sku != null && p.Sku.ToLower().Contains(searchTerm)) ||
                                           (p.Origin != null && p.Origin.ToLower().Contains(searchTerm)));
                }
            }

            // --- Advanced Granular Filters ---
            if (!string.IsNullOrWhiteSpace(fName)) query = query.Where(p => p.Name != null && p.Name.ToLower().Contains(fName.ToLower()));
            if (!string.IsNullOrWhiteSpace(fSku)) query = query.Where(p => p.Sku != null && p.Sku.ToLower().Contains(fSku.ToLower()));
            if (!string.IsNullOrWhiteSpace(fOrigin)) query = query.Where(p => p.Origin != null && p.Origin.ToLower().Contains(fOrigin.ToLower()));
            if (!string.IsNullOrWhiteSpace(fUnit)) query = query.Where(p => p.Unit != null && p.Unit.ToLower().Contains(fUnit.ToLower()));
            if (fCategoryId.HasValue) query = query.Where(p => p.CategoryId == fCategoryId.Value);
            if (fSupplierId.HasValue) query = query.Where(p => p.SupplierId == fSupplierId.Value);
            if (fStatus.HasValue) query = query.Where(p => p.Status == fStatus.Value);
            
            if (fMinPrice.HasValue) query = query.Where(p => p.Price >= fMinPrice.Value);
            if (fMaxPrice.HasValue) query = query.Where(p => p.Price <= fMaxPrice.Value);

            if (fMinDiscountPercent.HasValue) query = query.Where(p => p.DiscountPercent >= fMinDiscountPercent.Value);
            if (fMaxDiscountPercent.HasValue) query = query.Where(p => p.DiscountPercent <= fMaxDiscountPercent.Value);
            
            if (fMinQty.HasValue) query = query.Where(p => (p.StockQuantity ?? 0) >= fMinQty.Value);
            if (fMaxQty.HasValue) query = query.Where(p => (p.StockQuantity ?? 0) <= fMaxQty.Value);

            // --- Sorting ---
            query = sortBy.ToLower() switch
            {
                "code" => sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
                "name" => sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
                "price" => sortOrder == "desc" ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                "discount" => sortOrder == "desc" ? query.OrderByDescending(p => p.DiscountPercent) : query.OrderBy(p => p.DiscountPercent),
                "finalprice" => sortOrder == "desc" ? query.OrderByDescending(p => p.FinalPrice) : query.OrderBy(p => p.FinalPrice),
                "type" => sortOrder == "desc" ? query.OrderByDescending(p => p.Origin) : query.OrderBy(p => p.Origin),
                "unit" => sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.Unit) : query.OrderBy(p => p.Unit),
                "quantity" => sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.StockQuantity) : query.OrderBy(p => p.StockQuantity),
                _ => sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.Id) : query.OrderBy(p => p.Id)
            };

            int totalItems = await query.CountAsync();
            var pageData = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var viewModel = new ProductList
            {
                Products = pageData,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            // Pass filters back to view
            ViewData["SearchTerm"] = searchTerm;
            ViewData["fName"] = fName;
            ViewData["fSku"] = fSku;
            ViewData["fOrigin"] = fOrigin;
            ViewData["fUnit"] = fUnit;
            ViewData["fCategoryId"] = fCategoryId;
            ViewData["fSupplierId"] = fSupplierId;
            ViewData["fStatus"] = fStatus;
            ViewData["fMinPrice"] = fMinPrice;
            ViewData["fMaxPrice"] = fMaxPrice;
            ViewData["fMinDiscountPercent"] = fMinDiscountPercent;
            ViewData["fMaxDiscountPercent"] = fMaxDiscountPercent;
            ViewData["fMinQty"] = fMinQty;
            ViewData["fMaxQty"] = fMaxQty;

            var allCategories = await _context.Categories.ToListAsync();
            var categoryList = new List<CategoryDropdownItem>();
            BuildCategoryHierarchy(allCategories, null, 0, categoryList);
            ViewBag.Categories = categoryList;
            ViewBag.Suppliers = await _context.Suppliers.ToListAsync();

            return View(viewModel);
        }

        private void BuildCategoryHierarchy(List<Category> allCategories, int? parentId, int level, List<CategoryDropdownItem> result)
        {
            var children = allCategories.Where(c => c.ParentId == parentId).OrderBy(c => c.Name).ToList();
            foreach (var child in children)
            {
                result.Add(new CategoryDropdownItem
                {
                    Id = child.Id,
                    Name = new string('-', level * 2) + (level > 0 ? " " : "") + child.Name
                });
                BuildCategoryHierarchy(allCategories, child.Id, level + 1, result);
            }
        }

        public class CategoryDropdownItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm, string searchField = "all")
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = searchField.ToLower() switch
                {
                    "name" => query.Where(p => p.Name != null && p.Name.ToLower().Contains(searchTerm)),
                    "code" => query.Where(p => p.Sku != null && p.Sku.ToLower().Contains(searchTerm)),
                    "description" => query.Where(p => p.Description != null && p.Description.ToLower().Contains(searchTerm)),
                    "type" => query.Where(p => p.Origin != null && p.Origin.ToLower().Contains(searchTerm)),
                    _ => query.Where(p => (p.Name != null && p.Name.ToLower().Contains(searchTerm)) 
                                       || (p.Sku != null && p.Sku.ToLower().Contains(searchTerm)) 
                                       || (p.Description != null && p.Description.ToLower().Contains(searchTerm))
                                       || (p.Origin != null && p.Origin.ToLower().Contains(searchTerm)))
                };
            }

            var results = await query.Select(p => new {
                p.Id,
                p.Sku,
                p.Name,
                p.Price,
                p.DiscountPercent,
                p.FinalPrice,
                p.Origin,
                p.Unit,
                p.StockQuantity,
                ImageUrl = p.ProductImages.FirstOrDefault(i => i.IsMain == 1).ImageUrl ?? p.ProductImages.FirstOrDefault().ImageUrl,
                AllImages = p.ProductImages.Select(i => new { i.ImageUrl, i.IsMain }).ToList(),
                p.Description,
                p.CreatedAt,
                p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : ""
            }).ToListAsync();
            return Json(new { success = true, products = results });
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            var p = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (p == null) return Json(new { success = false, message = "Không tìm thấy sản phẩm" });

            return Json(new {
                success = true,
                data = new {
                    sku = p.Sku,
                    name = p.Name,
                    categoryName = p.Category?.Name ?? "—",
                    supplierName = p.Supplier?.Name ?? "—",
                    price = p.Price,
                    discountPercent = p.DiscountPercent,
                    finalPrice = p.FinalPrice,
                    stock = p.StockQuantity,
                    unit = p.Unit,
                    origin = p.Origin,
                    description = p.Description ?? "Chưa có mô tả",
                    status = p.Status,
                    createdAt = p.CreatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    images = p.ProductImages.Select(i => i.ImageUrl).ToList()
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> CopyAll(
            string searchTerm = "",
            string fName = "",
            string fSku = "",
            string fOrigin = "",
            string fUnit = "",
            int? fCategoryId = null,
            int? fSupplierId = null,
            byte? fStatus = null,
            decimal? fMinPrice = null,
            decimal? fMaxPrice = null,
            decimal? fMinDiscountPercent = null,
            decimal? fMaxDiscountPercent = null,
            int? fMinQty = null,
            int? fMaxQty = null)
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var ids = await _searchService.SearchIdsAsync(INDEX_NAME, searchTerm, 1000);
                if (ids != null && ids.Any()) {
                    var intIds = ids.Select(int.Parse).ToList();
                    query = query.Where(p => intIds.Contains(p.Id));
                } else {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(p => (p.Name != null && p.Name.ToLower().Contains(searchTerm)) || 
                                           (p.Sku != null && p.Sku.ToLower().Contains(searchTerm)) ||
                                           (p.Origin != null && p.Origin.ToLower().Contains(searchTerm)));
                }
            }

            if (!string.IsNullOrWhiteSpace(fName)) query = query.Where(p => p.Name != null && p.Name.ToLower().Contains(fName.ToLower()));
            if (!string.IsNullOrWhiteSpace(fSku)) query = query.Where(p => p.Sku != null && p.Sku.ToLower().Contains(fSku.ToLower()));
            if (!string.IsNullOrWhiteSpace(fOrigin)) query = query.Where(p => p.Origin != null && p.Origin.ToLower().Contains(fOrigin.ToLower()));
            if (!string.IsNullOrWhiteSpace(fUnit)) query = query.Where(p => p.Unit != null && p.Unit.ToLower().Contains(fUnit.ToLower()));
            if (fCategoryId.HasValue) query = query.Where(p => p.CategoryId == fCategoryId.Value);
            if (fSupplierId.HasValue) query = query.Where(p => p.SupplierId == fSupplierId.Value);
            if (fStatus.HasValue) query = query.Where(p => p.Status == fStatus.Value);
            
            if (fMinPrice.HasValue) query = query.Where(p => p.Price >= fMinPrice.Value);
            if (fMaxPrice.HasValue) query = query.Where(p => p.Price <= fMaxPrice.Value);

            if (fMinDiscountPercent.HasValue) query = query.Where(p => p.DiscountPercent >= fMinDiscountPercent.Value);
            if (fMaxDiscountPercent.HasValue) query = query.Where(p => p.DiscountPercent <= fMaxDiscountPercent.Value);
            
            if (fMinQty.HasValue) query = query.Where(p => (p.StockQuantity ?? 0) >= fMinQty.Value);
            if (fMaxQty.HasValue) query = query.Where(p => (p.StockQuantity ?? 0) <= fMaxQty.Value);

            var products = await query.Include(p => p.ProductImages).ToListAsync();
            var result = products.Select(p => new {
                sku = p.Sku,
                name = p.Name,
                categoryId = p.CategoryId,
                supplierId = p.SupplierId,
                price = p.Price,
                discountPercent = p.DiscountPercent,
                origin = p.Origin,
                unit = p.Unit,
                stock = p.StockQuantity ?? 0,
                status = p.Status,
                images = p.ProductImages.Select(i => new { ImageUrl = i.ImageUrl, IsMain = i.IsMain }).ToList(),
                desc = p.Description
            }).ToList();

            return Json(new { success = true, products = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] Product newProduct, List<IFormFile>? imageFiles, int mainImageIndex = 0)
        {
            try
            {
                newProduct.CreatedAt = DateTime.UtcNow;
                newProduct.Status = 1;

                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync();

                if (imageFiles != null && imageFiles.Count > 0)
                {
                    for (int i = 0; i < imageFiles.Count; i++)
                    {
                        var url = await UploadToCloudinary(imageFiles[i]);
                        if (!string.IsNullOrEmpty(url))
                        {
                            _context.ProductImages.Add(new ProductImage
                            {
                                ProductId = newProduct.Id,
                                ImageUrl = url,
                                IsMain = (byte)(i == mainImageIndex ? 1 : 0)
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await _searchService.IndexDocumentsAsync(INDEX_NAME, new[] { new { name = newProduct.Name, id = newProduct.Id, sku = newProduct.Sku, description = newProduct.Description } });

                return Json(new { success = true, id = newProduct.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi server: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] Product productToUpdate, List<IFormFile>? imageFiles, int mainImageIndex = -1, string? existingImagesJson = null)
        {
            try
            {
                var existingProduct = await _context.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == productToUpdate.Id);
                if (existingProduct == null)
                {
                    return NotFound(new { success = false, message = "Sản phẩm không tìm thấy để cập nhật." });
                }

                existingProduct.Name = productToUpdate.Name;
                existingProduct.Sku = productToUpdate.Sku;
                existingProduct.Price = productToUpdate.Price;
                existingProduct.DiscountPercent = productToUpdate.DiscountPercent;
                existingProduct.Origin = productToUpdate.Origin;
                existingProduct.Unit = productToUpdate.Unit;
                existingProduct.StockQuantity = productToUpdate.StockQuantity;
                existingProduct.Description = productToUpdate.Description;
                existingProduct.CategoryId = productToUpdate.CategoryId;
                existingProduct.SupplierId = productToUpdate.SupplierId;

                // Xử lý xóa ảnh không còn trong danh sách giữ lại
                var remainingUrls = new List<string>();
                if (!string.IsNullOrEmpty(existingImagesJson))
                {
                    remainingUrls = JsonSerializer.Deserialize<List<string>>(existingImagesJson) ?? new List<string>();
                }

                var toRemove = existingProduct.ProductImages.Where(pi => !remainingUrls.Contains(pi.ImageUrl ?? "")).ToList();
                foreach (var img in toRemove)
                {
                    await DeleteFromCloudinary(img.ImageUrl ?? "");
                    _context.ProductImages.Remove(img);
                }

                // Upload thêm ảnh mới
                if (imageFiles != null && imageFiles.Count > 0)
                {
                    foreach (var file in imageFiles)
                    {
                        var url = await UploadToCloudinary(file);
                        if (!string.IsNullOrEmpty(url))
                        {
                            _context.ProductImages.Add(new ProductImage
                            {
                                ProductId = existingProduct.Id,
                                ImageUrl = url,
                                IsMain = 0
                            });
                        }
                    }
                }
                
                await _context.SaveChangesAsync();

                // Cập nhật lại trạng thái ảnh chính
                var allImages = await _context.ProductImages
                    .Where(pi => pi.ProductId == existingProduct.Id)
                    .OrderBy(pi => pi.Id)
                    .ToListAsync();

                if (allImages.Any())
                {
                    foreach (var img in allImages) img.IsMain = 0;
                    
                    if (mainImageIndex >= 0 && mainImageIndex < allImages.Count)
                    {
                        allImages[mainImageIndex].IsMain = 1;
                    }
                    else
                    {
                        allImages.First().IsMain = 1;
                    }
                    await _context.SaveChangesAsync();
                }

                await _searchService.IndexDocumentsAsync(INDEX_NAME, new[] { new { id = existingProduct.Id, sku = existingProduct.Sku, name = existingProduct.Name, description = existingProduct.Description } });

                return Json(new { success = true, id = existingProduct.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi server khi đang cập nhật: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var product = await _context.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id);
                if (product == null)
                {
                    return NotFound(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                foreach (var img in product.ProductImages)
                {
                    await DeleteFromCloudinary(img.ImageUrl ?? "");
                }

                _context.ProductImages.RemoveRange(product.ProductImages);
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                await _searchService.DeleteDocumentAsync(INDEX_NAME, id.ToString());

                return Json(new { success = true, message = "Xóa sản phẩm thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi server khi đang xóa sản phẩm." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected([FromBody] int[] ids)
        {
            if (ids == null || !ids.Any())
            {
                return BadRequest(new { success = false, message = "Không có sản phẩm nào được chọn để xóa." });
            }

            try
            {
                var productsToDelete = await _context.Products.Include(p => p.ProductImages).Where(p => ids.Contains(p.Id)).ToListAsync();
                foreach (var p in productsToDelete)
                {
                    foreach (var img in p.ProductImages)
                    {
                        await DeleteFromCloudinary(img.ImageUrl ?? "");
                    }
                    _context.ProductImages.RemoveRange(p.ProductImages);
                }
                _context.Products.RemoveRange(productsToDelete);
                await _context.SaveChangesAsync();

                await _searchService.DeleteDocumentsAsync(INDEX_NAME, ids.Select(id => id.ToString()));

                return Json(new { success = true, message = $"Đã xóa thành công {productsToDelete.Count} sản phẩm." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi server khi đang xóa nhiều sản phẩm." });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Chưa chọn file để nhập." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".csv" && ext != ".xlsx")
                return BadRequest(new { success = false, message = "Chỉ hỗ trợ file .csv hoặc .xlsx." });

            var headerMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Mã sản phẩm (SKU)", "Sku" }, { "Mã SP (SKU)", "Sku" }, { "Mã sản phẩm", "Sku" }, { "SKU", "Sku" },
                { "Tên sản phẩm", "Name" }, { "Name", "Name" },
                { "Giá gốc (VNĐ)", "Price" }, { "Giá gốc", "Price" }, { "Giá bán", "Price" }, { "Price", "Price" },
                { "Phần trăm giảm giá (%)", "DiscountPercent" }, { "% giảm giá", "DiscountPercent" }, { "Giảm giá", "DiscountPercent" }, { "DiscountPercent", "DiscountPercent" },
                { "Xuất xứ", "Origin" }, { "Origin", "Origin" },
                { "Đơn vị tính", "Unit" }, { "Đơn vị", "Unit" }, { "Unit", "Unit" },
                { "Số lượng tồn kho", "StockQuantity" }, { "Số lượng", "StockQuantity" }, { "Tồn kho", "StockQuantity" }, { "StockQuantity", "StockQuantity" },
                { "Mô tả sản phẩm", "Description" }, { "Mô tả", "Description" }, { "Description", "Description" },
                { "URL Hình ảnh", "ImageUrl" }, { "Hình ảnh", "ImageUrl" }, { "ImageUrl", "ImageUrl" },
                { "Danh mục", "CategoryId" }, { "Loại sản phẩm", "CategoryId" }, { "Loại SP", "CategoryId" }, { "Category", "CategoryId" },
                { "Nhà cung cấp", "SupplierId" }, { "Supplier", "SupplierId" }
            };

            var newProducts = new List<Product>();
            var productImages = new List<ProductImage>();
            var errorLines  = new List<string>();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var categoryMap = (await _context.Categories
                                        .Where(c => c.Name != null)
                                        .ToListAsync())
                                        .GroupBy(c => c.Name!.ToLower())
                                        .ToDictionary(g => g.Key, g => g.First().Id);

                var supplierMap = (await _context.Suppliers
                                        .Where(s => s.Name != null)
                                        .ToListAsync())
                                        .GroupBy(s => s.Name!.ToLower())
                                        .ToDictionary(g => g.Key, g => g.First().Id);
                
                var existingSkus = await _context.Products
                                         .Where(p => p.Sku != null)
                                         .Select(p => p.Sku!.ToLower())
                                         .ToListAsync();

                string[] headers;
                List<string[]> rows;

                if (ext == ".csv")
                {
                    var allRows = new List<string[]>();
                    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    string? line;
                    char? detectedDelimiter = null;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (detectedDelimiter == null)
                        {
                            int commaCount = line.Count(c => c == ',');
                            int semiCount = line.Count(c => c == ';');
                            detectedDelimiter = (semiCount > commaCount) ? ';' : ',';
                        }
                        allRows.Add(ParseCsvLine(line, detectedDelimiter.Value).Select(v => v.Trim().Trim('"')).ToArray());
                    }

                    if (allRows.Count < 2) return BadRequest(new { success = false, message = "File CSV không có dữ liệu." });
                    headers = allRows[0];
                    rows    = allRows.Skip(1).ToList();
                }
                else // .xlsx
                {
                    using var workbook = new XLWorkbook(file.OpenReadStream());
                    var worksheet      = workbook.Worksheets.First();
                    int lastRow = worksheet.LastRowUsed()?.RowNumber()    ?? 1;
                    int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;
                    headers = Enumerable.Range(1, lastCol).Select(c => worksheet.Cell(1, c).GetString().Trim()).ToArray();
                    rows    = Enumerable.Range(2, Math.Max(0, lastRow - 1))
                                        .Where(r => !worksheet.Row(r).IsEmpty())
                                        .Select(r => Enumerable.Range(1, lastCol).Select(c => worksheet.Cell(r, c).GetString().Trim()).ToArray())
                                        .ToList();
                }

                var colMap = new Dictionary<int, string>();
                for (int i = 0; i < headers.Length; i++)
                {
                    var h = headers[i].Replace("\uFEFF", "");
                    if (headerMap.TryGetValue(h, out var prop))
                        colMap[i] = prop;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    var values = rows[i];
                    int lineNumber = i + 2;
                    try
                    {
                        var p = new Product { Status = 1, CreatedAt = DateTime.UtcNow };
                        string? catName = null;
                        string? supName = null;
                        string? imageUrl = null;

                        foreach (var kv in colMap)
                        {
                            if (kv.Key >= values.Length) continue;
                            var val = values[kv.Key];
                            if (string.IsNullOrWhiteSpace(val)) continue;

                            switch (kv.Value)
                            {
                                case "Sku":
                                    if (existingSkus.Contains(val.ToLower())) throw new Exception($"Mã SKU '{val}' đã tồn tại trong hệ thống.");
                                    if (newProducts.Any(np => np.Sku?.ToLower() == val.ToLower())) throw new Exception($"Mã SKU '{val}' bị trùng lặp trong file.");
                                    p.Sku = val; 
                                    break;
                                case "Name": p.Name = val; break;
                                case "Price":
                                    var rawPrice = val.Replace(".", "").Replace(",", ".").Replace("đ", "").Replace("VND", "").Trim();
                                    if (decimal.TryParse(rawPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var pr)) p.Price = pr;
                                    else throw new Exception($"Giá gốc '{val}' không hợp lệ.");
                                    break;
                                case "DiscountPercent":
                                    var rawDisc = val.Replace("%", "").Trim();
                                    if (decimal.TryParse(rawDisc, NumberStyles.Any, CultureInfo.InvariantCulture, out var dp)) p.DiscountPercent = dp;
                                    else throw new Exception($"Phần trăm giảm giá '{val}' không hợp lệ.");
                                    break;
                                case "Origin":       p.Origin      = val; break;
                                case "Unit":         p.Unit        = val; break;
                                case "StockQuantity":
                                    var rawQty = val.Replace(".", "").Replace(",", "");
                                    if (int.TryParse(rawQty, out var qty)) p.StockQuantity = qty;
                                    else throw new Exception($"Số lượng tồn '{val}' không hợp lệ.");
                                    break;
                                case "Description":  p.Description = val; break;
                                case "ImageUrl":     imageUrl      = val; break;
                                case "CategoryId":   catName       = val; break;
                                case "SupplierId":   supName       = val; break;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(p.Name)) throw new Exception("Thiếu tên sản phẩm.");
                        if (string.IsNullOrWhiteSpace(p.Sku))  throw new Exception("Thiếu mã SKU.");

                        if (!string.IsNullOrWhiteSpace(catName))
                        {
                            if (categoryMap.TryGetValue(catName.ToLower(), out var catId))
                            {
                                p.CategoryId = catId;
                            }
                            else
                            {
                                var newCat = new Category { Name = catName, Status = 1 };
                                _context.Categories.Add(newCat);
                                await _context.SaveChangesAsync();
                                categoryMap[catName.ToLower()] = newCat.Id;
                                p.CategoryId = newCat.Id;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(supName))
                        {
                            if (supplierMap.TryGetValue(supName.ToLower(), out var supId))
                            {
                                p.SupplierId = supId;
                            }
                            else
                            {
                                var newSup = new Supplier { Name = supName, Status = 1 };
                                _context.Suppliers.Add(newSup);
                                await _context.SaveChangesAsync();
                                supplierMap[supName.ToLower()] = newSup.Id;
                                p.SupplierId = newSup.Id;
                            }
                        }

                        _context.Products.Add(p);
                        await _context.SaveChangesAsync();
                        newProducts.Add(p);

                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            var img = new ProductImage { ProductId = p.Id, ImageUrl = imageUrl, IsMain = 1 };
                            _context.ProductImages.Add(img);
                            productImages.Add(img);
                        }
                    }
                    catch (Exception ex) 
                    { 
                        errorLines.Add($"Dòng {lineNumber}: {ex.Message}"); 
                    }
                }

                if (errorLines.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = $"Nhập file thất bại. Có {errorLines.Count} dòng dữ liệu không hợp lệ. Hệ thống đã hủy bỏ toàn bộ thay đổi.",
                        errors = errorLines
                    });
                }

                await _context.SaveChangesAsync();

                if (newProducts.Any())
                {
                    await _searchService.IndexDocumentsAsync(INDEX_NAME, newProducts.Select(p => new { id = p.Id, sku = p.Sku, name = p.Name }));
                }

                await transaction.CommitAsync();

                return Json(new
                {
                    success  = true,
                    imported = newProducts.Count,
                    message  = $"Nhập thành công {newProducts.Count} sản phẩm vào hệ thống."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý file: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? ids = null)
        {
            try
            {
                IQueryable<Product> query = _context.Products.Include(p => p.ProductImages).Include(p => p.Category).Include(p => p.Supplier);
                if (!string.IsNullOrEmpty(ids))
                {
                    var idList = ids.Split(',').Select(id => int.Parse(id)).ToList();
                    query = query.Where(p => idList.Contains(p.Id));
                }
                var products = await query.ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Danh sách sản phẩm");

                worksheet.Cell(1, 1).Value = "DANH SÁCH SẢN PHẨM - FRUITSHOP";
                worksheet.Range(1, 1, 1, 13).Merge();
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 14;
                worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
                worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
                worksheet.Row(1).Height = 28;

                var headers = new[] { "STT", "Mã SP (SKU)", "Tên sản phẩm", "Danh mục", "Nhà cung cấp", "Giá gốc (VNĐ)", "Giảm giá (%)", "Giá sau giảm (VNĐ)", "Xuất xứ", "Đơn vị tính", "Tồn kho", "Mô tả sản phẩm", "Link ảnh chính" };
                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = worksheet.Cell(2, col);
                    cell.Value = headers[col - 1];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                for (int i = 0; i < products.Count; i++)
                {
                    var p   = products[i];
                    int row = i + 3;
                    worksheet.Cell(row, 1).Value = i + 1;
                    worksheet.Cell(row, 2).Value = p.Sku ?? "";
                    worksheet.Cell(row, 3).Value = p.Name ?? "";
                    worksheet.Cell(row, 4).Value = p.Category?.Name ?? "";
                    worksheet.Cell(row, 5).Value = p.Supplier?.Name ?? "";
                    worksheet.Cell(row, 6).Value = (double)(p.Price ?? 0);
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                    worksheet.Cell(row, 7).Value = (double)(p.DiscountPercent ?? 0);
                    worksheet.Cell(row, 8).Value = (double)(p.FinalPrice ?? p.Price ?? 0);
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                    worksheet.Cell(row, 9).Value = p.Origin ?? "";
                    worksheet.Cell(row, 10).Value = p.Unit ?? "";
                    worksheet.Cell(row, 11).Value = p.StockQuantity ?? 0;
                    worksheet.Cell(row, 12).Value = p.Description ?? "";
                    
                    var mainImg = p.ProductImages.FirstOrDefault(img => img.IsMain == 1)?.ImageUrl ?? p.ProductImages.FirstOrDefault()?.ImageUrl;
                    if (!string.IsNullOrEmpty(mainImg))
                    {
                        worksheet.Cell(row, 13).Value = "Xem ảnh";
                        worksheet.Cell(row, 13).SetHyperlink(new XLHyperlink(mainImg));
                        worksheet.Cell(row, 13).Style.Font.FontColor = XLColor.Blue;
                        worksheet.Cell(row, 13).Style.Font.Underline = XLFontUnderlineValues.Single;
                    }
                    else worksheet.Cell(row, 13).Value = "Không có ảnh";
                }

                worksheet.Columns().AdjustToContents();
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Products.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xuất Excel: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? ids = null)
        {
            try
            {
                IQueryable<Product> query = _context.Products.Include(p => p.ProductImages).Include(p => p.Category).Include(p => p.Supplier);
                if (!string.IsNullOrEmpty(ids))
                {
                    var idList = ids.Split(',').Select(id => int.Parse(id)).ToList();
                    query = query.Where(p => idList.Contains(p.Id));
                }
                var products = await query.ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Mã sản phẩm (SKU),Tên sản phẩm,Danh mục,Nhà cung cấp,Giá gốc (VNĐ),Giảm giá (%),Xuất xứ,Đơn vị tính,Số lượng tồn kho,Mô tả sản phẩm,URL Hình ảnh chính");

                foreach (var p in products)
                {
                    var mainImg = p.ProductImages.FirstOrDefault(img => img.IsMain == 1)?.ImageUrl ?? p.ProductImages.FirstOrDefault()?.ImageUrl;
                    csv.AppendLine(string.Join(",",
                        EscapeCsvField(p.Sku ?? ""),
                        EscapeCsvField(p.Name ?? ""),
                        EscapeCsvField(p.Category?.Name ?? ""),
                        EscapeCsvField(p.Supplier?.Name ?? ""),
                        (p.Price ?? 0).ToString(CultureInfo.InvariantCulture),
                        (p.DiscountPercent ?? 0).ToString(CultureInfo.InvariantCulture),
                        EscapeCsvField(p.Origin ?? ""),
                        EscapeCsvField(p.Unit ?? ""),
                        p.StockQuantity ?? 0,
                        EscapeCsvField(p.Description ?? ""),
                        EscapeCsvField(mainImg ?? "")
                    ));
                }

                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
                return File(bytes, "text/csv; charset=utf-8", "Products.csv");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xuất CSV: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportJson(string? ids = null)
        {
            try
            {
                IQueryable<Product> query = _context.Products.Include(p => p.ProductImages).Include(p => p.Category).Include(p => p.Supplier);
                if (!string.IsNullOrEmpty(ids))
                {
                    var idList = ids.Split(',').Select(id => int.Parse(id)).ToList();
                    query = query.Where(p => idList.Contains(p.Id));
                }
                var products = await query.ToListAsync();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };
                var json = JsonSerializer.Serialize(products, options);
                return File(Encoding.UTF8.GetBytes(json), "application/json", "Products.json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xuất JSON: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportXml(string? ids = null)
        {
            try
            {
                IQueryable<Product> query = _context.Products.Include(p => p.ProductImages).Include(p => p.Category).Include(p => p.Supplier);
                if (!string.IsNullOrEmpty(ids))
                {
                    var idList = ids.Split(',').Select(id => int.Parse(id)).ToList();
                    query = query.Where(p => idList.Contains(p.Id));
                }
                var products = await query.ToListAsync();

                var xDoc = new XDocument(
                    new XElement("Products",
                        products.Select(p => new XElement("Product",
                            new XElement("Id", p.Id),
                            new XElement("Sku", p.Sku),
                            new XElement("Name", p.Name),
                            new XElement("Category", p.Category?.Name),
                            new XElement("Supplier", p.Supplier?.Name),
                            new XElement("Price", p.Price),
                            new XElement("DiscountPercent", p.DiscountPercent),
                            new XElement("FinalPrice", p.FinalPrice),
                            new XElement("Origin", p.Origin),
                            new XElement("Unit", p.Unit),
                            new XElement("StockQuantity", p.StockQuantity),
                            new XElement("Description", p.Description),
                            new XElement("CreatedAt", p.CreatedAt),
                            new XElement("Images", p.ProductImages.Select(img => new XElement("Image",
                                new XElement("Url", img.ImageUrl),
                                new XElement("IsMain", img.IsMain)
                            )))
                        ))
                    )
                );

                return File(Encoding.UTF8.GetBytes(xDoc.ToString()), "application/xml", "Products.xml");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xuất XML: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReindexAll() {
            var data = await _context.Products.Select(p => new { id = p.Id, sku = p.Sku, name = p.Name }).ToListAsync();
            await _searchService.IndexDocumentsAsync(INDEX_NAME, data);
            return Json(new { success = true, message = "Đã đồng bộ Meilisearch thành công" });
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Mã sản phẩm (SKU),Tên sản phẩm,Danh mục,Nhà cung cấp,Giá gốc (VNĐ),Phần trăm giảm giá (%),Xuất xứ,Đơn vị tính,Số lượng tồn kho,Mô tả sản phẩm,URL Hình ảnh");
            csv.AppendLine("SP_NEW_01,Táo Fuji Nhật Bản,Trái cây nhập khẩu,Fruit Supplier Inc,150000,10,Nhật Bản,Kg,100,Táo tươi nhập khẩu nguyên hộp,");
            csv.AppendLine("SP_NEW_02,Xoài Cát Hòa Lộc,Trái cây Việt Nam,Vietnam Agro,85000,,Việt Nam,Kg,200,Xoài ngọt đặc sản miền Tây,");

            var preamble = Encoding.UTF8.GetPreamble();
            var content = Encoding.UTF8.GetBytes(csv.ToString());
            var fileBytes = preamble.Concat(content).ToArray();

            return File(fileBytes, "text/csv; charset=utf-8", "mau_nhap_san_pham.csv");
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private string[] ParseCsvLine(string line, char delimiter = ',')
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool insideQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (c == delimiter && !insideQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields.ToArray();
        }
    }
}
