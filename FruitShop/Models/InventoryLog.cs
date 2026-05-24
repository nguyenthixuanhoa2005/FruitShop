using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class InventoryLog
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public string? ChangeType { get; set; }

    public int? Quantity { get; set; }

    public byte? Status { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Product? Product { get; set; }
}
