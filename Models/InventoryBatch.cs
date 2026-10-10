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

        // Purchase terms (latest purchase into this batch row)
        public int BonusUnits { get; set; }
        public decimal DiscountPercent { get; set; }
        /// <summary>Total as printed on the supplier invoice; 0 on rows saved before this was recorded.</summary>
        public decimal InvoiceAmount { get; set; }
        public bool IsNet { get; set; }

        public decimal InvoiceAmountOrNet => InvoiceAmount > 0 ? InvoiceAmount : PurchaseTotalPrice;
        public string BonusText => BonusUnits > 0 ? $"{BonusUnits} units" : "—";
        public string DiscOrPaidText => IsNet ? "net price" : DiscountPercent > 0 ? $"{DiscountPercent:0.##}%" : "—";
        public string ExpiryText => ExpiryDate.ToString("MM/yy");
        public bool IsExpiringSoon => DChemist.Utils.ExpiryPolicy.NeedsAttention(ExpiryDate, RemainingUnits);

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
