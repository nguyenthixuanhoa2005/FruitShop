using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace FruitShop.Controllers
{
    public class AdminOrdersController : Controller
    {
        private readonly FruitShopContext _context;
        public AdminOrdersController(FruitShopContext context) { _context = context; }

        public async Task<IActionResult> Index(
            string? searchTerm, 
            byte? status, 
            DateTime? fStartDate, 
            DateTime? fEndDate,
            decimal? fMinPrice,
            decimal? fMaxPrice,
            int page = 1)
        {
            int pageSize = 10;
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .AsQueryable();

            // Filters
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(o => o.Id.ToString().Contains(searchTerm) || 
                                       (o.User != null && o.User.FullName != null && o.User.FullName.ToLower().Contains(searchTerm)) || 
                                       (o.ReceiverName != null && o.ReceiverName.ToLower().Contains(searchTerm)) ||
                                       (o.ReceiverPhone != null && o.ReceiverPhone.Contains(searchTerm)));
            }

            if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            if (fStartDate.HasValue) query = query.Where(o => o.CreatedAt >= fStartDate.Value);
            if (fEndDate.HasValue) query = query.Where(o => o.CreatedAt <= fEndDate.Value.AddDays(1).AddSeconds(-1));
            if (fMinPrice.HasValue) query = query.Where(o => o.TotalAmount >= fMinPrice.Value);
            if (fMaxPrice.HasValue) query = query.Where(o => o.TotalAmount <= fMaxPrice.Value);

            // Statistics (on filtered result)
            var allFiltered = await query.ToListAsync();
            var totalOrders = allFiltered.Count;
            var totalAmount = allFiltered.Sum(o => o.TotalAmount ?? 0);
            
            var model = new OrderListViewModel {
                TotalOrders = totalOrders,
                TotalAmount = totalAmount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalOrders / pageSize),
                
                PendingCount = allFiltered.Count(o => o.Status == 1),
                ShippingCount = allFiltered.Count(o => o.Status == 2),
                CompletedCount = allFiltered.Count(o => o.Status == 3),
                CancelledCount = allFiltered.Count(o => o.Status == 4),
                AverageOrderValue = totalOrders > 0 ? totalAmount / totalOrders : 0,
                SuccessRate = totalOrders > 0 ? (decimal)allFiltered.Count(o => o.Status == 3) / totalOrders * 100 : 0
            };

            model.Orders = allFiltered
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderListItem { 
                    Id = o.Id, 
                    CustomerName = o.ReceiverName ?? o.User?.FullName ?? "Khách vãng lai", 
                    Phone = o.ReceiverPhone ?? o.User?.Phone ?? "",
                    CreatedAt = o.CreatedAt ?? DateTime.Now, 
                    TotalAmount = o.TotalAmount ?? 0, 
                    Status = o.Status ?? 1, 
                    ItemCount = o.OrderItems.Count 
                }).ToList();

            ViewData["SearchTerm"] = searchTerm;
            ViewData["Status"] = status;
            ViewData["fStartDate"] = fStartDate?.ToString("yyyy-MM-dd");
            ViewData["fEndDate"] = fEndDate?.ToString("yyyy-MM-dd");
            ViewData["fMinPrice"] = fMinPrice;
            ViewData["fMaxPrice"] = fMaxPrice;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Coupon)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return Json(new { success = false, message = "Không tìm thấy đơn hàng" });

            var data = new {
                id = order.Id,
                receiverName = order.ReceiverName ?? order.User?.FullName,
                receiverPhone = order.ReceiverPhone ?? order.User?.Phone,
                receiverAddress = order.ReceiverAddress,
                note = order.Note,
                createdAt = order.CreatedAt?.ToString("dd/MM/yyyy HH:mm"),
                couponCode = order.Coupon?.Code,
                discountAmount = order.DiscountAmount ?? 0,
                totalAmount = order.TotalAmount ?? 0,
                status = order.Status,
                items = order.OrderItems.Select(oi => new {
                    productName = oi.Product?.Name ?? "Sản phẩm đã bị xóa",
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice,
                    total = (oi.Quantity ?? 0) * (oi.UnitPrice ?? 0)
                })
            };

            return Json(new { success = true, data = data });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchTerm, byte? status, string? ids)
        {
            var query = _context.Orders.Include(o => o.User).AsQueryable();
            
            if (!string.IsNullOrEmpty(ids))
            {
                var idList = ids.Split(',').Select(int.Parse).ToList();
                query = query.Where(o => idList.Contains(o.Id));
            }
            else
            {
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(o => o.Id.ToString().Contains(searchTerm) || (o.User != null && o.User.FullName.ToLower().Contains(searchTerm)));
                }
                if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            }

            var orders = await query.ToListAsync();
            using var wb = new XLWorkbook(); 
            var ws = wb.Worksheets.Add("Orders");
            
            ws.Cell(1, 1).Value = "Mã đơn"; 
            ws.Cell(1, 2).Value = "Người nhận"; 
            ws.Cell(1, 3).Value = "SĐT"; 
            ws.Cell(1, 4).Value = "Địa chỉ";
            ws.Cell(1, 5).Value = "Tổng tiền"; 
            ws.Cell(1, 6).Value = "Ngày đặt";
            ws.Cell(1, 7).Value = "Trạng thái";

            for (int i = 0; i < orders.Count; i++) { 
                ws.Cell(i + 2, 1).Value = orders[i].Id; 
                ws.Cell(i + 2, 2).Value = orders[i].ReceiverName ?? orders[i].User?.FullName; 
                ws.Cell(i + 2, 3).Value = orders[i].ReceiverPhone ?? orders[i].User?.Phone;
                ws.Cell(i + 2, 4).Value = orders[i].ReceiverAddress;
                ws.Cell(i + 2, 5).Value = (double)(orders[i].TotalAmount ?? 0);
                ws.Cell(i + 2, 6).Value = orders[i].CreatedAt;
                ws.Cell(i + 2, 7).Value = orders[i].Status switch { 1 => "Chờ xác nhận", 2 => "Đang giao", 3 => "Hoàn thành", 4 => "Đã hủy", _ => "Lạ" };
            }
            
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream(); wb.SaveAs(ms); 
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Orders_Export_{DateTime.Now:yyyyMMddHHmm}.xlsx");
        }

        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Mã đơn,Người nhận,SĐT,Địa chỉ,Tổng tiền");
            csv.AppendLine(",Nguyễn Văn A,0901234567,123 Đường ABC Quận 1,500000");
            csv.AppendLine(",Trần Thị B,0987654321,456 Đường XYZ Quận 7,250000");

            var preamble = Encoding.UTF8.GetPreamble();
            var content = Encoding.UTF8.GetBytes(csv.ToString());
            return File(preamble.Concat(content).ToArray(), "text/csv; charset=utf-8", "Mau_Nhap_Don_Hang.csv");
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
}
