using System;

namespace DChemist.Models
{
    public class InventoryBatch
    {
        public int Id { get; set; }
        public int MedicineId { get; set; }
        public int? SupplierId { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        public int QuantityUnits { get; set; }
        public decimal PurchaseTotalPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal SellingPrice { get; set; }
        public int RemainingUnits { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime? InvoiceDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
