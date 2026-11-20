using System;
using System.Collections.Generic;

namespace FruitShop.Models;

public partial class Role
{
    public byte Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
