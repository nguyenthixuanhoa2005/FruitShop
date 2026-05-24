using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class Product
{
    public int Id { get; set; }

    public int? CategoryId { get; set; }

    public int? SupplierId { get; set; }

    public string? Sku { get; set; }

    public string? Name { get; set; }

    public decimal? Price { get; set; }

    public int? StockQuantity { get; set; }

    public string? Origin { get; set; }

    public string? Unit { get; set; }

    public decimal? DiscountPercent { get; set; }

    public byte? IsFeatured { get; set; }

    public string? Description { get; set; }

    public byte? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public decimal? FinalPrice { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<InventoryLog> InventoryLogs { get; set; } = new List<InventoryLog>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Supplier? Supplier { get; set; }
}
