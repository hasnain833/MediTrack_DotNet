using System;

namespace DChemist.Models
{
    public class InventoryBatch
    {
        public int Id { get; set; }
        public int MedicineId { get; set; }
        public string? MedicineName { get; set; }
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
        public string EntryMode { get; set; } = "Tablet";
        public int UnitsPerPack { get; set; } = 1;
        public int PacketsPerBox { get; set; } = 1;
        public int PackQuantity { get; set; } = 0;
        public int? PurchaseInvoiceId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string FormattedQuantity
        {
            get
            {
                if (EntryMode == "Box" && PacketsPerBox > 0 && UnitsPerPack > 0)
                {
                    int unitsPerBox = PacketsPerBox * UnitsPerPack;
                    int boxes = QuantityUnits / unitsPerBox;
                    int loose = QuantityUnits % unitsPerBox;
                    if (loose == 0) return $"{boxes} Box";
                    return $"{boxes} Box + {loose} Tab";
                }
                return $"{QuantityUnits} Units";
            }
        }
    }
}
