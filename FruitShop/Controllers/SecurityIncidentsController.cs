using System;
using System.Linq;
using System.Threading.Tasks;
using FruitShop.Filters;
using FruitShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FruitShop.Controllers
{
    [AdminSecurityFilter]
    public class SecurityIncidentsController : Controller
    {
        private readonly FruitShopContext _context;

        private static readonly string[] AllowedStatuses =
        {
            "Mới", "Đang xử lý", "Đã ngăn chặn", "Đã đóng"
        };

        public SecurityIncidentsController(FruitShopContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? status = null, string? severity = null)
        {
            var query = _context.SecurityIncidents.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(severity))
            {
                query = query.Where(i => i.Severity == severity);
            }

            var incidents = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewData["FilterStatus"] = status;
            ViewData["FilterSeverity"] = severity;
            return View(incidents);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? resolutionNote)
        {
            if (!AllowedStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Trạng thái không hợp lệ." });
            }

            var incident = await _context.SecurityIncidents.FindAsync(id);
            if (incident == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sự cố." });
            }

            incident.Status = status;
            incident.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote) ? null : resolutionNote.Trim();
            incident.ResolvedAt = (status == "Đã đóng" || status == "Đã ngăn chặn") ? DateTime.UtcNow : null;

            _context.Update(incident);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã cập nhật trạng thái và hướng dẫn xử lý." });
        }
    }
}
