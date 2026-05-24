using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using FruitShop.Models;
using FruitShop.ViewModels;

namespace FruitShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly FruitShopContext _context;

        public HomeController(FruitShopContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Lấy tất cả danh mục để xử lý phân cấp trong bộ nhớ
            var categories = await _context.Categories.ToListAsync();
            
            // 2. Xác định danh sách ID con cho từng loại
            var importedCategoryIds = categories.Where(c => c.Id == 1 || c.ParentId == 1).Select(c => c.Id).ToList();
            var localCategoryIds = categories.Where(c => c.Id == 2 || c.ParentId == 2).Select(c => c.Id).ToList();
            var giftCategoryIds = categories.Where(c => c.Id == 3 || c.ParentId == 3 || (c.ParentId != null && categories.Any(p => p.Id == c.ParentId && p.ParentId == 3))).Select(c => c.Id).ToList();

            // 3. Lấy tất cả sản phẩm đang kinh doanh
            var allProducts = await _context.Products
                .Include(p => p.Reviews)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == 1)
                .ToListAsync();

            var viewModel = new HomeViewModel
            {
                Categories = categories.Where(c => c.Status == 1 && c.ParentId == null).ToList(),
                
                // Trộn ngẫu nhiên cho mục Bán chạy
                FeaturedProducts = allProducts
                    .OrderBy(x => Guid.NewGuid())
                    .Take(12)
                    .ToList(),
                
                // Lấy đầy đủ sản phẩm nhập khẩu (bao gồm táo, nho, cherry...)
                ImportedFruits = allProducts
                    .Where(p => importedCategoryIds.Contains(p.CategoryId ?? 0))
                    .Take(8)
                    .ToList(),

                // Lấy đầy đủ sản phẩm nội địa
                LocalFruits = allProducts
                    .Where(p => localCategoryIds.Contains(p.CategoryId ?? 0))
                    .Take(8)
                    .ToList(),

                // Lấy đầy đủ quà tặng (bao gồm giỏ và hộp)
                GiftFruits = allProducts
                    .Where(p => giftCategoryIds.Contains(p.CategoryId ?? 0))
                    .OrderBy(x => Guid.NewGuid())
                    .Take(8)
                    .ToList()
            };
            return View(viewModel);
        }

        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email && u.Status == 1);

            if (user == null)
            {
                model.ErrorMessage = "Email hoặc mật khẩu không chính xác";
                return View(model);
            }

            // TODO: thực hiện xác thực mật khẩu đúng cách (ví dụ: sử dụng hashing và salting)
            // Temp: so sánh trực tiếp (KHÔNG AN TOÀN, chỉ để demo)
            if (user.PasswordHash != model.Password)
            {
                model.ErrorMessage = "Email hoặc mật khẩu không chính xác";
                return View(model);
            }

            // TODO: Implement proper authentication/session
            HttpContext.Session.SetString("UserId", user.Id.ToString());
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserRole", user.RoleId?.ToString() ?? "2");

            // Load giỏ hàng từ database vào Session
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == user.Id && c.Status == 1)
                .ToListAsync();
            
            if (cartItems.Count > 0)
            {
                var cart = new Dictionary<int, int>();
                foreach (var item in cartItems)
                {
                    cart[item.ProductId ?? 0] = item.Quantity ?? 0;
                }
                HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));
            }

            // Redirect dựa trên RoleId (Giả sử 1 là Admin, 2 là User)
            if (user.RoleId == 1)
            {
                return RedirectToAction("Index", "AdminHome");
            }

            return RedirectToAction("Index", "Products");
        }

        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Password != model.ConfirmPassword)
            {
                model.ErrorMessage = "Mật khẩu không khớp";
                return View(model);
            }

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (existingUser != null)
            {
                model.ErrorMessage = "Email này đã được sử dụng";
                return View(model);
            }

            var newUser = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                PasswordHash = model.Password, // TODO: Hash password properly
                RoleId = 2, // User role
                Status = 1,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Auto login after register
            HttpContext.Session.SetString("UserId", newUser.Id.ToString());
            HttpContext.Session.SetString("UserEmail", newUser.Email);
            HttpContext.Session.SetString("UserName", newUser.FullName);

            // Load giỏ hàng từ database vào Session (nếu có)
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == newUser.Id && c.Status == 1)
                .ToListAsync();
            
            if (cartItems.Count > 0)
            {
                var cart = new Dictionary<int, int>();
                foreach (var item in cartItems)
                {
                    cart[item.ProductId ?? 0] = item.Quantity ?? 0;
                }
                HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));
            }

            return RedirectToAction("Index", "Products");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Products");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
