using System;

namespace FruitShop.Models;

public partial class PasswordHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
