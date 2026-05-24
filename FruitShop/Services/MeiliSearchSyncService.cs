using FruitShop.Interfaces;
using FruitShop.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FruitShop.Services
{
    public class MeiliSearchSyncService
    {
        private readonly FruitShopContext _context;
        private readonly ISearchService _searchService;

        public MeiliSearchSyncService(FruitShopContext context, ISearchService searchService)
        {
            _context = context;
            _searchService = searchService;
        }

        public async Task SyncAllAsync()
        {
            await SyncProductsAsync();
            await SyncCategoriesAsync();
            await SyncUsersAsync();
            await SyncOrdersAsync();
        }

        public async Task SyncProductsAsync()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Select(p => new
                {
                    name = p.Name,
                    id = p.Id.ToString(),
                    sku = p.Sku,
                    price = p.Price,
                    finalPrice = p.FinalPrice,
                    categoryName = p.Category != null ? p.Category.Name : "",
                    supplierName = p.Supplier != null ? p.Supplier.Name : "",
                    origin = p.Origin,
                    description = p.Description,
                    status = p.Status
                })
                .ToListAsync();

            if (products.Any())
            {
                await _searchService.IndexDocumentsAsync("products", products);
            }
        }

        public async Task SyncCategoriesAsync()
        {
            var categories = await _context.Categories
                .Select(c => new
                {
                    id = c.Id.ToString(),
                    name = c.Name,
                    status = c.Status
                })
                .ToListAsync();

            if (categories.Any())
            {
                await _searchService.IndexDocumentsAsync("categories", categories);
            }
        }

        public async Task SyncUsersAsync()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    id = u.Id.ToString(),
                    fullName = u.FullName,
                    email = u.Email,
                    phone = u.Phone,
                    status = u.Status
                })
                .ToListAsync();

            if (users.Any())
            {
                await _searchService.IndexDocumentsAsync("users", users);
            }
        }

        public async Task SyncOrdersAsync()
        {
            var orders = await _context.Orders
                .Include(o => o.Payments)
                .Select(o => new
                {
                    id = o.Id.ToString(),
                    receiverName = o.ReceiverName,
                    receiverPhone = o.ReceiverPhone,
                    orderStatus = o.Status,
                    paymentStatus = o.Payments.Any() ? o.Payments.First().Status : null,
                    totalAmount = o.TotalAmount
                })
                .ToListAsync();

            if (orders.Any())
            {
                await _searchService.IndexDocumentsAsync("orders", orders);
            }
        }
    }
}
