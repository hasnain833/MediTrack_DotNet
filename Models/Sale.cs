using System;
using System.Collections.Generic;

namespace MediTrack.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int? CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public DateTime SaleDate { get; set; }
        
        public List<SaleItem> Items { get; set; } = new();
    }

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int InventoryId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }
}
