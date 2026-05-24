using FruitShop.Models;
using System.Collections.Generic;

namespace FruitShop.ViewModels
{
    public class ProductList
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
}
