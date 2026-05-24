using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class ProductImage
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public string? ImageUrl { get; set; }

    public byte? IsMain { get; set; }

    public virtual Product? Product { get; set; }
}
