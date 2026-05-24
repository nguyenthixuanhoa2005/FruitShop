using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class User
{
    public int Id { get; set; }

    public byte? RoleId { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? PasswordHash { get; set; }

    public string? AvatarUrl { get; set; }

    public byte? IsVerified { get; set; }

    public byte? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? ResetToken { get; set; }

    public DateTime? ResetTokenExpiry { get; set; }

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Role? Role { get; set; }
}
