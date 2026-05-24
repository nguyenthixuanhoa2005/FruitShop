using System;
using System.Collections.Generic;
using FruitShop.Models;

namespace FruitShop.ViewModels
{
    public class InventoryViewModel
    {
        public List<InventoryReceiptItem> Receipts { get; set; } = new List<InventoryReceiptItem>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }

        // Statistics for Fruit Shop
        public int TotalImportMonth { get; set; }
        public int TotalExportMonth { get; set; }
        public int LowStockAlertCount { get; set; }
    }

    public class InventoryReceiptItem
    {
        public string ReceiptCode { get; set; } // Derived from Note
        public string OriginalNote { get; set; } // The full note string for grouping/finding details
        public string Type { get; set; } // Import/Export
        public DateTime CreatedAt { get; set; }
        public int TotalProducts { get; set; }
        public int TotalQuantity { get; set; }
        public string SummaryNote { get; set; }
        public string ProductNames { get; set; } // Tên các sản phẩm trong phiếu
    }

    public class InventoryReceiptDetailModel
    {
        public string ReceiptCode { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Note { get; set; }
        public List<ReceiptProductDetail> Items { get; set; } = new List<ReceiptProductDetail>();
    }

    public class ReceiptProductDetail
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Current price from Product table
        public decimal TotalPrice => Quantity * UnitPrice;
    }

    public class CreateReceiptRequest
    {
        public string Type { get; set; } // Import/Export
        public string Note { get; set; }
        public List<ProductSelection> Products { get; set; }
    }

    public class ProductSelection
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
