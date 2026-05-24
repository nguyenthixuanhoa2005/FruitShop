using FruitShop.Models;
using System.Collections.Generic;

namespace FruitShop.ViewModels
{
    public class UserList
    {
        public List<User> Users { get; set; } = new List<User>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
}
