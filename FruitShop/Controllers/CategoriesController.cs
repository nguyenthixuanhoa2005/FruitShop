using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.ViewModels;
using FruitShop.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using System.Text;

namespace FruitShop.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly FruitShopContext _context;
        private readonly ISearchService _searchService;
        private const string INDEX_NAME = "categories";

        public CategoriesController(FruitShopContext context, ISearchService searchService)
        {
            _context = context;
            _searchService = searchService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string searchTerm = "", int? fParentId = null, byte? fStatus = null)
        {
            var query = _context.Categories.Include(c => c.Parent).AsQueryable();

            // Lọc theo từ khóa
            if (!string.IsNullOrWhiteSpace(searchTerm)) {
                var ids = await _searchService.SearchIdsAsync(INDEX_NAME, searchTerm, 500);
                if (ids.Any()) query = query.Where(c => ids.Select(int.Parse).Contains(c.Id));
                else query = query.Where(c => c.Name != null && c.Name.Contains(searchTerm));
            }

            // Lọc nâng cao
            if (fParentId.HasValue) query = query.Where(c => c.ParentId == fParentId.Value);
            if (fStatus.HasValue) query = query.Where(c => c.Status == fStatus.Value);

            var totalItems = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new CategoryList 
            { 
                Categories = data, 
                CurrentPage = page, 
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize), 
                TotalItems = totalItems,
                PageSize = pageSize
            };

            ViewBag.ParentCategories = await _context.Categories.Where(c => c.ParentId == null).Select(c => new { c.Id, c.Name }).ToListAsync();
            ViewData["SearchTerm"] = searchTerm;
            ViewData["fParentId"] = fParentId;
            ViewData["fStatus"] = fStatus;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Category category)
        {
            try {
                if (string.IsNullOrWhiteSpace(category.Name)) return Json(new { success = false, message = "Tên danh mục không được để trống" });
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                await _searchService.IndexDocumentsAsync(INDEX_NAME, new[] { new { id = category.Id, name = category.Name } });
                return Json(new { success = true, message = "Thêm mới thành công" });
            } catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> Update(Category category)
        {
            try {
                if (string.IsNullOrWhiteSpace(category.Name)) return Json(new { success = false, message = "Tên danh mục không được để trống" });
                var existing = await _context.Categories.FindAsync(category.Id);
                if (existing == null) return Json(new { success = false, message = "Không tìm thấy danh mục" });
                
                existing.Name = category.Name;
                existing.ParentId = category.ParentId;
                existing.Status = category.Status;
                
                await _context.SaveChangesAsync();
                await _searchService.IndexDocumentsAsync(INDEX_NAME, new[] { new { id = existing.Id, name = existing.Name } });
                return Json(new { success = true, message = "Cập nhật thành công" });
            } catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try {
                var category = await _context.Categories.Include(c => c.Products).Include(c => c.InverseParent).FirstOrDefaultAsync(c => c.Id == id);
                if (category == null) return Json(new { success = false, message = "Không tìm thấy danh mục" });
                
                if (category.Products.Any()) return Json(new { success = false, message = "Không thể xóa danh mục đang có sản phẩm liên kết" });
                if (category.InverseParent.Any()) return Json(new { success = false, message = "Không thể xóa danh mục đang có danh mục con" });

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                await _searchService.DeleteDocumentsAsync(INDEX_NAME, new[] { id.ToString() });
                return Json(new { success = true, message = "Xóa thành công" });
            } catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSelected([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return Json(new { success = false, message = "Vui lòng chọn ít nhất một bản ghi" });
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try {
                var categories = await _context.Categories.Include(c => c.Products).Include(c => c.InverseParent).Where(c => ids.Contains(c.Id)).ToListAsync();
                var canDelete = new List<Category>();
                var errors = new List<string>();

                foreach (var cat in categories) {
                    if (cat.Products.Any()) errors.Add($"Danh mục '{cat.Name}' đang có sản phẩm liên kết.");
                    else if (cat.InverseParent.Any()) errors.Add($"Danh mục '{cat.Name}' đang có danh mục con.");
                    else canDelete.Add(cat);
                }

                if (errors.Any()) return Json(new { success = false, message = "Một số bản ghi không thể xóa:\n" + string.Join("\n", errors) });

                _context.Categories.RemoveRange(canDelete);
                await _context.SaveChangesAsync();
                await _searchService.DeleteDocumentsAsync(INDEX_NAME, canDelete.Select(c => c.Id.ToString()));
                
                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã xóa thành công {canDelete.Count} bản ghi" });
            } catch (Exception ex) {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0) return Json(new { success = false, message = "Vui lòng chọn file Excel hoặc CSV" });
            
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".csv") {
                return Json(new { success = false, message = "Chỉ hỗ trợ định dạng .xlsx và .csv" });
            }

            var newItems = new List<Category>();
            var errors = new List<string>();
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try {
                // Lấy danh sách tên danh mục hiện có trong DB để check trùng nhanh
                var existingNames = await _context.Categories.Select(c => c.Name.ToLower().Trim()).ToListAsync();
                int rowNum = 2;

                if (extension == ".xlsx") {
                    using var workbook = new XLWorkbook(file.OpenReadStream());
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed().Skip(1); // Bỏ qua tiêu đề
                    
                    foreach (var row in rows) {
                        var name = row.Cell(1).GetValue<string>()?.Trim();
                        var parentName = row.Cell(2).GetValue<string>()?.Trim();
                        var statusVal = row.Cell(3).GetValue<string>()?.Trim();

                        await ProcessImportRow(name, parentName, statusVal, rowNum, newItems, existingNames, errors);
                        rowNum++;
                    }
                } else {
                    using var reader = new StreamReader(file.OpenReadStream());
                    var content = await reader.ReadToEndAsync();
                    var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    
                    // Bỏ qua dòng đầu (header)
                    for (int i = 1; i < lines.Length; i++) {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        var cells = line.Split(',');
                        var name = cells.Length > 0 ? cells[0].Trim().Trim('"') : null;
                        var parentName = cells.Length > 1 ? cells[1].Trim().Trim('"') : null;
                        var statusVal = cells.Length > 2 ? cells[2].Trim().Trim('"') : null;

                        await ProcessImportRow(name, parentName, statusVal, rowNum, newItems, existingNames, errors);
                        rowNum++;
                    }
                }

                if (errors.Any()) {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Nhập dữ liệu thất bại. Vui lòng sửa các lỗi sau:", errors = errors });
                }

                if (newItems.Any()) {
                    _context.Categories.AddRange(newItems);
                    await _context.SaveChangesAsync();
                    
                    // Index vào Meilisearch
                    await _searchService.IndexDocumentsAsync(INDEX_NAME, newItems.Select(c => new { id = c.Id, name = c.Name }));
                }
                
                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã nhập thành công {newItems.Count} danh mục" });
            } catch (Exception ex) {
                if (transaction != null) await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống khi xử lý file: " + ex.Message });
            }
        }

        private async Task ProcessImportRow(string? name, string? parentName, string? statusVal, int rowNum, List<Category> newItems, List<string> existingNames, List<string> errors)
        {
            if (string.IsNullOrEmpty(name)) {
                errors.Add($"Dòng {rowNum}: Tên danh mục không được để trống.");
                return;
            }

            // 1. Kiểm tra trùng tên trong DB
            if (existingNames.Contains(name.ToLower())) {
                errors.Add($"Dòng {rowNum}: Danh mục '{name}' đã tồn tại trong hệ thống.");
                return;
            }

            // 2. Kiểm tra trùng tên trong file (những bản ghi đã duyệt qua)
            if (newItems.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) {
                errors.Add($"Dòng {rowNum}: Tên danh mục '{name}' bị lặp lại trong file.");
                return;
            }

            int? parentId = null;
            if (!string.IsNullOrEmpty(parentName)) {
                // Ưu tiên tìm trong DB
                var p = await _context.Categories.FirstOrDefaultAsync(c => c.Name == parentName);
                if (p == null) {
                    // Nếu không có trong DB, kiểm tra xem có đang được thêm trong file này không
                    var pInFile = newItems.FirstOrDefault(x => x.Name.Equals(parentName, StringComparison.OrdinalIgnoreCase));
                    if (pInFile == null) {
                        errors.Add($"Dòng {rowNum}: Không tìm thấy danh mục cha '{parentName}' trong hệ thống hoặc trong file.");
                        return;
                    }
                    errors.Add($"Dòng {rowNum}: Danh mục cha '{parentName}' đang được thêm mới trong cùng file, vui lòng chia làm 2 đợt nhập hoặc nhập danh mục cha trước.");
                    return;
                }
                parentId = p.Id;
            }

            byte status = 1;
            if (!string.IsNullOrEmpty(statusVal)) {
                if (statusVal == "0" || statusVal.ToLower() == "ngừng" || statusVal.ToLower() == "inactive" || statusVal.ToLower() == "ngưng hoạt động") status = 0;
            }

            var cat = new Category { Name = name, ParentId = parentId, Status = status };
            newItems.Add(cat);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? ids)
        {
            var data = await GetExportData(ids);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Categories");

            // 1. Tiêu đề to
            ws.Cell(1, 1).Value = "DANH SÁCH DANH MỤC - FRUITSHOP";
            ws.Range(1, 1, 1, 4).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            ws.Row(1).Height = 28;

            // 2. Header bảng
            var headers = new[] { "ID", "Tên danh mục", "Danh mục cha", "Trạng thái" };
            for (int col = 1; col <= headers.Length; col++) {
                var cell = ws.Cell(2, col);
                cell.Value = headers[col - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB"); // Màu xanh blue giống sản phẩm
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // 3. Dữ liệu
            for (int i = 0; i < data.Count; i++) {
                int row = i + 3;
                ws.Cell(row, 1).Value = data[i].Id;
                ws.Cell(row, 2).Value = data[i].Name;
                ws.Cell(row, 3).Value = data[i].Parent?.Name ?? "";
                ws.Cell(row, 4).Value = data[i].Status == 1 ? "Hoạt động" : "Ngừng hoạt động";

                // Thêm border cho dữ liệu
                for (int col = 1; col <= 4; col++) {
                    ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Categories.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? ids)
        {
            var data = await GetExportData(ids);
            var sb = new StringBuilder();
            sb.AppendLine("ID,Name,Parent,Status");
            foreach (var item in data) {
                sb.AppendLine($"{item.Id},\"{item.Name}\",\"{item.Parent?.Name ?? ""}\",{(item.Status == 1 ? "Active" : "Inactive")}");
            }
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "Categories.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportJson(string? ids)
        {
            var data = await GetExportData(ids);
            var json = JsonSerializer.Serialize(data.Select(c => new { c.Id, c.Name, Parent = c.Parent?.Name, c.Status }));
            return File(Encoding.UTF8.GetBytes(json), "application/json", "Categories.json");
        }

        [HttpGet]
        public async Task<IActionResult> ExportXml(string? ids)
        {
            var data = await GetExportData(ids);
            var list = data.Select(c => new CategoryXmlModel { Id = c.Id, Name = c.Name, Parent = c.Parent?.Name, Status = c.Status == 1 ? "Active" : "Inactive" }).ToList();
            var serializer = new XmlSerializer(typeof(List<CategoryXmlModel>));
            using var sw = new StringWriter();
            serializer.Serialize(sw, list);
            return File(Encoding.UTF8.GetBytes(sw.ToString()), "application/xml", "Categories.xml");
        }

        private async Task<List<Category>> GetExportData(string? ids)
        {
            var query = _context.Categories.Include(c => c.Parent).AsQueryable();
            if (!string.IsNullOrEmpty(ids)) {
                var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                query = query.Where(c => idList.Contains(c.Id));
            }
            return await query.ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> CopyAll(string searchTerm = "", int? fParentId = null, byte? fStatus = null)
        {
            var query = _context.Categories.AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm)) {
                var ids = await _searchService.SearchIdsAsync(INDEX_NAME, searchTerm, 1000);
                if (ids.Any()) query = query.Where(c => ids.Select(int.Parse).Contains(c.Id));
                else query = query.Where(c => c.Name != null && c.Name.Contains(searchTerm));
            }
            if (fParentId.HasValue) query = query.Where(c => c.ParentId == fParentId.Value);
            if (fStatus.HasValue) query = query.Where(c => c.Status == fStatus.Value);

            var data = await query.Select(c => new { name = c.Name, parentId = c.ParentId, status = c.Status }).ToListAsync();
            return Json(new { success = true, categories = data });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsByCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return Json(new { success = false });

            var products = await _context.Products.Where(p => p.CategoryId == id)
                .Select(p => new {
                    id = p.Id,
                    name = p.Name,
                    sku = p.Sku,
                    price = p.Price,
                    imageUrl = p.ProductImages.OrderByDescending(pi => pi.IsMain).Select(pi => pi.ImageUrl).FirstOrDefault(),
                    status = p.Status
                }).ToListAsync();

            return Json(new { success = true, categoryName = category.Name, products = products });
        }

        [HttpPost]
        public async Task<IActionResult> ReindexAll() {
            var data = await _context.Categories.Select(c => new { id = c.Id, name = c.Name }).ToListAsync();
            await _searchService.IndexDocumentsAsync(INDEX_NAME, data);
            return Json(new { success = true, message = "Đã đồng bộ Meilisearch thành công" });
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sample");
            ws.Cell(1, 1).Value = "Tên danh mục";
            ws.Cell(1, 2).Value = "Tên danh mục cha";
            ws.Cell(1, 3).Value = "Trạng thái (1: Hoạt động, 0: Ngừng)";
            
            ws.Cell(2, 1).Value = "Trái cây nhập khẩu";
            ws.Cell(2, 2).Value = "";
            ws.Cell(2, 3).Value = "1";
            
            ws.Cell(3, 1).Value = "Táo Mỹ";
            ws.Cell(3, 2).Value = "Trái cây nhập khẩu";
            ws.Cell(3, 3).Value = "1";

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "CategoryImportTemplate.xlsx");
        }
    }

    public class CategoryXmlModel {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Parent { get; set; }
        public string? Status { get; set; }
    }
}
