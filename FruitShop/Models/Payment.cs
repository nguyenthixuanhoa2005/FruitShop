using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class Payment
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public string? Method { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public byte? Status { get; set; }

    public string? TransactionId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Order? Order { get; set; }
}
