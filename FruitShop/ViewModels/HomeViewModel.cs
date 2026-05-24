using System.Collections.Generic;
using FruitShop.Models;

namespace FruitShop.ViewModels
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Product> FeaturedProducts { get; set; } = new List<Product>();
        public List<Product> ImportedFruits { get; set; } = new List<Product>();
        public List<Product> LocalFruits { get; set; } = new List<Product>();
        public List<Product> GiftFruits { get; set; } = new List<Product>();
    }
}
