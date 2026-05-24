using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FruitShop.Controllers
{
    public class InventoryController : Controller
    {
        private readonly FruitShopContext _context;
        public InventoryController(FruitShopContext context) { _context = context; }

        public async Task<IActionResult> Index(
            int page = 1, 
            int pageSize = 10, 
            string searchTerm = "", 
            string type = "")
        {
            // 1. Lấy toàn bộ logs từ CSDL
            var queryLogs = _context.InventoryLogs
                .Include(l => l.Product)
                .AsNoTracking()
                .AsQueryable();

            // 2. Áp dụng các bộ lọc cơ bản
            if (!string.IsNullOrEmpty(type)) queryLogs = queryLogs.Where(l => l.ChangeType == type);
            
            var allLogs = await queryLogs.ToListAsync();

            // 3. Gom nhóm theo Note (Mã phiếu) - Cải tiến việc tách mã và lý do
            var receipts = allLogs
                .GroupBy(l => string.IsNullOrEmpty(l.Note) ? "ID-" + l.Id : l.Note)
                .Select(g => {
                    var rawKey = g.Key;
                    string displayCode = rawKey;
                    string summary = "Giao dịch kho";
                    
                    if (rawKey.StartsWith("REF:")) {
                        // Định dạng REF:PN-20260419-123|Lý do nhập
                        var content = rawKey.Replace("REF:", "");
                        var parts = content.Split('|');
                        displayCode = parts[0];
                        if (parts.Length > 1) summary = parts[1];
                        else summary = "Giao dịch hệ thống";
                    } else if (rawKey.StartsWith("ID-")) {
                        summary = "Giao dịch lẻ";
                    }

                    return new InventoryReceiptItem
                    {
                        ReceiptCode = displayCode,
                        OriginalNote = rawKey, // Lưu lại key gốc để tìm kiếm chi tiết chính xác
                        Type = g.First().ChangeType,
                        CreatedAt = g.Min(l => l.CreatedAt) ?? DateTime.Now,
                        TotalProducts = g.Select(l => l.ProductId).Distinct().Count(),
                        TotalQuantity = g.Sum(l => l.Quantity ?? 0),
                        SummaryNote = summary,
                        ProductNames = string.Join(", ", g.Select(l => l.Product?.Name).Distinct())
                    };
                })
                .OrderByDescending(r => r.CreatedAt)
                .AsQueryable();

            // 4. Tìm kiếm nâng cao trên kết quả đã gom nhóm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                receipts = receipts.Where(r => 
                    r.ReceiptCode.ToLower().Contains(searchTerm) || 
                    r.SummaryNote.ToLower().Contains(searchTerm) ||
                    r.ProductNames.ToLower().Contains(searchTerm)
                );
            }

            int totalItems = receipts.Count();
            var pagedData = receipts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 5. Thống kê
            var now = DateTime.Now;
            var currentMonthLogs = await _context.InventoryLogs
                .Where(l => l.CreatedAt.Value.Month == now.Month && l.CreatedAt.Value.Year == now.Year)
                .AsNoTracking()
                .ToListAsync();

            var model = new InventoryViewModel
            {
                Receipts = pagedData,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                TotalItems = totalItems,
                PageSize = pageSize,
                TotalImportMonth = currentMonthLogs.Where(l => l.ChangeType == "Import").Sum(l => l.Quantity ?? 0),
                TotalExportMonth = currentMonthLogs.Where(l => l.ChangeType == "Export").Sum(l => l.Quantity ?? 0),
                LowStockAlertCount = await _context.Products.CountAsync(p => (p.StockQuantity ?? 0) <= 10)
            };

            ViewData["SearchTerm"] = searchTerm;
            ViewData["Type"] = type;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(string code, string originalNote = "")
        {
            IQueryable<InventoryLog> query;

            // Ưu tiên tìm theo originalNote (key đã dùng để Group)
            if (!string.IsNullOrEmpty(originalNote))
            {
                query = _context.InventoryLogs.Where(l => l.Note == originalNote);
                
                // Nếu là ID-..., tìm theo ID
                if (originalNote.StartsWith("ID-"))
                {
                    if (int.TryParse(originalNote.Replace("ID-", ""), out int id))
                    {
                        query = _context.InventoryLogs.Where(l => l.Id == id);
                    }
                }
            }
            else
            {
                // Fallback nếu chỉ có code (từ các bản ghi cũ hoặc link trực tiếp)
                if (code.StartsWith("ID-"))
                {
                    int id = int.Parse(code.Replace("ID-", ""));
                    query = _context.InventoryLogs.Where(l => l.Id == id);
                }
                else
                {
                    // Tìm cả REF:CODE và REF:CODE|...
                    var refCodePrefix = "REF:" + code;
                    query = _context.InventoryLogs.Where(l => l.Note == code || l.Note.StartsWith(refCodePrefix));
                }
            }

            var logs = await query
                .Include(l => l.Product)
                .ThenInclude(p => p.ProductImages)
                .ToListAsync();

            if (!logs.Any()) return Json(new { success = false, message = "Không tìm thấy thông tin chi tiết của phiếu này" });

            var first = logs.First();
            string reason = "Giao dịch kho";
            string displayCode = code;

            if (!string.IsNullOrEmpty(first.Note) && first.Note.StartsWith("REF:"))
            {
                var content = first.Note.Replace("REF:", "");
                var parts = content.Split('|');
                displayCode = parts[0];
                if (parts.Length > 1) reason = parts[1];
            }
            else if (first.Note?.StartsWith("ID-") == true)
            {
                reason = "Giao dịch lẻ";
            }
            else
            {
                reason = first.Note ?? "Giao dịch hệ thống";
            }

            var detail = new InventoryReceiptDetailModel
            {
                ReceiptCode = displayCode,
                Type = first.ChangeType,
                CreatedAt = first.CreatedAt ?? DateTime.Now,
                Note = reason,
                Items = logs.Select(l => new ReceiptProductDetail
                {
                    ProductId = l.ProductId ?? 0,
                    ProductName = l.Product?.Name ?? "Sản phẩm đã xóa",
                    Sku = l.Product?.Sku ?? "N/A",
                    ImageUrl = l.Product?.ProductImages.FirstOrDefault(i => i.IsMain == 1)?.ImageUrl ?? l.Product?.ProductImages.FirstOrDefault()?.ImageUrl,
                    Quantity = l.Quantity ?? 0,
                    UnitPrice = l.Product?.Price ?? 0
                }).ToList()
            };

            return Json(new { success = true, data = detail });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductSearch(string q)
        {
            if (string.IsNullOrEmpty(q)) return Json(new List<object>());
            q = q.ToLower();
            var products = await _context.Products
                .Where(p => (p.Name.ToLower().Contains(q) || p.Sku.ToLower().Contains(q)) && p.Status == 1)
                .Take(10)
                .Select(p => new { id = p.Id, name = p.Name, sku = p.Sku, stock = p.StockQuantity, unit = p.Unit, price = p.Price })
                .ToListAsync();
            return Json(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateReceipt([FromBody] CreateReceiptRequest request)
        {
            if (request == null || request.Products == null || !request.Products.Any())
                return Json(new { success = false, message = "Dữ logic phiếu không hợp lệ" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var dateStr = DateTime.Now.ToString("yyyyMMdd");
                var prefix = request.Type == "Import" ? "PN" : "PX";
                var random = new Random().Next(100, 999);
                var receiptCode = $"{prefix}-{dateStr}-{random}";
                
                // Lưu Note theo định dạng REF:CODE|REASON để sau này dễ tách
                var finalNote = $"REF:{receiptCode}|{request.Note ?? "Giao dịch kho"}";

                foreach (var item in request.Products)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null) throw new Exception($"Sản phẩm ID {item.ProductId} không tồn tại");

                    // Logic cộng/trừ kho sẽ được Trigger trg_inventory_safe xử lý tự động
                    // Tuy nhiên ta vẫn check sơ bộ ở đây để báo lỗi sớm cho người dùng
                    if (request.Type == "Export" && (product.StockQuantity ?? 0) < item.Quantity)
                        throw new Exception($"Sản phẩm '{product.Name}' không đủ tồn kho để xuất (Còn lại: {product.StockQuantity})");

                    var log = new InventoryLog
                    {
                        ProductId = product.Id,
                        ChangeType = request.Type,
                        Quantity = item.Quantity,
                        Status = 1,
                        Note = finalNote,
                        CreatedAt = DateTime.Now
                    };
                    _context.InventoryLogs.Add(log);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = $"Tạo { (request.Type == "Import" ? "phiếu nhập" : "phiếu xuất") } thành công: {receiptCode}", code = receiptCode });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi nghiệp vụ: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PrintAllReceipts()
        {
            var logs = await _context.InventoryLogs
                .Include(l => l.Product)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
            
            ViewData["PrintTitle"] = "BÁO CÁO TOÀN BỘ GIAO DỊCH KHO";
            return View("PrintReceipts", logs);
        }

        [HttpGet]
        public async Task<IActionResult> PrintSelected(string codes)
        {
            if (string.IsNullOrEmpty(codes)) return RedirectToAction("Index");
            
            var selectedCodes = JsonSerializer.Deserialize<List<string>>(codes);
            if (selectedCodes == null || !selectedCodes.Any()) return RedirectToAction("Index");

            var allLogs = await _context.InventoryLogs.Include(l => l.Product).ToListAsync();
            var filteredLogs = allLogs.Where(l => {
                if (string.IsNullOrEmpty(l.Note)) return false;
                foreach (var code in selectedCodes) {
                    if (l.Note.Contains(code)) return true;
                }
                return false;
            }).OrderByDescending(l => l.CreatedAt).ToList();

            ViewData["PrintTitle"] = "CHI TIẾT PHIÊU KHO";
            return View("PrintReceipts", filteredLogs);
        }

        // Giữ lại ExportReport (Excel) cho Báo cáo tồn kho vì nó cần tính toán số lượng
        [HttpGet]
        public async Task<IActionResult> ExportReport()
        {
            var products = await _context.Products.Include(p => p.Category).OrderBy(p => p.StockQuantity).ToListAsync();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("BaoCaoTonKho");
            
            ws.Cell(1, 1).Value = "BÁO CÁO TỒN KHO HOA QUẢ - " + DateTime.Now.ToString("dd/MM/yyyy");
            ws.Range(1, 1, 1, 5).Merge().Style.Font.Bold = true;

            var headers = new[] { "Mã SKU", "Tên sản phẩm", "Danh mục", "Đơn vị", "Số lượng tồn" };
            for (int i = 0; i < headers.Length; i++) {
                ws.Cell(2, i + 1).Value = headers[i];
                ws.Cell(2, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }

            for (int i = 0; i < products.Count; i++) {
                ws.Cell(i + 3, 1).Value = products[i].Sku;
                ws.Cell(i + 3, 2).Value = products[i].Name;
                ws.Cell(i + 3, 3).Value = products[i].Category?.Name;
                ws.Cell(i + 3, 4).Value = products[i].Unit;
                ws.Cell(i + 3, 5).Value = products[i].StockQuantity;
                if (products[i].StockQuantity <= 10) ws.Cell(i + 3, 5).Style.Font.FontColor = XLColor.Red;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }
}
