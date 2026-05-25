using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using FruitShop.Models;

namespace FruitShop.Controllers
{
     public class CartController : Controller
     {
          private readonly FruitShopContext _context;

          public CartController(FruitShopContext context)
          {
               _context = context;
          }

          public IActionResult Index()
          {
               // Lấy giỏ hàng từ Session
               var cartJson = HttpContext.Session.GetString("Cart");
               var cart = new Dictionary<int, int>();

               if (!string.IsNullOrEmpty(cartJson))
               {
                    cart = JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();
               }

               // Lấy thông tin chi tiết sản phẩm từ database
               var cartItems = new List<CartItem>();
               foreach (var item in cart)
               {
                    var product = _context.Products
                        .Include(p => p.ProductImages)
                        .FirstOrDefault(p => p.Id == item.Key);
                    if (product != null)
                    {
                         cartItems.Add(new CartItem
                         {
                         ProductId = item.Key,
                         Quantity = item.Value,
                         Product = product
                         });
                    }
               }

               var total = cartItems.Sum(x => (x.Product?.FinalPrice ?? 0) * (x.Quantity ?? 0));
               ViewBag.Total = total;
               return View(cartItems);
          }

          [HttpGet]
          public IActionResult CheckLogin()
          {
               var userIdStr = HttpContext.Session.GetString("UserId");
               bool isLoggedIn = !string.IsNullOrEmpty(userIdStr);
               return Json(new { isLoggedIn });
          }

          [HttpPost]
          public async Task<IActionResult> AddToCart([FromBody] CartRequest request)
          {
               try
               {
                    // Kiểm tra đã đăng nhập chưa
                    var userIdStr = HttpContext.Session.GetString("UserId");
                    if (string.IsNullOrEmpty(userIdStr))
                    {
                         return Json(new { success = false, message = "Vui lòng đăng nhập để thêm vào giỏ hàng", requireLogin = true });
                    }

                    int userId = int.Parse(userIdStr);

                    System.Diagnostics.Debug.WriteLine($"AddToCart gọi: ProductId={request?.ProductId}, Quantity={request?.Quantity}, UserId={userId}");

                    if (request?.ProductId <= 0 || request?.Quantity <= 0)
                    {
                         return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                    }

                    // Kiểm tra sản phẩm tồn tại
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.Status == 1);
                    if (product == null)
                    {
                         return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã bị ẩn" });
                    }

                    // Lấy giỏ hàng từ Session
                    var cartJson = HttpContext.Session.GetString("Cart");
                    var cart = new Dictionary<int, int>();

                    if (!string.IsNullOrEmpty(cartJson))
                    {
                         cart = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();
                    }

                    // Thêm sản phẩm vào giỏ hàng
                    if (cart.ContainsKey(request.ProductId))
                    {
                         cart[request.ProductId] += request.Quantity;
                    }
                    else
                    {
                         cart[request.ProductId] = request.Quantity;
                    }

                    // Lưu lại vào Session
                    HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));
                    System.Diagnostics.Debug.WriteLine($"Giỏ hàng Session đã lưu. Tổng sản phẩm: {cart.Count}");

                    // Lưu vào Database
                    foreach (var item in cart)
                    {
                         var existingCartItem = await _context.CartItems
                         .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == item.Key && c.Status == 1);
                         
                         if (existingCartItem != null)
                         {
                         existingCartItem.Quantity = item.Value;
                         }
                         else
                         {
                         _context.CartItems.Add(new CartItem
                         {
                              UserId = userId,
                              ProductId = item.Key,
                              Quantity = item.Value,
                              Status = 1,
                              CreatedAt = DateTime.Now
                         });
                         }
                    }
                    await _context.SaveChangesAsync();
                    
                    // Trả về ID của cart item vừa thêm
                    var newCartItem = await _context.CartItems
                         .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId && c.Status == 1);

                    return Json(new { success = true, message = "Thêm vào giỏ hàng thành công", cartCount = cart.Sum(x => x.Value), cartItemId = newCartItem?.Id });
               }
               catch (Exception ex)
               {
                    System.Diagnostics.Debug.WriteLine($"Lỗi AddToCart: {ex.Message}");
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
               }
          }

          [HttpPost]
          public async Task<IActionResult> UpdateQuantity([FromBody] CartRequest request)
          {
               try
               {
                    var userIdStr = HttpContext.Session.GetString("UserId");
                    if (string.IsNullOrEmpty(userIdStr))
                    {
                         return Json(new { success = false, message = "Vui lòng đăng nhập" });
                    }

                    int userId = int.Parse(userIdStr);
                    System.Diagnostics.Debug.WriteLine($"UpdateQuantity gọi: ProductId={request?.ProductId}, Quantity={request?.Quantity}, UserId={userId}");

                    if (request?.ProductId <= 0 || request?.Quantity < 0)
                    {
                         return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                    }

                    var cartJson = HttpContext.Session.GetString("Cart");
                    System.Diagnostics.Debug.WriteLine($"Giỏ hàng cũ từ Session: {cartJson}");
                    
                    var cart = new Dictionary<int, int>();

                    if (!string.IsNullOrEmpty(cartJson))
                    {
                         cart = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();
                    }

                    if (request.Quantity == 0)
                    {
                         cart.Remove(request.ProductId);
                         // Xóa từ DB
                         var cartItem = await _context.CartItems
                         .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);
                         if (cartItem != null)
                         {
                         _context.CartItems.Remove(cartItem);
                         }
                         System.Diagnostics.Debug.WriteLine($"Xóa sản phẩm {request.ProductId} khỏi giỏ");
                    }
                    else
                    {
                         cart[request.ProductId] = request.Quantity;
                         // Cập nhật DB
                         var cartItem = await _context.CartItems
                         .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId && c.Status == 1);
                         if (cartItem != null)
                         {
                         cartItem.Quantity = request.Quantity;
                         }
                         System.Diagnostics.Debug.WriteLine($"Cập nhật sản phẩm {request.ProductId} -> {request.Quantity} cái");
                    }

                    var newCartJson = System.Text.Json.JsonSerializer.Serialize(cart);
                    HttpContext.Session.SetString("Cart", newCartJson);
                    await _context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"Giỏ hàng mới: {newCartJson}");
                    
                    return Json(new { success = true, message = "Cập nhật thành công", cartCount = cart.Sum(x => x.Value) });
               }
               catch (Exception ex)
               {
                    System.Diagnostics.Debug.WriteLine($"Lỗi UpdateQuantity: {ex.Message}");
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
               }
          }

          [HttpPost]
          public async Task<IActionResult> RemoveFromCart([FromBody] CartRequest request)
          {
               try
               {
                    var userIdStr = HttpContext.Session.GetString("UserId");
                    if (string.IsNullOrEmpty(userIdStr))
                    {
                         return Json(new { success = false, message = "Vui lòng đăng nhập" });
                    }

                    int userId = int.Parse(userIdStr);

                    if (request?.ProductId <= 0)
                    {
                         return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
                    }

                    var cartJson = HttpContext.Session.GetString("Cart");
                    var cart = new Dictionary<int, int>();

                    if (!string.IsNullOrEmpty(cartJson))
                    {
                         cart = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();
                    }

                    cart.Remove(request.ProductId);
                    HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));
                    
                    // Xóa từ DB
                    var cartItem = await _context.CartItems
                         .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);
                    if (cartItem != null)
                    {
                         _context.CartItems.Remove(cartItem);
                         await _context.SaveChangesAsync();
                    }
                    
                    return Json(new { success = true, message = "Xóa thành công" });
               }
               catch (Exception ex)
               {
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
               }
          }

          // Trang Checkout
          public async Task<IActionResult> Checkout(string selectedItems)
          {
               var userIdStr = HttpContext.Session.GetString("UserId");
               if (string.IsNullOrEmpty(userIdStr))
               {
                    return RedirectToAction("Login", "Home");
               }

               int userId = int.Parse(userIdStr);

               // Chuyển string "1,2,3" thành List<int>
               var selectedIds = string.IsNullOrEmpty(selectedItems)
                    ? new List<int>()
                    : selectedItems.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(id => int.TryParse(id, out var productId) ? productId : 0)
                                   .Where(id => id > 0)
                                   .ToList();

               if (!selectedIds.Any())
               {
                    var cartJson = HttpContext.Session.GetString("Cart");
                    if (!string.IsNullOrEmpty(cartJson))
                    {
                         var cart = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();
                         selectedIds = cart.Keys.ToList();
                    }
               }

               if (!selectedIds.Any())
               {
                    TempData["ErrorMessage"] = "Vui lòng chọn sản phẩm để thanh toán";
                    return RedirectToAction("Index");
               }

               // Lấy thông tin sản phẩm từ DB dựa trên ID đã chọn
               var cartItems = await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c => c.UserId == userId && c.Status == 1 && selectedIds.Contains(c.ProductId ?? 0))
                    .ToListAsync();

               if (!cartItems.Any())
               {
                    TempData["ErrorMessage"] = "Sản phẩm không hợp lệ hoặc đã hết hàng";
                    return RedirectToAction("Index");
               }

               return View(cartItems);
          }

          // Validate coupon
          [HttpPost]
          public async Task<IActionResult> ValidateCoupon([FromBody] CouponValidateRequest request)
          {
               try
               {
               string couponCode = request?.CouponCode?.Trim();
               decimal subtotal = request?.Subtotal ?? 0;

                    if (string.IsNullOrEmpty(couponCode))
                    {
                         return Json(new { success = false, message = "Vui lòng nhập mã giảm giá" });
                    }

                    var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode && c.Status == 1);

                    if (coupon == null)
                    {
                         return Json(new { success = false, message = "Mã giảm giá không tồn tại" });
                    }

                    // Check if coupon is expired
                    if (coupon.StartDate > DateTime.Now || coupon.EndDate < DateTime.Now)
                    {
                         return Json(new { success = false, message = "Mã giảm giá đã hết hạn" });
                    }

                    // Check usage limit
                    if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
                    {
                         return Json(new { success = false, message = "Mã này đã hết lượt sử dụng" });
                    }

                    // Check minimum order value
                    if (coupon.MinOrderValue.HasValue && subtotal < coupon.MinOrderValue.Value)
                    {
                         return Json(new { success = false, message = $"Giỏ hàng phải tối thiểu {coupon.MinOrderValue.Value:N0}đ để dùng mã này" });
                    }

                    // Calculate discount
                    decimal discount = 0;
                    string discountDisplay = "";
                    const decimal SHIPPING_FEE = 30000m;
                    
                    if (coupon.DiscountType == "fixed")
                    {
                         discount = coupon.DiscountValue ?? 0;
                         discountDisplay = $"{discount:N0}đ";
                    }
                    else if (coupon.DiscountType == "percent")
                    {
                         discount = (decimal)(Math.Floor(subtotal * (coupon.DiscountValue ?? 0) / 100));
                         if (coupon.MaxDiscount.HasValue && discount > coupon.MaxDiscount.Value)
                         {
                         discount = coupon.MaxDiscount.Value;
                         }
                         discountDisplay = $"{coupon.DiscountValue}% (tối đa {discount:N0}đ)";
                    }

                    return Json(new
                    {
                         success = true,
                         coupon = new
                         {
                         code = coupon.Code,
                         discountType = coupon.DiscountType,
                         discountValue = coupon.DiscountValue,
                         maxDiscount = coupon.MaxDiscount,
                         minOrderValue = coupon.MinOrderValue,
                         discountDisplay = discountDisplay
                         },
                         discountAmount = discount,
                         finalTotal = subtotal + SHIPPING_FEE - discount
                    });
               }
               catch (Exception ex)
               {
                    System.Diagnostics.Debug.WriteLine($"Lỗi ValidateCoupon: {ex.Message}");
                    return Json(new { success = false, message = "Lỗi khi kiểm tra mã" });
               }
          }

          // Tạo Order
          [HttpPost]
          public async Task<IActionResult> CreateOrder([FromBody] OrderCheckoutRequest request)
          {
               try
               {
                    var userIdStr = HttpContext.Session.GetString("UserId");
                    if (string.IsNullOrEmpty(userIdStr))
                    {
                         return Json(new { success = false, message = "Vui lòng đăng nhập" });
                    }

                    int userId = int.Parse(userIdStr);
                    var user = await _context.Users.FindAsync(userId);

                    if (user == null)
                    {
                         return Json(new { success = false, message = "Người dùng không tồn tại" });
                    }

                    var cartJson = HttpContext.Session.GetString("Cart");
                    if (string.IsNullOrEmpty(cartJson))
                    {
                         return Json(new { success = false, message = "Giỏ hàng trống" });
                    }

                    var cart = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();

                    // Filter cart items if selectedProducts provided
                    Dictionary<int, int> itemsToOrder = cart;
                    if (request.SelectedProducts != null && request.SelectedProducts.Count > 0)
                    {
                         itemsToOrder = new Dictionary<int, int>();
                         foreach (var productId in request.SelectedProducts)
                         {
                         if (cart.ContainsKey(productId))
                         {
                              itemsToOrder[productId] = cart[productId];
                         }
                         }
                    }

                    if (itemsToOrder.Count == 0)
                    {
                         return Json(new { success = false, message = "Không có sản phẩm nào được chọn" });
                    }

                    // Tính tổng tiền
                    decimal subtotal = 0;
                    var orderItems = new List<OrderItem>();

                    foreach (var item in itemsToOrder)
                    {
                         var product = await _context.Products.FindAsync(item.Key);
                         if (product == null) continue;

                         var unitPrice = product.FinalPrice ?? product.Price ?? 0;
                         var itemTotal = unitPrice * item.Value;
                         subtotal += itemTotal;

                         orderItems.Add(new OrderItem
                         {
                         ProductId = item.Key,
                         Quantity = item.Value,
                         UnitPrice = unitPrice
                         });
                    }

                    // Phí vận chuyển
                    const decimal SHIPPING_FEE = 30000m;
                    
                    // Xử lý coupon
                    decimal discount = 0;
                    Coupon appliedCoupon = null;

                    if (!string.IsNullOrEmpty(request.CouponCode))
                    {
                         appliedCoupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode && c.Status == 1);

                         if (appliedCoupon != null)
                         {
                         // Check if coupon is valid
                         if (appliedCoupon.StartDate <= DateTime.Now && appliedCoupon.EndDate >= DateTime.Now)
                         {
                              // Check usage limit
                              if (!appliedCoupon.UsageLimit.HasValue || appliedCoupon.UsedCount < appliedCoupon.UsageLimit.Value)
                              {
                                   // Check minimum order value
                                   if (!appliedCoupon.MinOrderValue.HasValue || subtotal >= appliedCoupon.MinOrderValue.Value)
                                   {
                                        // Calculate discount
                                        if (appliedCoupon.DiscountType == "fixed")
                                        {
                                             discount = appliedCoupon.DiscountValue ?? 0;
                                        }
                                        else if (appliedCoupon.DiscountType == "percent")
                                        {
                                             discount = (decimal)Math.Floor(subtotal * (appliedCoupon.DiscountValue ?? 0) / 100);
                                             if (appliedCoupon.MaxDiscount.HasValue && discount > appliedCoupon.MaxDiscount.Value)
                                             {
                                             discount = appliedCoupon.MaxDiscount.Value;
                                             }
                                        }
                                   }
                              }
                         }
                         }
                    }

                    // Tính tổng final
                    decimal totalAmount = subtotal + SHIPPING_FEE - discount;

                    // Tạo Order
                    var order = new Order
                    {
                         UserId = userId,
                         TotalAmount = totalAmount,
                         ReceiverName = request.ReceiverName,
                         ReceiverPhone = request.ReceiverPhone,
                         ReceiverAddress = request.ReceiverAddress,
                         Note = request.Note,
                         Status = 1, // 1: Chờ
                         CreatedAt = DateTime.Now,
                         CouponId = appliedCoupon?.Id,
                         DiscountAmount = discount
                    };

                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();

                    // Thêm OrderItems
                    foreach (var item in orderItems)
                    {
                         item.OrderId = order.Id;
                    }
                    _context.OrderItems.AddRange(orderItems);

                    // Tạo Payment
                    var payment = new Payment
                    {
                         OrderId = order.Id,
                         Method = request.PaymentMethod,
                         Amount = totalAmount,
                         Currency = "VND",
                         Status = request.PaymentMethod == "COD" ? (byte)0 : (byte)1, // COD: pending, others: processing
                         CreatedAt = DateTime.Now
                    };
                    _context.Payments.Add(payment);

                    // Cập nhật used_count của coupon
                    if (appliedCoupon != null && discount > 0)
                    {
                         appliedCoupon.UsedCount = (appliedCoupon.UsedCount ?? 0) + 1;
                         _context.Coupons.Update(appliedCoupon);
                    }

                    // Xóa CartItems từ DB (chỉ những sản phẩm được đặt hàng)
                    var userCartItems = await _context.CartItems
                         .Where(c => c.UserId == userId && c.Status == 1 && c.ProductId.HasValue && itemsToOrder.Keys.Contains(c.ProductId.Value))
                         .ToListAsync();
                    _context.CartItems.RemoveRange(userCartItems);

                    await _context.SaveChangesAsync();

                    // Xóa từ Session Cart (chỉ những sản phẩm được đặt hàng)
                    var updatedCart = new Dictionary<int, int>(cart);
                    foreach (var productId in itemsToOrder.Keys)
                    {
                         updatedCart.Remove(productId);
                    }

                    if (updatedCart.Count > 0)
                    {
                         HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(updatedCart));
                    }
                    else
                    {
                         HttpContext.Session.Remove("Cart");
                    }

                    // Trả về payment method để client biết redirect sang đâu
                    var redirectUrl = request.PaymentMethod == "COD" 
                         ? $"/Cart/PaymentSuccess?orderId={order.Id}"
                         : $"/Cart/PaymentInfo?orderId={order.Id}";

                    return Json(new { success = true, orderId = order.Id, totalAmount = order.TotalAmount, redirectUrl = redirectUrl });
               }
               catch (Exception ex)
               {
                    System.Diagnostics.Debug.WriteLine($"Lỗi CreateOrder: {ex.Message}");
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
               }
          }

          // Payment Success page
          public async Task<IActionResult> PaymentSuccess(int orderId)
          {
               var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

               if (order == null)
               {
                    return NotFound();
               }

               var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);

               ViewBag.OrderId = order.Id;
               ViewBag.OrderCode = $"ĐH-{order.CreatedAt?.ToString("yyyyMMdd")}-{order.Id.ToString("D3")}";
               ViewBag.TotalAmount = order.TotalAmount;
               ViewBag.PaymentMethod = payment?.Method;
               ViewBag.ReceiverName = order.ReceiverName;
               ViewBag.ReceiverAddress = order.ReceiverAddress;

               return View();
          }

          // Trang thông tin chuyển khoản
          public async Task<IActionResult> PaymentInfo(int orderId)
          {
               var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

               if (order == null)
               {
                    return NotFound();
               }

               var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
               ViewBag.Payment = payment;

               return View(order);
          }

          // Hủy giao dịch chuyển khoản: chỉ hủy đơn và giao dịch, không hoàn hàng về giỏ
          [HttpGet]
          public async Task<IActionResult> CancelBankTransfer(int orderId)
          {
               var userIdStr = HttpContext.Session.GetString("UserId");
               if (string.IsNullOrEmpty(userIdStr))
               {
                    return RedirectToAction("Login", "Home");
               }

               int userId = int.Parse(userIdStr);

               var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

               if (order == null)
               {
                    TempData["ErrorMessage"] = "Không tìm thấy đơn hàng để hủy";
                    return RedirectToAction("Index");
               }

               var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
               if (payment == null || !string.Equals(payment.Method, "BANK_TRANSFER", StringComparison.OrdinalIgnoreCase))
               {
                    TempData["ErrorMessage"] = "Đơn hàng này không thuộc giao dịch chuyển khoản";
                    return RedirectToAction("Index");
               }

               // Đã hủy trước đó thì không xử lý lại
               if (order.Status == 5)
               {
                    return RedirectToAction("Index");
               }

               // Rollback coupon usage if this order consumed one
               if (order.CouponId.HasValue && (order.DiscountAmount ?? 0) > 0)
               {
                    var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Id == order.CouponId.Value);
                    if (coupon != null && (coupon.UsedCount ?? 0) > 0)
                    {
                         coupon.UsedCount = coupon.UsedCount.Value - 1;
                    }
               }

               // Mã không xóa để giữ lịch sử, chỉ cập nhật trạng thái để không tính vào báo cáo doanh thu
               order.Status = 5;
               payment.Status = 0;

               await _context.SaveChangesAsync();

               TempData["SuccessMessage"] = "Đã hủy giao dịch và hủy đơn hàng thành công";
               return RedirectToAction("Index");
          }
          // 1. Webhook nhận thông báo từ SePay
          [HttpPost]
          [IgnoreAntiforgeryToken]
          public async Task<IActionResult> SePayWebhook()
          {
               try
               {
                    // Đọc nội dung request thủ công để debug nếu cần
                    using var reader = new StreamReader(Request.Body);
                    var body = await reader.ReadToEndAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"SePay Webhook Received: {body}");
                    
                    if (string.IsNullOrEmpty(body)) {
                         return BadRequest(new { success = false, message = "Empty body" });
                    }

                    var data = JsonSerializer.Deserialize<JsonElement>(body);

                    string content = string.Empty;
                    foreach (var key in new[] { "content", "description", "transferContent", "transactionContent", "memo", "des" })
                    {
                         if (data.TryGetProperty(key, out var contentElem) && contentElem.ValueKind == JsonValueKind.String)
                         {
                              content = contentElem.GetString() ?? string.Empty;
                              if (!string.IsNullOrWhiteSpace(content))
                              {
                                   break;
                              }
                         }
                    }

                    decimal transferAmount = 0;
                    foreach (var key in new[] { "transferAmount", "amount" })
                    {
                         if (data.TryGetProperty(key, out var amountElem))
                         {
                              if (amountElem.ValueKind == JsonValueKind.Number && amountElem.TryGetDecimal(out var numericAmount))
                              {
                                   transferAmount = numericAmount;
                                   break;
                              }

                              if (amountElem.ValueKind == JsonValueKind.String && decimal.TryParse(amountElem.GetString(), out var parsedAmount))
                              {
                                   transferAmount = parsedAmount;
                                   break;
                              }
                         }
                    }

                    System.Diagnostics.Debug.WriteLine($"Parsed SePay: Content='{content}', Amount={transferAmount}");

                    int orderId = ExtractOrderId(content);
                    System.Diagnostics.Debug.WriteLine($"Extracted OrderID: {orderId}");

                    if (orderId <= 0 && transferAmount > 0)
                    {
                         // Tìm kiếm theo số tiền nếu không có mã đơn hàng
                         var candidateOrderIds = await _context.Orders
                              .Where(o =>
                                   (o.Status == 1 || o.Status == null) &&
                                   o.TotalAmount.HasValue &&
                                   o.TotalAmount.Value == transferAmount &&
                                   o.CreatedAt.HasValue &&
                                   o.CreatedAt.Value >= DateTime.Now.AddDays(-1))
                              .OrderByDescending(o => o.CreatedAt)
                              .Select(o => o.Id)
                              .Take(2)
                              .ToListAsync();

                         if (candidateOrderIds.Count == 1)
                         {
                              orderId = candidateOrderIds[0];
                              System.Diagnostics.Debug.WriteLine($"Found candidate OrderID by amount: {orderId}");
                         }
                    }

                    if (orderId <= 0)
                    {
                         return Ok(new { success = false, message = "Không tìm thấy đơn hàng trong nội dung chuyển khoản" });
                    }

                    var order = await _context.Orders.FindAsync(orderId);

                    if (order != null && (order.Status == 1 || order.Status == null))
                    {
                         // Kiểm tra số tiền (cho phép sai số nhỏ hoặc khách chuyển dư)
                         if (transferAmount >= (order.TotalAmount ?? 0) - 100) 
                         {
                              order.Status = 2; // 2: Xác nhận (Đã thanh toán)

                              var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
                              if (payment != null)
                              {
                                   payment.Status = 4; // 4: Thành công
                                   if (data.TryGetProperty("id", out var idElem)) payment.TransactionId = idElem.ToString();
                                   else if (data.TryGetProperty("transactionId", out var txElem)) payment.TransactionId = txElem.ToString();
                              }

                              await _context.SaveChangesAsync();
                              System.Diagnostics.Debug.WriteLine($"Order {orderId} confirmed successfully via SePay");
                              return Ok(new { success = true, message = "Xác nhận đơn hàng thành công" });
                         }
                         else 
                         {
                              System.Diagnostics.Debug.WriteLine($"Order {orderId} amount mismatch: Need {order.TotalAmount}, got {transferAmount}");
                              return Ok(new { success = false, message = "Số tiền không khớp" });
                         }
                    }

                    return Ok(new { success = false, message = "Đơn hàng không tồn tại hoặc đã được xử lý" });
               }
               catch (Exception ex)
               {
                    System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR in SePayWebhook: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    // Luôn trả về Ok để SePay không gửi lại liên tục nếu đây là lỗi logic, 
                    // hoặc trả về lỗi cụ thể để debug. Ở đây trả về Ok để tránh loop 500/502 từ phía SePay.
                    return Ok(new { success = false, error = ex.Message });
               }
          }

          private static int ExtractOrderId(string content)
          {
               if (string.IsNullOrWhiteSpace(content))
               {
                    return 0;
               }

               var preferredMatch = System.Text.RegularExpressions.Regex.Match(content, @"ĐH-\d{8}-(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
               if (preferredMatch.Success && int.TryParse(preferredMatch.Groups[1].Value, out var preferredId))
               {
                    return preferredId;
               }

               var separatedMatch = System.Text.RegularExpressions.Regex.Match(content, @"\b(?:DH|ĐH)[-_ ]?(\d{8})[-_ ]?(\d{1,6})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
               if (separatedMatch.Success && int.TryParse(separatedMatch.Groups[2].Value, out var separatedId))
               {
                    return separatedId;
               }

               var compactContent = new string(content.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
               var compactMatch = System.Text.RegularExpressions.Regex.Match(compactContent, @"(?:DH|ĐH)(\d{8})(\d{1,6})");
               if (compactMatch.Success && int.TryParse(compactMatch.Groups[2].Value, out var compactId))
               {
                    return compactId;
               }

               var fallbackMatch = System.Text.RegularExpressions.Regex.Match(content, @"(\d+)(?!.*\d)");
               return fallbackMatch.Success && int.TryParse(fallbackMatch.Groups[1].Value, out var fallbackId) ? fallbackId : 0;
          }
          
          // 2. API kiểm tra trạng thái đơn hàng (dùng cho JS ở frontend)
          [HttpGet]
          public async Task<IActionResult> GetOrderStatus(int orderId)
          {
               var order = await _context.Orders
                    .Select(o => new { o.Id, o.Status })
                    .FirstOrDefaultAsync(o => o.Id == orderId);
          
               if (order == null) return NotFound();
          
               return Json(new { status = order.Status });
          }

          // 3. Hàm xác nhận thanh toán thủ công (Dùng để Demo/Test nhanh)
          [HttpGet]
          public async Task<IActionResult> VerifyPayment(int orderId)
          {
               var order = await _context.Orders.FindAsync(orderId);
               if (order != null)
               {
                    order.Status = 2; // 2: Xác nhận
                    var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
                    if (payment != null)
                    {
                         payment.Status = 4; // 4: Thành công
                         payment.TransactionId = "MANUAL_" + DateTime.Now.Ticks.ToString();
                    }
                    await _context.SaveChangesAsync();
               }
               return RedirectToAction("PaymentSuccess", new { orderId = orderId });
          }
          
          // Debug endpoint để kiểm tra Session
          [HttpGet]
          public IActionResult GetCartDebug()
          {
               var cartJson = HttpContext.Session.GetString("Cart");
               var sessionId = HttpContext.Session.Id;
               
               var cart = new Dictionary<int, int>();
               if (!string.IsNullOrEmpty(cartJson))
               {
                    cart = JsonSerializer.Deserialize<Dictionary<int, int>>(cartJson) ?? new Dictionary<int, int>();
               }

               return Json(new 
               { 
                    sessionId = sessionId,
                    cartJson = cartJson,
                    cartItems = cart,
                    cartCount = cart.Sum(x => x.Value), // Đếm tổng số lượng sản phẩm
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
               });
          }
     }

     public class CartRequest
     {
          public int ProductId { get; set; }
          public int Quantity { get; set; }
     }

     public class OrderCheckoutRequest
     {
          public string ReceiverName { get; set; }
          public string ReceiverPhone { get; set; }
          public string ReceiverAddress { get; set; }
          public string Note { get; set; }
          public string PaymentMethod { get; set; }
          public List<int> SelectedProducts { get; set; } = new List<int>();
          public string CouponCode { get; set; }
     }

     public class CouponValidateRequest
     {
          public string CouponCode { get; set; }
          public decimal Subtotal { get; set; }
     }
}
