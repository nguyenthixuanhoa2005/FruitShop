using FruitShop.Models;
using System.Collections.Generic;

namespace FruitShop.ViewModels
{
    public class CategoryList
    {
        public List<Category> Categories { get; set; } = new List<Category>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
}
