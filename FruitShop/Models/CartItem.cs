using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class CartItem
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? SessionId { get; set; }

    public int? ProductId { get; set; }

    public int? Quantity { get; set; }

    public byte? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Product? Product { get; set; }
}
