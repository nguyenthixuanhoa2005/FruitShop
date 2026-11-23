using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FruitShop.AddonSecurity
{
    [Route("AdminAddon")]
    public class AdminAddonController : Controller
    {
        private readonly ILogger<AdminAddonController> _logger;
        private readonly AddonScanner _scanner;

        public AdminAddonController(ILogger<AdminAddonController> logger)
        {
            _logger = logger;
            _scanner = new AddonScanner();
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost("Upload")]
        [ValidateAntiForgeryToken]
        public IActionResult Upload(IFormFile addonFile)
        {
            if (addonFile == null || addonFile.Length == 0)
            {
                ViewBag.Error = "Vui lòng chọn tệp .zip của gói Add-on cần cài đặt!";
                return View("Index");
            }

            var extension = Path.GetExtension(addonFile.FileName).ToLowerInvariant();
            if (extension != ".zip")
            {
                ViewBag.Error = "Chỉ chấp nhận tệp nén định dạng .zip!";
                return View("Index");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "FruitShop_Addon_" + Guid.NewGuid().ToString("N"));
            var zipPath = Path.Combine(tempDir, addonFile.FileName);

            try
            {
                Directory.CreateDirectory(tempDir);
                using (var stream = new FileStream(zipPath, FileMode.Create))
                {
                    addonFile.CopyTo(stream);
                }

                var extractDir = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Quét bảo mật add-on
                var findings = _scanner.ScanDirectory(extractDir);

                if (findings.Any())
                {
                    _logger.LogWarning("Add-on {Name} có {Count} dấu hiệu đáng ngờ", addonFile.FileName, findings.Count);
                    ViewBag.AddonName = addonFile.FileName;
                    ViewBag.FindingsCount = findings.Count;
                    ViewBag.ExtractDir = extractDir;
                    return View("AddonWarning", findings);
                }

                ViewBag.Success = $"Gói Add-on '{addonFile.FileName}' đã được quét bảo mật thành công: KHÔNG phát hiện mã độc hại!";
                return View("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi giải nén hoặc quét Add-on");
                ViewBag.Error = "Lỗi khi xử lý gói Add-on: " + ex.Message;
                return View("Index");
            }
            finally
            {
                // Dọn dẹp nếu an toàn hoặc lỗi
            }
        }
    }
}
