using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FruitShop.Models;
using FruitShop.ViewModels;
using FruitShop.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using ClosedXML.Excel;

namespace FruitShop.Controllers
{
    public class UsersController : Controller
    {
        private readonly FruitShopContext _context;
        private readonly ISearchService _searchService;
        private const string INDEX_NAME = "users";

        public UsersController(FruitShopContext context, ISearchService searchService)
        {
            _context = context;
            _searchService = searchService;
        }

        public async Task<IActionResult> Index(
            int page = 1, 
            int pageSize = 10, 
            string searchTerm = "",
            byte? fRoleId = null,
            byte? fStatus = null)
        {
            var query = _context.Users
                .Include(u => u.Role)
                .Where(u => u.DeletedAt == null)
                .AsQueryable();

            // Meilisearch logic
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var ids = await _searchService.SearchIdsAsync(INDEX_NAME, searchTerm, 1000);
                if (ids != null && ids.Any())
                {
                    var intIds = ids.Select(int.Parse).ToList();
                    query = query.Where(u => intIds.Contains(u.Id));
                }
                else
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(u => (u.FullName != null && u.FullName.ToLower().Contains(searchTerm)) || 
                                           (u.Email != null && u.Email.ToLower().Contains(searchTerm)));
                }
            }

            // Advanced Filters
            if (fRoleId.HasValue) query = query.Where(u => u.RoleId == fRoleId.Value);
            if (fStatus.HasValue) query = query.Where(u => u.Status == fStatus.Value);

            int totalItems = await query.CountAsync();
            var data = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new UserList
            {
                Users = data,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                TotalItems = totalItems,
                PageSize = pageSize
            };

            ViewBag.Roles = await _context.Roles.ToListAsync();
            ViewData["SearchTerm"] = searchTerm;
            ViewData["fRoleId"] = fRoleId;
            ViewData["fStatus"] = fStatus;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            var u = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);

            if (u == null) return Json(new { success = false, message = "Không tìm thấy người dùng" });

            return Json(new {
                success = true,
                data = new {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.RoleId,
                    RoleName = u.Role?.Name,
                    u.Status,
                    u.IsVerified,
                    u.AvatarUrl,
                    CreatedAt = u.CreatedAt?.ToString("dd/MM/yyyy HH:mm")
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    user.CreatedAt = DateTime.UtcNow;
                    user.Status = 1;
                    // Note: Password hashing should be handled here if not already
                    _context.Add(user);
                    await _context.SaveChangesAsync();

                    // Meilisearch Indexing
                    await _searchService.IndexDocumentsAsync(INDEX_NAME, new[] { new { id = user.Id, fullName = user.FullName, email = user.Email } });

                    return Json(new { success = true, message = "Thêm người dùng thành công" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id) return Json(new { success = false, message = "ID không khớp" });

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Users.FindAsync(id);
                    if (existing == null || existing.DeletedAt != null) 
                        return Json(new { success = false, message = "Người dùng không tồn tại" });

                    existing.FullName = user.FullName;
                    existing.Email = user.Email;
                    existing.Phone = user.Phone;
                    existing.RoleId = user.RoleId;
                    existing.Status = user.Status;
                    existing.IsVerified = user.IsVerified;
                    if (!string.IsNullOrEmpty(user.AvatarUrl)) existing.AvatarUrl = user.AvatarUrl;

                    _context.Update(existing);
                    await _context.SaveChangesAsync();

                    // Meilisearch Indexing
                    await _searchService.IndexDocumentsAsync(INDEX_NAME, new[] { new { id = existing.Id, fullName = existing.FullName, email = existing.Email } });

                    return Json(new { success = true, message = "Cập nhật người dùng thành công" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
            return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại" });

                user.DeletedAt = DateTime.UtcNow;
                user.Status = 0;
                await _context.SaveChangesAsync();

                // Meilisearch Deletion (or update to reflect soft delete)
                await _searchService.DeleteDocumentAsync(INDEX_NAME, id.ToString());

                return Json(new { success = true, message = "Xóa người dùng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected([FromBody] int[] ids)
        {
            if (ids == null || !ids.Any()) return Json(new { success = false, message = "Không có mục nào được chọn" });

            try
            {
                var users = await _context.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
                foreach (var u in users)
                {
                    u.DeletedAt = DateTime.UtcNow;
                    u.Status = 0;
                }
                await _context.SaveChangesAsync();

                // Meilisearch Deletion
                await _searchService.DeleteDocumentsAsync(INDEX_NAME, ids.Select(id => id.ToString()));

                return Json(new { success = true, message = $"Đã xóa thành công {users.Count} người dùng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa nhiều: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? ids)
        {
            var query = _context.Users.Include(u => u.Role).Where(u => u.DeletedAt == null).AsQueryable();
            if (!string.IsNullOrEmpty(ids))
            {
                var idList = ids.Split(',').Select(int.Parse);
                query = query.Where(u => idList.Contains(u.Id));
            }
            var data = await query.ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Users");
            ws.Cell(1, 1).Value = "Họ tên";
            ws.Cell(1, 2).Value = "Email";
            ws.Cell(1, 3).Value = "Số điện thoại";
            ws.Cell(1, 4).Value = "Vai trò";
            ws.Cell(1, 5).Value = "Trạng thái";
            ws.Cell(1, 6).Value = "Ngày tạo";

            for (int i = 0; i < data.Count; i++)
            {
                ws.Cell(i + 2, 1).Value = data[i].FullName;
                ws.Cell(i + 2, 2).Value = data[i].Email;
                ws.Cell(i + 2, 3).Value = data[i].Phone;
                ws.Cell(i + 2, 4).Value = data[i].Role?.Name;
                ws.Cell(i + 2, 5).Value = data[i].Status == 1 ? "Hoạt động" : "Khóa";
                ws.Cell(i + 2, 6).Value = data[i].CreatedAt?.ToString("dd/MM/yyyy HH:mm");
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> ReindexAll()
        {
            var data = await _context.Users
                .Where(u => u.DeletedAt == null)
                .Select(u => new { id = u.Id, fullName = u.FullName, email = u.Email })
                .ToListAsync();
            await _searchService.IndexDocumentsAsync(INDEX_NAME, data);
            return Json(new { success = true, message = "Đã đồng bộ Meilisearch thành công cho Người dùng" });
        }
    }
}
