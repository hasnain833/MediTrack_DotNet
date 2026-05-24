using System;
using System.Collections.Generic;

namespace DChemist.Models
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
        public string Status { get; set; } = "Completed";
        
        public List<SaleItem> Items { get; set; } = new();

        public decimal TotalProfit
        {
            get
            {
                decimal profit = 0;
                foreach (var item in Items)
                {
                    profit += item.Profit;
                }
                return profit;
            }
        }
    }

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int? MedicineId { get; set; }
        public int? BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }

        public decimal PurchasePrice { get; set; }
        public int NetQuantity => Quantity - ReturnedQuantity;
        public decimal NetSubtotal => NetQuantity * UnitPrice;
        public decimal Profit => NetSubtotal - (NetQuantity * PurchasePrice);
    }
}
