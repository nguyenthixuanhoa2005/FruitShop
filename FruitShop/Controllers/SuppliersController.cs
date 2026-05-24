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
    public class SuppliersController : Controller
    {
        private readonly FruitShopContext _context;
        public SuppliersController(FruitShopContext context) { _context = context; }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string searchTerm = "", byte? status = null)
        {
            var query = _context.Suppliers.AsQueryable();
            if (!string.IsNullOrEmpty(searchTerm)) 
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(searchTerm) || 
                                       (s.Phone != null && s.Phone.Contains(searchTerm)) ||
                                       (s.Email != null && s.Email.ToLower().Contains(searchTerm)));
            }
            if (status.HasValue) query = query.Where(s => s.Status == status.Value);

            var totalItems = await query.CountAsync();
            var data = await query.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var model = new SupplierList { 
                Suppliers = data, 
                CurrentPage = page, 
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                TotalItems = totalItems,
                PageSize = pageSize
            };

            ViewData["SearchTerm"] = searchTerm;
            ViewData["Status"] = status;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] Supplier supplier)
        {
            try 
            {
                if (string.IsNullOrEmpty(supplier.Name)) return Json(new { success = false, message = "Tên không được để trống" });
                
                supplier.CreatedAt = DateTime.Now;
                if (supplier.Status == null) supplier.Status = 1;
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Thêm nhà cung cấp thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] Supplier supplier)
        {
            try 
            {
                var existing = await _context.Suppliers.FindAsync(supplier.Id);
                if (existing == null) return Json(new { success = false, message = "Không tìm thấy nhà cung cấp" });
                
                existing.Name = supplier.Name;
                existing.Phone = supplier.Phone;
                existing.Email = supplier.Email;
                existing.Address = supplier.Address;
                existing.Status = supplier.Status;
                
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers.Include(s => s.Products).FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return Json(new { success = false, message = "Không tìm thấy" });
            if (supplier.Products.Any()) return Json(new { success = false, message = "Không thể xóa nhà cung cấp đang có sản phẩm." });
            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return Json(new { success = false, message = "Chưa chọn bản ghi nào." });
            var suppliers = await _context.Suppliers.Include(s => s.Products).Where(s => ids.Contains(s.Id)).ToListAsync();
            var canDelete = suppliers.Where(s => !s.Products.Any()).ToList();
            if (!canDelete.Any()) return Json(new { success = false, message = "Tất cả nhà cung cấp đã chọn đều đang có sản phẩm, không thể xóa." });
            
            _context.Suppliers.RemoveRange(canDelete);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã xóa thành công {canDelete.Count} nhà cung cấp." });
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

            var errorLines = new List<string>();
            var newItems = new List<Supplier>();
            
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existingNames = await _context.Suppliers.Select(s => s.Name.ToLower()).ToListAsync();
                var existingPhones = await _context.Suppliers.Where(s => s.Phone != null).Select(s => s.Phone!.ToLower()).ToListAsync();
                
                string[] headers;
                List<string[]> rows = new List<string[]>();

                if (ext == ".csv")
                {
                    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        rows.Add(ParseCsvLine(line));
                    }
                    if (rows.Count < 2) return BadRequest(new { success = false, message = "File CSV không có dữ liệu." });
                    headers = rows[0];
                    rows = rows.Skip(1).ToList();
                }
                else // .xlsx
                {
                    using var workbook = new XLWorkbook(file.OpenReadStream());
                    var worksheet = workbook.Worksheets.First();
                    var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                    var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;
                    headers = Enumerable.Range(1, lastCol).Select(c => worksheet.Cell(1, c).GetString().Trim()).ToArray();
                    for (int r = 2; r <= lastRow; r++)
                    {
                        if (worksheet.Row(r).IsEmpty()) continue;
                        rows.Add(Enumerable.Range(1, lastCol).Select(c => worksheet.Cell(r, c).GetString().Trim()).ToArray());
                    }
                }

                // Header mapping
                var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length; i++) colMap[headers[i].Replace("\uFEFF", "")] = i;

                for (int i = 0; i < rows.Count; i++)
                {
                    var values = rows[i];
                    var lineNum = i + 2;
                    try
                    {
                        var s = new Supplier { CreatedAt = DateTime.Now, Status = 1 };
                        
                        if (colMap.TryGetValue("Tên nhà cung cấp", out int nameIdx) || colMap.TryGetValue("Name", out nameIdx))
                        {
                            var name = values[nameIdx].Trim();
                            if (string.IsNullOrEmpty(name)) throw new Exception("Tên nhà cung cấp không được để trống.");
                            if (existingNames.Contains(name.ToLower())) throw new Exception($"Tên '{name}' đã tồn tại.");
                            if (newItems.Any(ni => ni.Name.ToLower() == name.ToLower())) throw new Exception($"Tên '{name}' bị trùng trong file.");
                            s.Name = name;
                        }
                        else throw new Exception("Không tìm thấy cột 'Tên nhà cung cấp'.");

                        if (colMap.TryGetValue("Điện thoại", out int phoneIdx) || colMap.TryGetValue("Phone", out phoneIdx))
                        {
                            var phone = values[phoneIdx].Trim();
                            if (!string.IsNullOrEmpty(phone))
                            {
                                if (existingPhones.Contains(phone.ToLower())) throw new Exception($"SĐT '{phone}' đã tồn tại.");
                                if (newItems.Any(ni => ni.Phone != null && ni.Phone.ToLower() == phone.ToLower())) throw new Exception($"SĐT '{phone}' bị trùng trong file.");
                                s.Phone = phone;
                            }
                        }

                        if (colMap.TryGetValue("Email", out int emailIdx)) s.Email = values[emailIdx].Trim();
                        if (colMap.TryGetValue("Địa chỉ", out int addrIdx)) s.Address = values[addrIdx].Trim();

                        _context.Suppliers.Add(s);
                        newItems.Add(s);
                    }
                    catch (Exception ex)
                    {
                        errorLines.Add($"Dòng {lineNum}: {ex.Message}");
                    }
                }

                if (errorLines.Any())
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Nhập file thất bại. Có {errorLines.Count} lỗi.", errors = errorLines });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = $"Nhập thành công {newItems.Count} nhà cung cấp." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CopyAll(string? ids, string? searchTerm, byte? status)
        {
            var query = _context.Suppliers.AsQueryable();
            
            if (!string.IsNullOrEmpty(ids))
            {
                var idList = ids.Split(',').Select(int.Parse).ToList();
                query = query.Where(s => idList.Contains(s.Id));
            }
            else 
            {
                // Nếu không truyền IDs, áp dụng bộ lọc giống như trang Index để lấy "Copy All"
                if (!string.IsNullOrEmpty(searchTerm)) 
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(s => s.Name.ToLower().Contains(searchTerm) || 
                                           (s.Phone != null && s.Phone.Contains(searchTerm)) ||
                                           (s.Email != null && s.Email.ToLower().Contains(searchTerm)));
                }
                if (status.HasValue) query = query.Where(s => s.Status == status.Value);
            }

            var data = await query.ToListAsync();
            var result = data.Select(s => new {
                name = s.Name,
                phone = s.Phone,
                email = s.Email,
                address = s.Address,
                status = s.Status
            }).ToList();

            return Json(new { success = true, items = result });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? ids)
        {
            var data = await GetExportData(ids);
            using var wb = new XLWorkbook(); 
            var ws = wb.Worksheets.Add("Suppliers");
            ws.Cell(1, 1).Value = "ID"; 
            ws.Cell(1, 2).Value = "Tên nhà cung cấp"; 
            ws.Cell(1, 3).Value = "Điện thoại"; 
            ws.Cell(1, 4).Value = "Email"; 
            ws.Cell(1, 5).Value = "Địa chỉ";
            ws.Cell(1, 6).Value = "Ngày tạo";
            
            for (int i = 0; i < data.Count; i++) {
                ws.Cell(i + 2, 1).Value = data[i].Id; 
                ws.Cell(i + 2, 2).Value = data[i].Name; 
                ws.Cell(i + 2, 3).Value = data[i].Phone; 
                ws.Cell(i + 2, 4).Value = data[i].Email; 
                ws.Cell(i + 2, 5).Value = data[i].Address;
                ws.Cell(i + 2, 6).Value = data[i].CreatedAt;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Suppliers.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? ids)
        {
            var data = await GetExportData(ids);
            var csv = new StringBuilder(); 
            csv.AppendLine("ID,Tên nhà cung cấp,Điện thoại,Email,Địa chỉ");
            foreach (var s in data) 
            {
                csv.AppendLine($"{s.Id},\"{s.Name}\",\"{s.Phone}\",\"{s.Email}\",\"{s.Address}\"");
            }
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "Suppliers.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportJson(string? ids)
        {
            var data = await GetExportData(ids);
            var options = new JsonSerializerOptions { WriteIndented = true };
            return File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, options)), "application/json", "Suppliers.json");
        }

        [HttpGet]
        public async Task<IActionResult> ExportXml(string? ids)
        {
            var data = await GetExportData(ids);
            var list = data.Select(s => new SupplierXmlModel { Id = s.Id, Name = s.Name, Phone = s.Phone, Email = s.Email, Address = s.Address }).ToList();
            var ser = new XmlSerializer(typeof(List<SupplierXmlModel>));
            using var sw = new StringWriter(); 
            ser.Serialize(sw, list);
            return File(Encoding.UTF8.GetBytes(sw.ToString()), "application/xml", "Suppliers.xml");
        }

        private async Task<List<Supplier>> GetExportData(string? ids) {
            var query = _context.Suppliers.AsQueryable();
            if (!string.IsNullOrEmpty(ids)) {
                var idList = ids.Split(',').Select(int.Parse).ToList();
                query = query.Where(s => idList.Contains(s.Id));
            }
            return await query.ToListAsync();
        }

        [HttpGet]
        public IActionResult DownloadTemplate() {
            var csv = new StringBuilder();
            csv.AppendLine("Tên nhà cung cấp,Điện thoại,Email,Địa chỉ");
            csv.AppendLine("Công ty Trái cây sạch,0987654321,ncc@gmail.com,Hà Nội");
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "Mau_Nhap_Nha_Cung_Cap.csv");
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes) { fields.Add(currentField.ToString()); currentField.Clear(); }
                else currentField.Append(c);
            }
            fields.Add(currentField.ToString());
            return fields.Select(f => f.Trim('\"', ' ')).ToArray();
        }
    }

    public class SupplierXmlModel { public int Id { get; set; } public string? Name { get; set; } public string? Phone { get; set; } public string? Email { get; set; } public string? Address { get; set; } }
}
