using FruitShop.Models;
using System.Collections.Generic;

namespace FruitShop.ViewModels
{
    public class CouponList
    {
        public List<Coupon> Coupons { get; set; } = new List<Coupon>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
    }
}
