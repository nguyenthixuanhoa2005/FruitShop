using System.Collections.Generic;
using FruitShop.Models;

namespace FruitShop.ViewModels
{
    public class SupplierList
    {
        public List<Supplier> Suppliers { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
}
