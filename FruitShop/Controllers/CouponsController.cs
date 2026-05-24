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
using System.Text.Json;
using System.Xml.Serialization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace FruitShop.Controllers
{
    public class CouponsController : Controller
    {
        private readonly FruitShopContext _context;
        public CouponsController(FruitShopContext context) { _context = context; }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string searchTerm = "", string? fStatus = null)
        {
            var query = _context.Coupons.AsQueryable();
            DateTime now = DateTime.Now;

            if (!string.IsNullOrEmpty(searchTerm)) 
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(c => c.Code.ToLower().Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(fStatus)) {
                if (fStatus == "1") query = query.Where(c => c.Status == 1);
                else if (fStatus == "0") query = query.Where(c => c.Status == 0);
                else if (fStatus == "active") query = query.Where(c => c.Status == 1 && (c.StartDate == null || c.StartDate <= now) && (c.EndDate == null || c.EndDate >= now));
                else if (fStatus == "expired") query = query.Where(c => c.EndDate != null && c.EndDate < now);
            }

            var totalItems = await query.CountAsync();
            var data = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new CouponList {
                Coupons = data,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                TotalItems = totalItems,
                PageSize = pageSize
            };

            // Stats
            ViewBag.TotalCoupons = await _context.Coupons.CountAsync();
            ViewBag.ActiveCoupons = await _context.Coupons.CountAsync(c => c.Status == 1 && (c.EndDate == null || c.EndDate >= now));
            
            ViewData["SearchTerm"] = searchTerm;
            ViewData["fStatus"] = fStatus;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id) {
            var c = await _context.Coupons.FindAsync(id);
            if (c == null) return Json(new { success = false, message = "Không tìm thấy" });
            return Json(new { success = true, data = c });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] Coupon c) {
            try {
                if (string.IsNullOrEmpty(c.Code)) return Json(new { success = false, message = "Mã không được để trống" });
                if (await _context.Coupons.AnyAsync(x => x.Code == c.Code.ToUpper())) 
                    return Json(new { success = false, message = "Mã giảm giá này đã tồn tại" });

                c.CreatedAt = DateTime.Now;
                c.UsedCount = 0;
                c.Code = c.Code.ToUpper();
                if (c.Status == null) c.Status = 1;

                _context.Coupons.Add(c);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Thêm mã giảm giá thành công!" });
            } catch (Exception ex) { return Json(new { success = false, message = "Lỗi: " + ex.Message }); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromForm] Coupon c) {
            try {
                var existing = await _context.Coupons.FindAsync(c.Id);
                if (existing == null) return Json(new { success = false, message = "Không tìm thấy" });
                
                existing.Code = c.Code.ToUpper();
                existing.DiscountType = c.DiscountType;
                existing.DiscountValue = c.DiscountValue;
                existing.MinOrderValue = c.MinOrderValue;
                existing.MaxDiscount = c.MaxDiscount;
                existing.UsageLimit = c.UsageLimit;
                existing.StartDate = c.StartDate;
                existing.EndDate = c.EndDate;
                existing.Status = c.Status;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            } catch (Exception ex) { return Json(new { success = false, message = "Lỗi: " + ex.Message }); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id) {
            var c = await _context.Coupons.FindAsync(id);
            if (c == null) return Json(new { success = false, message = "Không tìm thấy" });
            _context.Coupons.Remove(c);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa mã giảm giá!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected([FromBody] List<int> ids) {
            if (ids == null || !ids.Any()) return Json(new { success = false, message = "Chưa chọn mã nào" });
            var items = await _context.Coupons.Where(c => ids.Contains(c.Id)).ToListAsync();
            _context.Coupons.RemoveRange(items);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã xóa thành công {items.Count} mã giảm giá" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file) {
            if (file == null || file.Length == 0) return Json(new { success = false, message = "Vui lòng chọn file" });
            
            var errorLines = new List<string>();
            var newItems = new List<Coupon>();
            using var transaction = await _context.Database.BeginTransactionAsync();

            try {
                using var workbook = new XLWorkbook(file.OpenReadStream());
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1);
                int rowNum = 2;

                var existingCodes = await _context.Coupons.Select(x => x.Code.ToUpper()).ToListAsync();

                foreach (var row in rows) {
                    try {
                        var code = row.Cell(1).GetValue<string>()?.Trim().ToUpper();
                        if (string.IsNullOrEmpty(code)) throw new Exception("Mã không được trống");
                        if (existingCodes.Contains(code)) throw new Exception($"Mã '{code}' đã tồn tại");
                        if (newItems.Any(x => x.Code == code)) throw new Exception($"Mã '{code}' bị trùng trong file");

                        newItems.Add(new Coupon { 
                            Code = code, 
                            DiscountType = row.Cell(2).GetValue<string>()?.ToLower() == "fixed" ? "fixed" : "percent",
                            DiscountValue = row.Cell(3).GetValue<decimal>(),
                            UsageLimit = row.Cell(4).GetValue<int>(),
                            MinOrderValue = row.Cell(5).GetValue<decimal>(),
                            Status = 1, CreatedAt = DateTime.Now, UsedCount = 0 
                        });
                    } catch (Exception ex) { errorLines.Add($"Dòng {rowNum}: {ex.Message}"); }
                    rowNum++;
                }

                if (errorLines.Any()) {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Nhập file thất bại. Có {errorLines.Count} lỗi.", errors = errorLines });
                }

                _context.Coupons.AddRange(newItems);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Json(new { success = true, message = $"Đã nhập thành công {newItems.Count} mã" });
            } catch (Exception ex) { 
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message }); 
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? ids) {
            var data = await GetExportData(ids);
            using var wb = new XLWorkbook(); var ws = wb.Worksheets.Add("Coupons");
            ws.Cell(1, 1).Value = "Mã"; ws.Cell(1, 2).Value = "Loại"; ws.Cell(1, 3).Value = "Giá trị"; ws.Cell(1, 4).Value = "Lượt dùng"; ws.Cell(1, 5).Value = "Đã dùng";
            for (int i = 0; i < data.Count; i++) {
                ws.Cell(i+2, 1).Value = data[i].Code; ws.Cell(i+2, 2).Value = data[i].DiscountType; ws.Cell(i+2, 3).Value = data[i].DiscountValue; ws.Cell(i+2, 4).Value = data[i].UsageLimit; ws.Cell(i+2, 5).Value = data[i].UsedCount;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Coupons.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> CopyAll(string? ids) {
            var query = _context.Coupons.AsQueryable();
            if (!string.IsNullOrEmpty(ids)) {
                var idList = ids.Split(',').Select(int.Parse).ToList();
                query = query.Where(c => idList.Contains(c.Id));
            }
            var data = await query.Select(c => new { 
                code = c.Code, discountType = c.DiscountType, discountValue = c.DiscountValue, 
                usageLimit = c.UsageLimit, minOrderValue = c.MinOrderValue, maxDiscount = c.MaxDiscount 
            }).ToListAsync();
            return Json(new { success = true, items = data });
        }

        private async Task<List<Coupon>> GetExportData(string? ids) {
            var query = _context.Coupons.AsQueryable();
            if (!string.IsNullOrEmpty(ids)) {
                var idList = ids.Split(',').Select(int.Parse).ToList();
                query = query.Where(c => idList.Contains(c.Id));
            }
            return await query.ToListAsync();
        }

        [HttpGet]
        public IActionResult DownloadTemplate() {
            var csv = new StringBuilder();
            csv.AppendLine("Mã Coupon,Loại (percent/fixed),Giá trị giảm,Số lượt dùng,Đơn tối thiểu");
            csv.AppendLine("SUMMER2026,percent,10,100,200000");
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "Mau_Nhap_Coupon.csv");
        }
    }
}
