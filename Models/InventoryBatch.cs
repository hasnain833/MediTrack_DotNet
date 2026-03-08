using System;

namespace DChemist.Models
{
    public class InventoryBatch
    {
        public int Id { get; set; }
        public int MedicineId { get; set; }
        public int SupplierId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQty { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
