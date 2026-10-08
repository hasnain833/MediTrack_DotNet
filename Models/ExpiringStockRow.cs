using System;

namespace DChemist.Models
{
    /// <summary>One expiring batch on the supplier return sheet (Items → Expiring → Export).</summary>
    public class ExpiringStockRow
    {
        public int No { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierPhone { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public int RemainingUnits { get; set; }
        public string? InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public decimal UnitCost { get; set; }

        public decimal Value => Math.Round(RemainingUnits * UnitCost, 2);
        public string ExpiryText => ExpiryDate.ToString("MM/yyyy");
        public string InvoiceText => string.IsNullOrWhiteSpace(InvoiceNo) ? "—"
            : InvoiceDate.HasValue ? $"{InvoiceNo} ({InvoiceDate:dd-MM-yy})" : InvoiceNo;
        public string StatusText
        {
            get
            {
                int days = (ExpiryDate.Date - DateTime.Today).Days;
                return days < 0 ? "Expired" : days == 0 ? "Today" : $"{days} days";
            }
        }
        public string UnitCostText => UnitCost.ToString("N2");
        public string ValueText => Value.ToString("N2");
    }
}
