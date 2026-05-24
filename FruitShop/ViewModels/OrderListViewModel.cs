using System;
using System.Collections.Generic;
using FruitShop.Models;

namespace FruitShop.ViewModels
{
    public class OrderListViewModel
    {
        public List<OrderListItem> Orders { get; set; } = new List<OrderListItem>();
        public int TotalOrders { get; set; }
        public decimal TotalAmount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        
        // Advanced Statistics
        public int PendingCount { get; set; }
        public int ShippingCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public class OrderListItem
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string Phone { get; set; }
        public decimal TotalAmount { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public string PaymentMethod { get; set; }
    }
}
